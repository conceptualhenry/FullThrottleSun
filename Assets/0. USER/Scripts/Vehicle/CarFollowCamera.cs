using FullThrottleSun.Controller;
using UnityEngine;

namespace FullThrottleSun.Vehicle
{
    /// <summary>
    /// Car camera with a third person chase view and a first person driver view, toggled by a controller button
    /// (right stick click by default). The right stick looks around: it orbits the car in third person and turns
    /// the head in first person. Once released, the view eases back to straight ahead.
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
        [Range(20f, 100f)] [SerializeField] float thirdPersonFov = 60f;

        [Header("First person")]
        [Tooltip("Eye position and straight-ahead direction inside the car.")]
        [SerializeField] Transform driverEye;
        [Tooltip("How far (degrees) the head can turn left or right.")]
        [Range(0f, 180f)] [SerializeField] float headYawLimit = 120f;
        [Tooltip("How far (degrees) the head can look down and up.")]
        [SerializeField] Vector2 headPitchRange = new Vector2(-40f, 50f);
        [Range(20f, 100f)] [SerializeField] float firstPersonFov = 70f;
        [Tooltip("Small near clip so the dashboard right in front of the eye isn't cut off.")]
        [Min(0.01f)] [SerializeField] float firstPersonNearClip = 0.05f;

        [Header("Right stick look")]
        [Tooltip("Degrees per second at full stick.")]
        [Min(1f)] [SerializeField] float lookSpeed = 180f;
        [SerializeField] bool invertLookY;
        [SerializeField] bool autoRecenter = true;
        [Tooltip("Seconds after releasing the stick before the view eases back.")]
        [Min(0f)] [SerializeField] float recenterDelay = 0.3f;
        [Tooltip("How quickly the view eases back. Higher = faster.")]
        [Min(0.1f)] [SerializeField] float recenterSpeed = 4f;

        Camera cam;
        float thirdPersonNearClip;
        float currentYaw;
        float currentHeight;
        float lookYaw;
        float lookPitch;
        float stickReleasedTime;

        void Awake()
        {
            cam = GetComponent<Camera>();
            thirdPersonNearClip = cam.nearClipPlane;
        }

        void Start() => SnapThirdPerson();

        void LateUpdate()
        {
            if (target == null)
                return;

            if (VirtualController.GetButtonDown(switchButton, switchHand))
                Toggle();

            bool firstPerson = view == View.FirstPerson && driverEye != null;
            UpdateLook(firstPerson, Time.deltaTime);

            if (firstPerson)
                UpdateFirstPerson();
            else
                UpdateThirdPerson(Time.deltaTime);
        }

        public void Toggle()
        {
            view = view == View.ThirdPerson ? View.FirstPerson : View.ThirdPerson;
            lookYaw = 0f;
            lookPitch = 0f;
            if (view == View.ThirdPerson)
                SnapThirdPerson();
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

        void UpdateFirstPerson()
        {
            cam.fieldOfView = firstPersonFov;
            cam.nearClipPlane = firstPersonNearClip;
            transform.SetPositionAndRotation(driverEye.position, driverEye.rotation * Quaternion.Euler(-lookPitch, lookYaw, 0f));
        }

        void UpdateThirdPerson(float dt)
        {
            cam.fieldOfView = thirdPersonFov;
            cam.nearClipPlane = thirdPersonNearClip;

            currentYaw = Mathf.LerpAngle(currentYaw, target.eulerAngles.y, 1f - Mathf.Exp(-rotationDamping * dt));
            currentHeight = Mathf.Lerp(currentHeight, target.position.y, 1f - Mathf.Exp(-heightDamping * dt));

            Vector3 pivot = new Vector3(target.position.x, currentHeight, target.position.z);
            Quaternion orbit = Quaternion.Euler(-lookPitch, currentYaw + lookYaw, 0f);
            transform.position = pivot + orbit * new Vector3(0f, height, -distance);
            transform.LookAt(target.TransformPoint(lookOffset));
        }
    }
}
