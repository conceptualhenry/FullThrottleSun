#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;
using UnityEngine;

namespace FullThrottleSun.Controller
{
    /// <summary>
    /// Background thread that opens the DualSense HID interface in shared mode and parses sensor bytes.
    /// </summary>
    internal sealed class DualSenseHidReader
    {
        const ushort SonyVendorId = 0x054C;
        const ushort DualSenseProductId = 0x0CE6;
        const ushort DualSenseEdgeProductId = 0x0DF2;

        const byte UsbReportId = 0x01;
        const byte BluetoothReportId = 0x31;
        const byte CalibrationFeatureReportId = 0x05;
        const int UsbFullReportMinLength = 64;

        // Offsets from the first payload byte (right after the report id, plus one sequence byte on Bluetooth).
        const int GyroOffset = 15;
        const int AccelOffset = 21;
        const int TimestampOffset = 27;

        const int RescanIntervalMs = 1000;
        const int ReconnectDelayMs = 200;
        const int ReadWaitMs = 100;
        // The DualSense streams reports continuously, so a gap this long means the handle stopped delivering.
        const int StallTimeoutMs = 1000;
        const double MaxIntegrationStep = 0.1;

        // Pressing the bind button shakes the controller, so sampling starts only after it settles.
        const double CalibrationSettleSeconds = 0.3;
        // Spread (max - min) of any axis above this during calibration means the controller moved; sampling restarts.
        const float CalibrationMotionTolerance = 8f;
        // Corrected rates below this (°/s) are treated as sensor noise: not integrated, and used to track bias drift.
        const float StillDeadband = 0.8f;
        const float BiasTrackingTimeConstant = 3f;

        readonly object sampleLock = new object();
        readonly Stopwatch clock = Stopwatch.StartNew();
        DualSenseMotionSample latest = new DualSenseMotionSample { status = "Starting" };

        Thread thread;
        volatile bool running;

        double lastSampleTime = -1;
        double angleX, angleY, angleZ;
        Vector3 gyroBias;
        bool calibrating;
        double calibrationDuration;
        double calibrationStartTime;
        Vector3 calibrationSum;
        Vector3 calibrationMin;
        Vector3 calibrationMax;
        int calibrationCount;

        public DualSenseMotionSample Latest
        {
            get { lock (sampleLock) return latest; }
        }

        public bool IsCalibrating
        {
            get { lock (sampleLock) return calibrating; }
        }

        public void Calibrate(float seconds)
        {
            lock (sampleLock)
            {
                calibrating = true;
                calibrationDuration = seconds;
                RestartCalibrationWindow(clock.Elapsed.TotalSeconds);
            }
        }

        void RestartCalibrationWindow(double now)
        {
            calibrationStartTime = now + CalibrationSettleSeconds;
            calibrationSum = Vector3.zero;
            calibrationMin = Vector3.positiveInfinity;
            calibrationMax = Vector3.negativeInfinity;
            calibrationCount = 0;
        }

        public void ResetAngle()
        {
            lock (sampleLock)
                angleX = angleY = angleZ = 0;
        }

        public void Start()
        {
            running = true;
            thread = new Thread(Run) { IsBackground = true, Name = "DualSenseHidReader" };
            thread.Start();
        }

        public void Stop()
        {
            running = false;
            if (thread != null && thread.IsAlive)
                thread.Join(1000);
            thread = null;
        }

        void Run()
        {
            while (running)
            {
                string path = FindDevicePath();
                if (path == null)
                {
                    Publish(new DualSenseMotionSample { status = "Searching for DualSense..." });
                    Thread.Sleep(RescanIntervalMs);
                    continue;
                }

                try
                {
                    ReadDevice(path);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogWarning($"[DualSenseMotion] {e.Message}");
                }

                lastSampleTime = -1;
                if (running)
                {
                    Publish(new DualSenseMotionSample { status = "Reconnecting..." });
                    Thread.Sleep(ReconnectDelayMs);
                }
            }
        }

        void ReadDevice(string path)
        {
            using (var handle = Native.CreateFile(path, Native.GenericRead | Native.GenericWrite,
                       Native.FileShareRead | Native.FileShareWrite, IntPtr.Zero, Native.OpenExisting,
                       Native.FileFlagOverlapped, IntPtr.Zero))
            {
                if (handle.IsInvalid)
                {
                    Publish(new DualSenseMotionSample { status = $"Cannot open device (error {Marshal.GetLastWin32Error()})" });
                    Thread.Sleep(RescanIntervalMs);
                    return;
                }

                GetCaps(handle, out int inputLength, out int featureLength);
                bool isBluetooth = path.IndexOf("00001124-0000-1000-8000-00805f9b34fb", StringComparison.OrdinalIgnoreCase) >= 0;

                // Reading the calibration feature report switches a Bluetooth DualSense to full reports.
                if (featureLength > 0)
                {
                    var feature = new byte[featureLength];
                    feature[0] = CalibrationFeatureReportId;
                    Native.HidD_GetFeature(handle, feature, feature.Length);
                }

                ReadLoop(handle, inputLength, isBluetooth);
            }
        }

        void ReadLoop(SafeFileHandle handle, int inputLength, bool isBluetooth)
        {
            IntPtr buffer = Marshal.AllocHGlobal(inputLength);
            IntPtr overlapped = Marshal.AllocHGlobal(Marshal.SizeOf<NativeOverlapped>());
            var managed = new byte[inputLength];

            var rateTimer = Stopwatch.StartNew();
            int reportsThisSecond = 0;
            float sampleRate = 0f;
            long lastReportMs = clock.ElapsedMilliseconds;

            try
            {
                using (var readEvent = new ManualResetEvent(false))
                {
                    while (running)
                    {
                        readEvent.Reset();
                        Marshal.StructureToPtr(new NativeOverlapped { EventHandle = readEvent.SafeWaitHandle.DangerousGetHandle() }, overlapped, false);

                        if (!Native.ReadFile(handle, buffer, inputLength, IntPtr.Zero, overlapped)
                            && Marshal.GetLastWin32Error() != Native.ErrorIoPending)
                            return;

                        while (!readEvent.WaitOne(ReadWaitMs))
                        {
                            bool stalled = clock.ElapsedMilliseconds - lastReportMs > StallTimeoutMs;
                            if (running && !stalled)
                                continue;
                            Native.CancelIoEx(handle, overlapped);
                            Native.GetOverlappedResult(handle, overlapped, out _, true);
                            if (stalled)
                                UnityEngine.Debug.Log("[DualSenseMotion] No reports for 1s, reopening device");
                            return;
                        }

                        if (!Native.GetOverlappedResult(handle, overlapped, out int bytesRead, false))
                            return;

                        lastReportMs = clock.ElapsedMilliseconds;
                        reportsThisSecond++;
                        if (rateTimer.ElapsedMilliseconds >= 1000)
                        {
                            sampleRate = reportsThisSecond * 1000f / rateTimer.ElapsedMilliseconds;
                            reportsThisSecond = 0;
                            rateTimer.Restart();
                        }

                        Marshal.Copy(buffer, managed, 0, bytesRead);
                        Publish(Parse(managed, bytesRead, isBluetooth, sampleRate));
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(overlapped);
                Marshal.FreeHGlobal(buffer);
            }
        }

        static DualSenseMotionSample Parse(byte[] report, int length, bool isBluetooth, float sampleRate)
        {
            var sample = new DualSenseMotionSample
            {
                connection = isBluetooth ? MotionConnection.Bluetooth : MotionConnection.Usb,
                sampleRate = sampleRate
            };

            int payload;
            if (report[0] == BluetoothReportId)
                payload = 2;
            else if (report[0] == UsbReportId && !isBluetooth && length >= UsbFullReportMinLength)
                payload = 1;
            else
            {
                sample.status = isBluetooth ? "Bluetooth simple mode (no sensor data)" : $"Unexpected report 0x{report[0]:X2}";
                return sample;
            }

            if (length < payload + TimestampOffset + 4)
            {
                sample.status = $"Report too short ({length} bytes)";
                return sample;
            }

            sample.hasMotionData = true;
            sample.status = "Connected";
            sample.rawGyro = ReadVector(report, payload + GyroOffset);
            sample.rawAccel = ReadVector(report, payload + AccelOffset);
            sample.sensorTimestamp = BitConverter.ToUInt32(report, payload + TimestampOffset);
            return sample;
        }

        static Vector3Int ReadVector(byte[] report, int offset) => new Vector3Int(
            BitConverter.ToInt16(report, offset),
            BitConverter.ToInt16(report, offset + 2),
            BitConverter.ToInt16(report, offset + 4));

        void Publish(DualSenseMotionSample sample)
        {
            double now = clock.Elapsed.TotalSeconds;

            lock (sampleLock)
            {
                if (sample.hasMotionData)
                {
                    Vector3 rate = sample.AngularVelocity;
                    double dt = lastSampleTime >= 0 ? Math.Min(now - lastSampleTime, MaxIntegrationStep) : 0;
                    lastSampleTime = now;

                    if (calibrating)
                        AccumulateCalibration(rate, now);
                    else
                        Integrate(rate, (float)dt);
                }

                sample.integratedAngle = new Vector3((float)angleX, (float)angleY, (float)angleZ);
                sample.gyroBias = gyroBias;
                sample.isCalibrating = calibrating;
                latest = sample;
            }
        }

        void AccumulateCalibration(Vector3 rate, double now)
        {
            if (now < calibrationStartTime)
                return;

            calibrationSum += rate;
            calibrationMin = Vector3.Min(calibrationMin, rate);
            calibrationMax = Vector3.Max(calibrationMax, rate);
            calibrationCount++;

            Vector3 spread = calibrationMax - calibrationMin;
            if (spread.x > CalibrationMotionTolerance || spread.y > CalibrationMotionTolerance || spread.z > CalibrationMotionTolerance)
            {
                RestartCalibrationWindow(now);
                return;
            }

            if (now >= calibrationStartTime + calibrationDuration)
            {
                gyroBias = calibrationSum / calibrationCount;
                calibrating = false;
                angleX = angleY = angleZ = 0;
            }
        }

        void Integrate(Vector3 rate, float dt)
        {
            Vector3 corrected = rate - gyroBias;
            if (corrected.magnitude < StillDeadband)
            {
                gyroBias += corrected * Mathf.Clamp01(dt / BiasTrackingTimeConstant);
                return;
            }

            angleX += corrected.x * dt;
            angleY += corrected.y * dt;
            angleZ += corrected.z * dt;
        }

        static void GetCaps(SafeFileHandle handle, out int inputLength, out int featureLength)
        {
            inputLength = UsbFullReportMinLength;
            featureLength = 0;
            if (!Native.HidD_GetPreparsedData(handle, out IntPtr preparsed))
                return;
            try
            {
                if (Native.HidP_GetCaps(preparsed, out Native.HidpCaps caps) == Native.HidpStatusSuccess)
                {
                    inputLength = caps.InputReportByteLength;
                    featureLength = caps.FeatureReportByteLength;
                }
            }
            finally
            {
                Native.HidD_FreePreparsedData(preparsed);
            }
        }

        static string FindDevicePath()
        {
            Native.HidD_GetHidGuid(out Guid hidGuid);
            IntPtr deviceSet = Native.SetupDiGetClassDevs(ref hidGuid, IntPtr.Zero, IntPtr.Zero,
                Native.DigcfPresent | Native.DigcfDeviceInterface);
            if (deviceSet == Native.InvalidHandleValue)
                return null;

            try
            {
                var interfaceData = new Native.SpDeviceInterfaceData { cbSize = Marshal.SizeOf<Native.SpDeviceInterfaceData>() };
                for (int i = 0; Native.SetupDiEnumDeviceInterfaces(deviceSet, IntPtr.Zero, ref hidGuid, i, ref interfaceData); i++)
                {
                    string path = GetInterfacePath(deviceSet, ref interfaceData);
                    if (path != null && IsDualSense(path))
                        return path;
                }
            }
            finally
            {
                Native.SetupDiDestroyDeviceInfoList(deviceSet);
            }
            return null;
        }

        static string GetInterfacePath(IntPtr deviceSet, ref Native.SpDeviceInterfaceData interfaceData)
        {
            Native.SetupDiGetDeviceInterfaceDetail(deviceSet, ref interfaceData, IntPtr.Zero, 0, out int requiredSize, IntPtr.Zero);
            if (requiredSize <= 0)
                return null;

            IntPtr detail = Marshal.AllocHGlobal(requiredSize);
            try
            {
                // cbSize of SP_DEVICE_INTERFACE_DETAIL_DATA_W: 8 on 64-bit, 6 on 32-bit.
                Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
                if (!Native.SetupDiGetDeviceInterfaceDetail(deviceSet, ref interfaceData, detail, requiredSize, out _, IntPtr.Zero))
                    return null;
                return Marshal.PtrToStringUni(IntPtr.Add(detail, 4));
            }
            finally
            {
                Marshal.FreeHGlobal(detail);
            }
        }

        static bool IsDualSense(string path)
        {
            using (var handle = Native.CreateFile(path, 0, Native.FileShareRead | Native.FileShareWrite,
                       IntPtr.Zero, Native.OpenExisting, 0, IntPtr.Zero))
            {
                if (handle.IsInvalid)
                    return false;
                var attributes = new Native.HiddAttributes { Size = Marshal.SizeOf<Native.HiddAttributes>() };
                return Native.HidD_GetAttributes(handle, ref attributes)
                       && attributes.VendorID == SonyVendorId
                       && (attributes.ProductID == DualSenseProductId || attributes.ProductID == DualSenseEdgeProductId);
            }
        }

        static class Native
        {
            public const uint GenericRead = 0x80000000;
            public const uint GenericWrite = 0x40000000;
            public const uint FileShareRead = 0x1;
            public const uint FileShareWrite = 0x2;
            public const uint OpenExisting = 3;
            public const uint FileFlagOverlapped = 0x40000000;
            public const int ErrorIoPending = 997;
            public const int DigcfPresent = 0x2;
            public const int DigcfDeviceInterface = 0x10;
            public const int HidpStatusSuccess = 0x00110000;
            public static readonly IntPtr InvalidHandleValue = new IntPtr(-1);

            [StructLayout(LayoutKind.Sequential)]
            public struct HiddAttributes
            {
                public int Size;
                public ushort VendorID;
                public ushort ProductID;
                public ushort VersionNumber;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct HidpCaps
            {
                public ushort Usage;
                public ushort UsagePage;
                public ushort InputReportByteLength;
                public ushort OutputReportByteLength;
                public ushort FeatureReportByteLength;
                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
                public ushort[] Reserved;
                public ushort NumberLinkCollectionNodes;
                public ushort NumberInputButtonCaps;
                public ushort NumberInputValueCaps;
                public ushort NumberInputDataIndices;
                public ushort NumberOutputButtonCaps;
                public ushort NumberOutputValueCaps;
                public ushort NumberOutputDataIndices;
                public ushort NumberFeatureButtonCaps;
                public ushort NumberFeatureValueCaps;
                public ushort NumberFeatureDataIndices;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct SpDeviceInterfaceData
            {
                public int cbSize;
                public Guid InterfaceClassGuid;
                public int Flags;
                public IntPtr Reserved;
            }

            [DllImport("hid.dll")]
            public static extern void HidD_GetHidGuid(out Guid hidGuid);

            [DllImport("hid.dll", SetLastError = true)]
            public static extern bool HidD_GetAttributes(SafeFileHandle device, ref HiddAttributes attributes);

            [DllImport("hid.dll", SetLastError = true)]
            public static extern bool HidD_GetPreparsedData(SafeFileHandle device, out IntPtr preparsedData);

            [DllImport("hid.dll")]
            public static extern bool HidD_FreePreparsedData(IntPtr preparsedData);

            [DllImport("hid.dll")]
            public static extern int HidP_GetCaps(IntPtr preparsedData, out HidpCaps capabilities);

            [DllImport("hid.dll", SetLastError = true)]
            public static extern bool HidD_GetFeature(SafeFileHandle device, byte[] buffer, int bufferLength);

            [DllImport("setupapi.dll", SetLastError = true)]
            public static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, IntPtr enumerator, IntPtr hwndParent, int flags);

            [DllImport("setupapi.dll", SetLastError = true)]
            public static extern bool SetupDiEnumDeviceInterfaces(IntPtr deviceInfoSet, IntPtr deviceInfoData,
                ref Guid interfaceClassGuid, int memberIndex, ref SpDeviceInterfaceData deviceInterfaceData);

            [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "SetupDiGetDeviceInterfaceDetailW")]
            public static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr deviceInfoSet, ref SpDeviceInterfaceData deviceInterfaceData,
                IntPtr deviceInterfaceDetailData, int deviceInterfaceDetailDataSize, out int requiredSize, IntPtr deviceInfoData);

            [DllImport("setupapi.dll", SetLastError = true)]
            public static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

            [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "CreateFileW")]
            public static extern SafeFileHandle CreateFile(string fileName, uint desiredAccess, uint shareMode,
                IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool ReadFile(SafeFileHandle file, IntPtr buffer, int numberOfBytesToRead,
                IntPtr numberOfBytesRead, IntPtr overlapped);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool GetOverlappedResult(SafeFileHandle file, IntPtr overlapped, out int numberOfBytesTransferred, bool wait);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool CancelIoEx(SafeFileHandle file, IntPtr overlapped);
        }
    }
}
#endif
