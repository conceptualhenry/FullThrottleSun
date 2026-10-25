using System;
using UnityEngine;

namespace FullThrottleSun.Controller
{
    /// <summary>
    /// Single entry point for reading VR-controller style input, regardless of the physical device.
    /// Several devices can be connected at once (a gamepad and Quest controllers); the last one used drives it.
    /// </summary>
    public static class VirtualController
    {
        static IControllerSource[] sources = Array.Empty<IControllerSource>();
        static IControllerSource active;
        static ControllerKind activeKind;
        static readonly VirtualControllerState[] current = new VirtualControllerState[2];
        static readonly VirtualControllerState[] previous = new VirtualControllerState[2];

        public static bool IsConnected => active != null && active.IsAvailable;
        public static string DeviceName => IsConnected ? active.DeviceName : string.Empty;

        /// <summary>Kind of device currently driving the virtual controller.</summary>
        public static ControllerKind ActiveKind => activeKind;
        public static bool IsTrackedController => activeKind == ControllerKind.MetaQuest || activeKind == ControllerKind.XR;
        public static event Action<ControllerKind> ActiveKindChanged;

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

        internal static void SetSources(params IControllerSource[] newSources)
        {
            sources = newSources ?? Array.Empty<IControllerSource>();
            active = null;
            ClearStates();
            UpdateKind();
        }

        internal static void Tick()
        {
            previous[0] = current[0];
            previous[1] = current[1];

            var next = active != null && active.IsAvailable ? active : null;
            var left = next != null ? next.GetState(ControllerHand.Left) : default;
            var right = next != null ? next.GetState(ControllerHand.Right) : default;

            // Only hand over while the current device is idle, so two devices in use at once don't flicker.
            if (next == null || !(left.HasInput || right.HasInput))
            {
                foreach (var source in sources)
                {
                    if (source == next || !source.IsAvailable)
                        continue;
                    var sourceLeft = source.GetState(ControllerHand.Left);
                    var sourceRight = source.GetState(ControllerHand.Right);
                    bool used = sourceLeft.HasInput || sourceRight.HasInput;
                    if (!used && next != null)
                        continue;
                    next = source;
                    left = sourceLeft;
                    right = sourceRight;
                    if (used)
                        break;
                }
            }

            if (next != active)
            {
                active = next;
                // Cleared so the press that switched devices also counts as a button down.
                previous[0] = previous[1] = default;
            }

            current[0] = left;
            current[1] = right;
            UpdateKind();
        }

        static void UpdateKind()
        {
            var kind = IsConnected ? active.Kind : ControllerKind.None;
            if (kind == activeKind)
                return;
            activeKind = kind;
            ActiveKindChanged?.Invoke(kind);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            sources = Array.Empty<IControllerSource>();
            active = null;
            activeKind = ControllerKind.None;
            ActiveKindChanged = null;
            ClearStates();
        }

        static void ClearStates()
        {
            current[0] = current[1] = default;
            previous[0] = previous[1] = default;
        }
    }
}
