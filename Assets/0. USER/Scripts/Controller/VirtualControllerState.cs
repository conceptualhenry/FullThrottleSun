using UnityEngine;

namespace FullThrottleSun.Controller
{
    public enum ControllerHand
    {
        Left = 0,
        Right = 1
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
