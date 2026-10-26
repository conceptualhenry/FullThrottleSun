using UnityEngine;
using UnityEngine.InputSystem;

namespace FullThrottleSun.Controller
{
    /// <summary>
    /// Feeds remappable keyboard keys into the same left/right virtual pads the rest of the game reads.
    /// Steer is applied separately in the vehicle (A/D) so it does not fight camera look on the right stick.
    /// </summary>
    public class KeyboardControllerSource : IControllerSource
    {
        public ControllerKind Kind => ControllerKind.Keyboard;
        public string DeviceName => "Keyboard";
        public bool IsAvailable => Keyboard.current != null;

        public void Enable() { }
        public void Disable() { }

        public VirtualControllerState GetState(ControllerHand hand)
        {
            if (Keyboard.current == null || KeyboardBindings.IsListening || SettingsMenu.IsOpen)
                return default;

            // Steer (A/D) is consumed by the vehicle directly; still mark activity so this source can become active.
            bool active = KeyboardBindings.AnyBoundKeyPressed();

            if (hand == ControllerHand.Left)
            {
                float lightX = KeyboardBindings.GetAxis(KeyboardAction.LightLeft, KeyboardAction.LightRight);
                float lightY = KeyboardBindings.GetAxis(KeyboardAction.LightDown, KeyboardAction.LightUp);
                return new VirtualControllerState
                {
                    activity = active,
                    trigger = KeyboardBindings.IsPressed(KeyboardAction.Brake) ? 1f : 0f,
                    grip = KeyboardBindings.IsPressed(KeyboardAction.GripLeft) ? 1f : 0f,
                    thumbstick = new Vector2(lightX, lightY),
                    menu = KeyboardBindings.IsPressed(KeyboardAction.Menu)
                };
            }

            float lookX = KeyboardBindings.GetAxis(KeyboardAction.LookLeft, KeyboardAction.LookRight);
            float lookY = KeyboardBindings.GetAxis(KeyboardAction.LookDown, KeyboardAction.LookUp);
            return new VirtualControllerState
            {
                activity = active,
                trigger = KeyboardBindings.IsPressed(KeyboardAction.Throttle) ? 1f : 0f,
                grip = KeyboardBindings.IsPressed(KeyboardAction.GripRight) ? 1f : 0f,
                thumbstick = new Vector2(lookX, lookY),
                thumbstickClick = KeyboardBindings.IsPressed(KeyboardAction.CameraSwitch),
                primary = KeyboardBindings.IsPressed(KeyboardAction.Handbrake),
                secondary = KeyboardBindings.IsPressed(KeyboardAction.Secondary)
            };
        }
    }
}
