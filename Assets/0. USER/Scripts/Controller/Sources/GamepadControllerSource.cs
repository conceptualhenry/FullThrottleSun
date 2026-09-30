using UnityEngine.InputSystem;

namespace FullThrottleSun.Controller
{
    /// <summary>
    /// Reads any Input System gamepad (DualSense, DualShock, Xbox) through VirtualControllerActions.
    /// </summary>
    public class GamepadControllerSource : IControllerSource
    {
        readonly InputActionAsset actions;
        readonly HandActions[] hands;

        public GamepadControllerSource(InputActionAsset actions)
        {
            this.actions = actions;
            hands = new[]
            {
                new HandActions(actions.FindActionMap("LeftHand", true)),
                new HandActions(actions.FindActionMap("RightHand", true))
            };
        }

        public string DeviceName => Gamepad.current != null ? Gamepad.current.displayName : string.Empty;
        public bool IsAvailable => Gamepad.current != null;

        public void Enable() => actions.Enable();
        public void Disable() => actions.Disable();

        public VirtualControllerState GetState(ControllerHand hand) => hands[(int)hand].Read();

        class HandActions
        {
            readonly InputAction trigger;
            readonly InputAction grip;
            readonly InputAction thumbstick;
            readonly InputAction thumbstickClick;
            readonly InputAction primary;
            readonly InputAction secondary;
            readonly InputAction menu;
            readonly InputAction dpadUp;
            readonly InputAction dpadDown;
            readonly InputAction dpadLeft;
            readonly InputAction dpadRight;

            public HandActions(InputActionMap map)
            {
                trigger = map.FindAction("Trigger", true);
                grip = map.FindAction("Grip", true);
                thumbstick = map.FindAction("Thumbstick", true);
                thumbstickClick = map.FindAction("ThumbstickClick", true);
                primary = map.FindAction("Primary", true);
                secondary = map.FindAction("Secondary", true);
                menu = map.FindAction("Menu");
                dpadUp = map.FindAction("DpadUp");
                dpadDown = map.FindAction("DpadDown");
                dpadLeft = map.FindAction("DpadLeft");
                dpadRight = map.FindAction("DpadRight");
            }

            public VirtualControllerState Read()
            {
                return new VirtualControllerState
                {
                    trigger = trigger.ReadValue<float>(),
                    grip = grip.ReadValue<float>(),
                    thumbstick = thumbstick.ReadValue<UnityEngine.Vector2>(),
                    thumbstickClick = thumbstickClick.IsPressed(),
                    primary = primary.IsPressed(),
                    secondary = secondary.IsPressed(),
                    menu = IsPressed(menu),
                    dpadUp = IsPressed(dpadUp),
                    dpadDown = IsPressed(dpadDown),
                    dpadLeft = IsPressed(dpadLeft),
                    dpadRight = IsPressed(dpadRight)
                };
            }

            static bool IsPressed(InputAction action) => action != null && action.IsPressed();
        }
    }
}
