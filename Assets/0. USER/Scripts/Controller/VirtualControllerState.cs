using UnityEngine;

namespace FullThrottleSun.Controller
{
    public enum ControllerHand
    {
        Left = 0,
        Right = 1
    }

    public enum ControllerKind
    {
        None,
        PlayStation,
        Xbox,
        /// <summary>Any other gamepad.</summary>
        Gamepad,
        Keyboard,
        MetaQuest,
        /// <summary>Any other OpenXR controller.</summary>
        XR
    }

    public enum ControllerButton
    {
        /// <summary>A on the right hand, X on the left hand.</summary>
        Primary,
        /// <summary>B on the right hand, Y on the left hand.</summary>
        Secondary,
        ThumbstickClick,
        /// <summary>Left hand only.</summary>
        Menu,
        /// <summary>Trigger value above PressThreshold.</summary>
        Trigger,
        /// <summary>Grip value above PressThreshold.</summary>
        Grip,
        /// <summary>D-pad buttons are left hand only.</summary>
        DpadUp,
        DpadDown,
        DpadLeft,
        DpadRight
    }

    public struct VirtualControllerState
    {
        public const float PressThreshold = 0.5f;

        public float trigger;
        public float grip;
        public Vector2 thumbstick;
        public bool primary;
        public bool secondary;
        public bool thumbstickClick;
        public bool menu;
        public bool dpadUp;
        public bool dpadDown;
        public bool dpadLeft;
        public bool dpadRight;

        /// <summary>Tracked controllers only. Pose in the headset's tracking space, valid while isTracked.</summary>
        public bool isTracked;
        public Vector3 position;
        public Quaternion rotation;

        /// <summary>
        /// Extra activity flag for devices whose inputs are not fully represented in this struct
        /// (e.g. keyboard steer A/D). Pose tracking alone must not set this.
        /// </summary>
        public bool activity;

        /// <summary>True when the player is actively using this controller (pose changes don't count).</summary>
        public bool HasInput =>
            activity
            || trigger > 0.2f || grip > PressThreshold || thumbstick.sqrMagnitude > 0.09f
            || primary || secondary || thumbstickClick || menu || dpadUp || dpadDown || dpadLeft || dpadRight;

        public bool GetButton(ControllerButton button)
        {
            switch (button)
            {
                case ControllerButton.Primary: return primary;
                case ControllerButton.Secondary: return secondary;
                case ControllerButton.ThumbstickClick: return thumbstickClick;
                case ControllerButton.Menu: return menu;
                case ControllerButton.Trigger: return trigger >= PressThreshold;
                case ControllerButton.Grip: return grip >= PressThreshold;
                case ControllerButton.DpadUp: return dpadUp;
                case ControllerButton.DpadDown: return dpadDown;
                case ControllerButton.DpadLeft: return dpadLeft;
                case ControllerButton.DpadRight: return dpadRight;
                default: return false;
            }
        }
    }
}
