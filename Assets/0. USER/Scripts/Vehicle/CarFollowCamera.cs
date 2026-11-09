using System.Collections.Generic;
using FullThrottleSun.Controller;
using UnityEngine;
using UnityEngine.XR;

namespace FullThrottleSun.Vehicle
{
    /// <summary>
    /// First-person driver camera only. VR headset (or the driver-eye pose on a flat screen) drives the view.
    /// No stick look — head tracking / the fixed driver eye aims the camera.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CarFollowCamera : MonoBehaviour
    {
        [SerializeField] Transform target;

        [Header("First person")]
        [Tooltip("Eye position and straight-ahead direction inside the car.")]
        [SerializeField] Transform driverEye;
        [Tooltip("Vertical field of view on a flat screen. The headset sets its own in VR.")]
        [Range(20f, 120f)] [SerializeField] float firstPersonFov = 80f;
        [Tooltip("Small near clip so the dashboard right in front of the eye isn't cut off.")]
        [Min(0.01f)] [SerializeField] float firstPersonNearClip = 0.05f;

        [Header("VR head tracking")]
        [Tooltip("When a headset is active, the head drives the view.")]
        [SerializeField] bool useHeadTracking = true;
        [Tooltip("Maps the current head pose onto the driver eye. Same button as the gyro bind, so both recalibrate together.")]
        [SerializeField] ControllerButton headRecenterButton = ControllerButton.Menu;
        [SerializeField] ControllerHand headRecenterHand = ControllerHand.Left;
        [Tooltip("How far (m) leaning can move the view away from the anchor, so the camera can't leave the cabin.")]
        [Min(0f)] [SerializeField] float maxHeadOffset = 0.3f;

        Camera cam;

        readonly List<InputDevice> headDevices = new List<InputDevice>();
        bool headCalibrated;
        bool headWasPresent;
        Vector3 headCalibrationPosition;
        float headCalibrationYaw;
        bool vrPoseValid;
        bool headTracking;
        Vector3 vrAnchorPosition;
        Quaternion vrAnchorRotation;

        public Transform Target => target;
        /// <summary>Always true while a driver eye is assigned (third person was removed).</summary>
        public bool IsFirstPerson => driverEye != null;
        /// <summary>True while a calibrated headset drives the view.</summary>
        public bool IsHeadTracked => headTracking && headCalibrated;
        /// <summary>Changes every recenter, when tracked poses jump.</summary>
        public int CalibrationVersion { get; private set; }

        /// <summary>
        /// Maps a pose from the headset's tracking space (e.g. a controller) into the target's local space, using the
        /// same calibration as the head: the calibrated head lands on the driver eye.
        /// </summary>
        public bool TryTrackedToTarget(Vector3 position, Quaternion rotation, out Vector3 localPosition, out Quaternion localRotation)
        {
            localPosition = default;
            localRotation = Quaternion.identity;
            if (!IsHeadTracked || target == null || driverEye == null)
                return false;
            Quaternion yaw = Quaternion.Euler(0f, -headCalibrationYaw, 0f);
            localPosition = target.InverseTransformPoint(driverEye.position) + yaw * (position - headCalibrationPosition);
            localRotation = yaw * rotation;
            return true;
        }

        void Awake() => cam = GetComponent<Camera>();

        void OnEnable() => Application.onBeforeRender += ApplyHeadPose;

        void OnDisable() => Application.onBeforeRender -= ApplyHeadPose;

        void LateUpdate()
        {
            if (target == null || driverEye == null)
            {
                headTracking = false;
                return;
            }

            bool vr = useHeadTracking && TryGetHeadPose(out _, out _);
            headTracking = vr;
            vrPoseValid = false;

            if (vr)
            {
                // Putting the headset on also recenters, since a pose taken while it sat on the desk is meaningless.
                bool present = IsUserPresent();
                if (!headCalibrated || (present && !headWasPresent)
                    || VirtualController.GetButtonDown(headRecenterButton, headRecenterHand))
                    RecenterHead();
                headWasPresent = present;
            }

            UpdateFirstPerson(vr);

            if (vr)
                ApplyHeadPose();
        }

        /// <summary>Makes the current real head pose count as sitting straight in the driver seat.</summary>
        public void RecenterHead()
        {
            if (!TryGetHeadPose(out Vector3 position, out Quaternion rotation))
                return;
            headCalibrationPosition = position;
            headCalibrationYaw = rotation.eulerAngles.y;
            headCalibrated = true;
            CalibrationVersion++;
        }

        void UpdateFirstPerson(bool vr)
        {
            cam.nearClipPlane = firstPersonNearClip;
            if (vr)
            {
                SetHeadAnchor(driverEye.position, target.rotation);
                return;
            }
            cam.fieldOfView = firstPersonFov;
            transform.SetPositionAndRotation(driverEye.position, driverEye.rotation);
        }

        void SetHeadAnchor(Vector3 position, Quaternion rotation)
        {
            vrAnchorPosition = position;
            vrAnchorRotation = rotation;
            vrPoseValid = true;
        }

        /// <summary>Also runs right before rendering with the newest head pose, which keeps VR from lagging a frame.</summary>
        void ApplyHeadPose()
        {
            if (!vrPoseValid || !headCalibrated || !TryGetHeadPose(out Vector3 position, out Quaternion rotation))
                return;
            Quaternion space = vrAnchorRotation * Quaternion.Euler(0f, -headCalibrationYaw, 0f);
            Vector3 offset = Vector3.ClampMagnitude(position - headCalibrationPosition, maxHeadOffset);
            transform.SetPositionAndRotation(vrAnchorPosition + space * offset, space * rotation);
        }

        /// <summary>True while the headset is worn, or when the runtime doesn't report it.</summary>
        bool IsUserPresent()
        {
            foreach (var device in headDevices)
                if (device.TryGetFeatureValue(CommonUsages.userPresence, out bool present))
                    return present;
            return true;
        }

        bool TryGetHeadPose(out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = Quaternion.identity;
            if (!XRSettings.isDeviceActive)
                return false;

            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.HeadMounted, headDevices);
            foreach (var device in headDevices)
            {
                if (device.TryGetFeatureValue(CommonUsages.centerEyePosition, out position)
                    && device.TryGetFeatureValue(CommonUsages.centerEyeRotation, out rotation))
                    return true;
                if (device.TryGetFeatureValue(CommonUsages.devicePosition, out position)
                    && device.TryGetFeatureValue(CommonUsages.deviceRotation, out rotation))
                    return true;
            }
            return false;
        }
    }
}
