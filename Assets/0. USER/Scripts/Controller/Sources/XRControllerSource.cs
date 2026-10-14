using UnityEngine;
using UnityEngine.InputSystem;

namespace FullThrottleSun.Controller
{
    /// <summary>
    /// Reads OpenXR controllers (Meta Quest Touch) into the same virtual layout as a gamepad: index trigger, grip
    /// (side trigger), thumbstick, A/X primary, B/Y secondary, left ☰ menu. Also reports each controller's pose.
    /// </summary>
    public class XRControllerSource : IControllerSource
    {
        readonly InputActionMap map = new InputActionMap("XRControllers");
        readonly HandActions[] hands;

        public XRControllerSource()
        {
            hands = new[]
            {
                new HandActions(map, "LeftHand", withMenu: true),
                // The right controller's menu button is reserved for the Meta system menu.
                new HandActions(map, "RightHand", withMenu: false)
            };
        }

        public ControllerKind Kind
        {
            get
            {
                var device = hands[1].Device ?? hands[0].Device;
                if (device == null) return ControllerKind.None;
                return IsMeta(device) ? ControllerKind.MetaQuest : ControllerKind.XR;
            }
        }

        public string DeviceName
        {
            get
            {
                var device = hands[1].Device ?? hands[0].Device;
                return device != null ? device.displayName : string.Empty;
            }
        }

        public bool IsAvailable => hands[0].Device != null || hands[1].Device != null;

        public void Enable() => map.Enable();
        public void Disable() => map.Disable();

        public VirtualControllerState GetState(ControllerHand hand) => hands[(int)hand].Read();

        static bool IsMeta(InputDevice device)
        {
            string text = device.layout + " " + device.description.manufacturer + " " + device.description.product;
            return text.Contains("Oculus") || text.Contains("Meta") || text.Contains("Quest");
        }

        class HandActions
        {
            readonly InputAction trigger;
            readonly InputAction grip;
            readonly InputAction thumbstick;
            readonly InputAction thumbstickClick;
            readonly InputAction primary;
            readonly InputAction secondary;
            readonly InputAction menu;
            readonly InputAction tracked;
            readonly InputAction position;
            readonly InputAction rotation;

            public HandActions(InputActionMap map, string usage, bool withMenu)
            {
                string prefix = "<XRController>{" + usage + "}/";
                InputAction Add(string control, InputActionType type) =>
                    map.AddAction(usage + "_" + control, type, prefix + control);

                trigger = Add("trigger", InputActionType.Value);
                grip = Add("grip", InputActionType.Value);
                thumbstick = Add("thumbstick", InputActionType.Value);
                thumbstickClick = Add("thumbstickClicked", InputActionType.Button);
                primary = Add("primaryButton", InputActionType.Button);
                secondary = Add("secondaryButton", InputActionType.Button);
                menu = withMenu ? Add("menu", InputActionType.Button) : null;
                tracked = Add("isTracked", InputActionType.Value);
                position = Add("devicePosition", InputActionType.Value);
                rotation = Add("deviceRotation", InputActionType.Value);
            }

            public InputDevice Device => trigger.controls.Count > 0 ? trigger.controls[0].device : null;

            public VirtualControllerState Read()
            {
                bool isTracked = tracked.ReadValue<float>() > 0.5f && rotation.controls.Count > 0;
                return new VirtualControllerState
                {
                    trigger = trigger.ReadValue<float>(),
                    grip = grip.ReadValue<float>(),
                    thumbstick = thumbstick.ReadValue<Vector2>(),
                    thumbstickClick = thumbstickClick.IsPressed(),
                    primary = primary.IsPressed(),
                    secondary = secondary.IsPressed(),
                    menu = menu != null && menu.IsPressed(),
                    isTracked = isTracked,
                    position = isTracked ? position.ReadValue<Vector3>() : Vector3.zero,
                    rotation = isTracked ? rotation.ReadValue<Quaternion>() : Quaternion.identity
                };
            }
        }
    }
}
