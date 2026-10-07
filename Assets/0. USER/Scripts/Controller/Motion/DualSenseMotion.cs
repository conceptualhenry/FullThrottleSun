using UnityEngine;

namespace FullThrottleSun.Controller
{
    public enum MotionConnection
    {
        None,
        Usb,
        Bluetooth
    }

    public struct DualSenseMotionSample
    {
        public MotionConnection connection;
        /// <summary>False while connected over Bluetooth in simple mode, where reports carry no sensor data.</summary>
        public bool hasMotionData;
        public Vector3Int rawGyro;
        public Vector3Int rawAccel;
        public uint sensorTimestamp;
        public float sampleRate;
        public string status;

        /// <summary>Bias-corrected angular velocity integrated per HID report, in degrees. Drifts slowly over time.</summary>
        public Vector3 integratedAngle;
        /// <summary>Average angular velocity measured during the last calibration, in °/s.</summary>
        public Vector3 gyroBias;
        public bool isCalibrating;

        /// <summary>
        /// Rotation about the gyro Y axis since calibration, in degrees, for a controller held upright like a steering wheel.
        /// Gyro integrated, then pulled toward the angle of gravity so it does not drift.
        /// </summary>
        public float uprightAngle;
        /// <summary>False while gravity can't correct uprightAngle (held flat, or shaken), it then runs on gyro alone.</summary>
        public bool uprightGravityValid;

        /// <summary>Approximate, uncalibrated: ±2000 °/s over the int16 range.</summary>
        public Vector3 AngularVelocity => (Vector3)rawGyro * DualSenseMotion.GyroDegreesPerUnit;

        /// <summary>Approximate, uncalibrated: 8192 units per g.</summary>
        public Vector3 Acceleration => (Vector3)rawAccel * DualSenseMotion.AccelGPerUnit;
    }

    /// <summary>
    /// Gyroscope and accelerometer of a DualSense, read straight from Windows HID because
    /// Unity's DualSense layout discards the sensor bytes. Buttons still come from VirtualController.
    /// </summary>
    public static class DualSenseMotion
    {
        public const float GyroDegreesPerUnit = 2000f / 32768f;
        public const float AccelGPerUnit = 1f / 8192f;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        static DualSenseHidReader reader;
#endif

        public static bool IsSupported =>
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            true;
#else
            false;
#endif

        public static DualSenseMotionSample Current
        {
            get
            {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                if (reader != null)
                    return reader.Latest;
#endif
                return new DualSenseMotionSample { status = IsSupported ? "Not started" : "Only supported on Windows" };
            }
        }

        public static bool IsAvailable => Current.hasMotionData;
        public static Vector3 AngularVelocity => Current.AngularVelocity;
        public static Vector3 Acceleration => Current.Acceleration;
        public static Vector3 IntegratedAngle => Current.integratedAngle;
        public static float UprightAngle => Current.uprightAngle;
        public static bool IsCalibrating
        {
            get
            {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                return reader != null && reader.IsCalibrating;
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// Averages the gyro for the given time to measure its resting bias, then zeroes IntegratedAngle.
        /// Sampling starts after a short settle delay and restarts whenever the controller moves.
        /// </summary>
        public static void Calibrate(float seconds = 1f)
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            reader?.Calibrate(seconds);
#endif
        }

        public static void ResetAngle()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            reader?.ResetAngle();
#endif
        }

        internal static void Start()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (reader != null)
                return;
            reader = new DualSenseHidReader();
            reader.Start();
#endif
        }

        internal static void Stop()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            reader?.Stop();
            reader = null;
#endif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Stop();
    }
}
