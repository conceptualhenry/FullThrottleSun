using UnityEngine;

namespace FullThrottleSun.Controller
{
    /// <summary>
    /// Single entry point for reading VR-controller style input, regardless of the physical device.
    /// </summary>
    public static class VirtualController
    {
        static IControllerSource source;
        static readonly VirtualControllerState[] current = new VirtualControllerState[2];
        static readonly VirtualControllerState[] previous = new VirtualControllerState[2];

        public static bool IsConnected => source != null && source.IsAvailable;
        public static string DeviceName => source != null ? source.DeviceName : string.Empty;

        public static VirtualControllerState GetState(ControllerHand hand) => current[(int)hand];

        public static float GetTrigger(ControllerHand hand) => current[(int)hand].trigger;
        public static float GetGrip(ControllerHand hand) => current[(int)hand].grip;
        public static Vector2 GetThumbstick(ControllerHand hand) => current[(int)hand].thumbstick;

        public static bool GetButton(ControllerButton button, ControllerHand hand)
            => current[(int)hand].GetButton(button);

        public static bool GetButtonDown(ControllerButton button, ControllerHand hand)
            => current[(int)hand].GetButton(button) && !previous[(int)hand].GetButton(button);

        public static bool GetButtonUp(ControllerButton button, ControllerHand hand)
            => !current[(int)hand].GetButton(button) && previous[(int)hand].GetButton(button);

        internal static void SetSource(IControllerSource newSource)
        {
            source = newSource;
            ClearStates();
        }

        internal static void Tick()
        {
            previous[0] = current[0];
            previous[1] = current[1];

            if (!IsConnected)
            {
                current[0] = default;
                current[1] = default;
                return;
            }

            current[0] = source.GetState(ControllerHand.Left);
            current[1] = source.GetState(ControllerHand.Right);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            source = null;
            ClearStates();
        }

        static void ClearStates()
        {
            current[0] = current[1] = default;
            previous[0] = previous[1] = default;
        }
    }
}
