using FullThrottleSun.Controller;
using UnityEngine;

namespace FullThrottleSun.Vehicle
{
    /// <summary>
    /// Thin helpers for grip-button + hand lookup. Prefer this from custom grab gameplay
    /// instead of reaching into <see cref="SteeringWheelGrab"/> internals.
    /// </summary>
    public static class VrGrip
    {
        public static bool IsDown(ControllerHand hand)
            => VirtualController.GetButtonDown(ControllerButton.Grip, hand);

        public static bool IsHeld(ControllerHand hand)
            => VirtualController.GetButton(ControllerButton.Grip, hand);

        public static bool IsUp(ControllerHand hand)
            => VirtualController.GetButtonUp(ControllerButton.Grip, hand);

        public static float Analog(ControllerHand hand)
            => VirtualController.GetGrip(hand);

        public static bool TryGetHand(ControllerHand side, out VRHand hand)
        {
            var all = Object.FindObjectsByType<VRHand>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].Side == side)
                {
                    hand = all[i];
                    return true;
                }
            }
            hand = null;
            return false;
        }

        public static bool TryGetWorldPose(ControllerHand side, out Vector3 position, out Quaternion rotation)
        {
            if (TryGetHand(side, out VRHand hand) && hand.PoseSource != null)
                return hand.PoseSource.TryGetTrackedPose(side, out position, out rotation);
            position = default;
            rotation = Quaternion.identity;
            return false;
        }
    }
}
