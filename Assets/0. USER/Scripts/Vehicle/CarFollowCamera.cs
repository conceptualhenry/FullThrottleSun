using System.Collections.Generic;
using FullThrottleSun.Controller;
using UnityEngine;
using UnityEngine.XR;

namespace FullThrottleSun.Vehicle
{
    /// <summary>
    /// Car camera with a third person chase view and a first person driver view, toggled by a controller button
    /// (right stick click by default).
    /// Flat screen: the right stick looks around, orbiting the car in third person and turning the head in first person,
    /// and eases back to straight ahead once released.
    /// VR headset active: the headset drives the view instead of the stick. The pose at recenter (Options, or the first
    /// tracked frame) is mapped onto the driver eye, so leaning and turning the real head moves from there.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CarFollowCamera : MonoBehaviour
    {
        public enum View { ThirdPerson, FirstPerson }

        [SerializeField] Transform target;
        [SerializeField] View view = View.ThirdPerson;
        [SerializeField] ControllerButton switchButton = ControllerButton.ThumbstickClick;
        [SerializeField] ControllerHand switchHand = ControllerHand.Right;
        [SerializeField] ControllerHand lookHand = ControllerHand.Right;

        [Header("Third person")]
        [Tooltip("Distance behind the target (m).")]
        [Min(0f)] [SerializeField] float distance = 7f;
        [Tooltip("Height above the target's origin (m).")]
        [SerializeField] float height = 2.8f;
        [Tooltip("Point on the target the camera looks at, in the target's local space.")]
        [SerializeField] Vector3 lookOffset = new Vector3(0f, 1.2f, 0f);
        [Tooltip("How quickly the camera swings around to the target's heading. Higher = tighter.")]
        [Min(0f)] [SerializeField] float rotationDamping = 4f;
        [Tooltip("How quickly the camera catches up in height. Higher = tighter.")]
        [Min(0f)] [SerializeField] float heightDamping = 6f;
        [Tooltip("How far (degrees) the right stick can swing the camera below and above its normal height.")]
        [SerializeField] Vector2 orbitPitchRange = new Vector2(-15f, 45f);
        [Tooltip("Vertical field of view on a flat screen. The headset sets its own in VR.")]
        [Range(20f, 120f)] [SerializeField] float thirdPersonFov = 70f;

        [Header("First person")]
        [Tooltip("Eye position and straight-ahead direction inside the car.")]
        [SerializeField] Transform driverEye;
        [Tooltip("How far (degrees) the head can turn left or right.")]
        [Range(0f, 180f)] [SerializeField] float headYawLimit = 120f;
        [Tooltip("How far (degrees) the head can look down and up.")]
        [SerializeField] Vector2 headPitchRange = new Vector2(-40f, 50f);
        [Tooltip("Vertical field of view on a flat screen. The headset sets its own in VR.")]
        [Range(20f, 120f)] [SerializeField] float firstPersonFov = 80f;
        [Tooltip("Small near clip so the dashboard right in front of the eye isn't cut off.")]
        [Min(0.01f)] [SerializeField] float firstPersonNearClip = 0.05f;

        [Header("Right stick look (flat screen)")]
        [Tooltip("Degrees per second at full stick.")]
        [Min(1f)] [SerializeField] float lookSpeed = 180f;
        [SerializeField] bool invertLookY;
        [SerializeField] bool autoRecenter = true;
        [Tooltip("Seconds after releasing the stick before the view eases back.")]
        [Min(0f)] [SerializeField] float recenterDelay = 0.3f;
        [Tooltip("How quickly the view eases back. Higher = faster.")]
        [Min(0.1f)] [SerializeField] float recenterSpeed = 4f;

        [Header("VR head tracking")]
        [Tooltip("When a headset is active, the head drives the view instead of the right stick.")]
        [SerializeField] bool useHeadTracking = true;
        [Tooltip("Maps the current head pose onto the driver eye. Same button as the gyro bind, so both recalibrate together.")]
        [SerializeField] ControllerButton headRecenterButton = ControllerButton.Menu;
        [SerializeField] ControllerHand headRecenterHand = ControllerHand.Left;
        [Tooltip("How far (m) leaning can move the view away from the anchor, so the camera can't leave the cabin.")]
        [Min(0f)] [SerializeField] float maxHeadOffset = 0.3f;

        Camera cam;
        float thirdPersonNearClip;
        float currentYaw;
        float currentHeight;
        float lookYaw;
        float lookPitch;
        float stickReleasedTime;

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
        public bool IsFirstPerson => view == View.FirstPerson && driverEye != null;
        /// <summary>True while a calibrated headset drives the view.</summary>
        public bool IsHeadTracked => headTracking && headCalibrated;
        /// <summary>Changes every recenter, when tracked poses jump.</summary>
        public int CalibrationVersion { get; private set; }

        /// <summary>
        /// Maps a pose from the headset's tracking space (e.g. a controller) into the target's local space, using the
        /// same calibration as the head: the calibrated head lands on the driver eye. First person only.
        /// </summary>
        public bool TryTrackedToTarget(Vector3 position, Quaternion rotation, out Vector3 localPosition, out Quaternion localRotation)
        {
            localPosition = default;
            localRotation = Quaternion.identity;
            if (!IsHeadTracked || !IsFirstPerson || target == null)
                return false;
            Quaternion yaw = Quaternion.Euler(0f, -headCalibrationYaw, 0f);
            localPosition = target.InverseTransformPoint(driverEye.position) + yaw * (position - headCalibrationPosition);
            localRotation = yaw * rotation;
            return true;
        }

        void Awake()
        {
            cam = GetComponent<Camera>();
            thirdPersonNearClip = cam.nearClipPlane;
        }

        void OnEnable() => Application.onBeforeRender += ApplyHeadPose;

        void OnDisable() => Application.onBeforeRender -= ApplyHeadPose;

        void Start() => SnapThirdPerson();

        void LateUpdate()
        {
            if (target == null)
            {
                headTracking = false;
                return;
            }

            if (VirtualController.GetButtonDown(switchButton, switchHand))
                Toggle();

            bool firstPerson = view == View.FirstPerson && driverEye != null;
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
                lookYaw = 0f;
                lookPitch = 0f;
            }
            else
            {
                UpdateLook(firstPerson, Time.deltaTime);
            }

            if (firstPerson)
                UpdateFirstPerson(vr);
            else
                UpdateThirdPerson(vr, Time.deltaTime);

            if (vr)
                ApplyHeadPose();
        }

        public void Toggle()
        {
            view = view == View.ThirdPerson ? View.FirstPerson : View.ThirdPerson;
            lookYaw = 0f;
            lookPitch = 0f;
            if (view == View.ThirdPerson)
                SnapThirdPerson();
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

        void SnapThirdPerson()
        {
            if (target == null)
                return;
            currentYaw = target.eulerAngles.y;
            currentHeight = target.position.y;
        }

        void UpdateLook(bool firstPerson, float dt)
        {
            Vector2 stick = VirtualController.GetThumbstick(lookHand);
            if (invertLookY)
                stick.y = -stick.y;

            if (stick.sqrMagnitude > 0.01f)
            {
                stickReleasedTime = 0f;
                lookYaw += stick.x * lookSpeed * dt;
                lookPitch += stick.y * lookSpeed * dt;
            }
            else
            {
                stickReleasedTime += dt;
                if (autoRecenter && stickReleasedTime > recenterDelay)
                {
                    float t = 1f - Mathf.Exp(-recenterSpeed * dt);
                    lookYaw = Mathf.LerpAngle(lookYaw, 0f, t);
                    lookPitch = Mathf.Lerp(lookPitch, 0f, t);
                }
            }

            if (firstPerson)
            {
                lookYaw = Mathf.Clamp(lookYaw, -headYawLimit, headYawLimit);
                lookPitch = Mathf.Clamp(lookPitch, headPitchRange.x, headPitchRange.y);
            }
            else
            {
                lookYaw = Mathf.Repeat(lookYaw + 180f, 360f) - 180f;
                // Looking up means the camera drops lower behind the car.
                lookPitch = Mathf.Clamp(lookPitch, -orbitPitchRange.y, -orbitPitchRange.x);
            }
        }

        void UpdateFirstPerson(bool vr)
        {
            cam.nearClipPlane = firstPersonNearClip;
            if (vr)
            {
                // The eye's own downward tilt suits a flat screen; in VR it would tilt the horizon, so follow the car body.
                SetHeadAnchor(driverEye.position, target.rotation);
                return;
            }
            cam.fieldOfView = firstPersonFov;
            transform.SetPositionAndRotation(driverEye.position, driverEye.rotation * Quaternion.Euler(-lookPitch, lookYaw, 0f));
        }

        void UpdateThirdPerson(bool vr, float dt)
        {
            cam.nearClipPlane = thirdPersonNearClip;

            currentYaw = Mathf.LerpAngle(currentYaw, target.eulerAngles.y, 1f - Mathf.Exp(-rotationDamping * dt));
            currentHeight = Mathf.Lerp(currentHeight, target.position.y, 1f - Mathf.Exp(-heightDamping * dt));

            Vector3 pivot = new Vector3(target.position.x, currentHeight, target.position.z);
            Quaternion orbit = Quaternion.Euler(-lookPitch, currentYaw + lookYaw, 0f);
            Vector3 position = pivot + orbit * new Vector3(0f, height, -distance);

            if (vr)
            {
                SetHeadAnchor(position, Quaternion.Euler(0f, currentYaw, 0f));
                return;
            }
            cam.fieldOfView = thirdPersonFov;
            transform.position = position;
            transform.LookAt(target.TransformPoint(lookOffset));
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
