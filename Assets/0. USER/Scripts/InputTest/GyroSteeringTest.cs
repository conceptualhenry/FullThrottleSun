using FullThrottleSun.Controller;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FullThrottleSun.InputTest
{
    /// <summary>
    /// Put on the steering wheel. Press Options on the controller or Space (hold the DualSense still) to bind:
    /// this object then turns with the gyro's integrated Y angle, 1:1. Press again to recenter.
    /// Space only works while Unity has keyboard focus; the Meta XR Simulator window takes focus and uses Space itself.
    /// </summary>
    public class GyroSteeringTest : MonoBehaviour
    {
        public enum Axis { X, Y, Z }

        [Tooltip("Local axis this object spins around.")]
        [SerializeField] Axis rotationAxis = Axis.Z;
        [SerializeField] bool invert;
        [Tooltip("Degrees of rotation per gyro degree. 1 = turning the controller 360° turns this object 360°.")]
        [SerializeField] float ratio = 1f;

        [Header("Bind / recenter")]
        [SerializeField] ControllerButton bindButton = ControllerButton.Menu;
        [SerializeField] ControllerHand bindHand = ControllerHand.Left;

        [Header("Debug (read only)")]
        [SerializeField] bool bound;
        [SerializeField] float currentAngle;

        Quaternion restRotation;
        bool waitingForCalibration;

        void Start() => restRotation = transform.localRotation;

        void Update()
        {
            bool spacePressed = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
            if (spacePressed || VirtualController.GetButtonDown(bindButton, bindHand))
                Bind();

            if (!bound)
                return;

            if (DualSenseMotion.IsCalibrating)
            {
                waitingForCalibration = true;
                return;
            }

            if (waitingForCalibration)
            {
                waitingForCalibration = false;
                Debug.Log($"[GyroSteeringTest] Bound. Gyro bias = {DualSenseMotion.Current.gyroBias:F2} °/s", this);
            }

            currentAngle = DualSenseMotion.IntegratedAngle.y * ratio * (invert ? -1f : 1f);
            transform.localRotation = restRotation * Quaternion.AngleAxis(currentAngle, AxisVector(rotationAxis));
        }

        void Bind()
        {
            if (!DualSenseMotion.IsAvailable)
            {
                Debug.LogWarning($"[GyroSteeringTest] No gyro data: {DualSenseMotion.Current.status}", this);
                return;
            }

            DualSenseMotion.Calibrate();
            bound = true;
            waitingForCalibration = true;
            currentAngle = 0f;
            transform.localRotation = restRotation;
            Debug.Log("[GyroSteeringTest] Calibrating, keep the controller still...", this);
        }

        static Vector3 AxisVector(Axis axis)
        {
            switch (axis)
            {
                case Axis.X: return Vector3.right;
                case Axis.Y: return Vector3.up;
                default: return Vector3.forward;
            }
        }
    }
}
