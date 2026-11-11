using System.Collections.Generic;
using FullThrottleSun.Controller;
using UnityEngine;

namespace FullThrottleSun.Vehicle
{
    /// <summary>
    /// Central grip-down arbitration for free props (<see cref="HandGrabbable"/>) vs cockpit grabs
    /// (wheel / stalks / shift). On Grip press, the touching target closest to the hand centre wins.
    /// </summary>
    public static class VrGrabBroker
    {
        static readonly List<HandGrabbable> freeGrabs = new List<HandGrabbable>(8);
        static int resolvedFrame = -1;
        static readonly HandGrabbable[] freeWinner = new HandGrabbable[2];

        public static void Register(HandGrabbable grab)
        {
            if (grab != null && !freeGrabs.Contains(grab))
                freeGrabs.Add(grab);
        }

        public static void Unregister(HandGrabbable grab)
        {
            if (grab == null)
                return;
            freeGrabs.Remove(grab);
            for (int i = 0; i < freeWinner.Length; i++)
            {
                if (freeWinner[i] == grab)
                    freeWinner[i] = null;
            }
        }

        /// <summary>
        /// Ensures this frame's free-grab winners are resolved once, then returns whether
        /// <paramref name="grab"/> should begin a grab on <paramref name="hand"/>.
        /// </summary>
        public static bool ShouldFreeGrabBegin(HandGrabbable grab, ControllerHand hand)
        {
            ResolveFrame();
            return freeWinner[(int)hand] == grab;
        }

        /// <summary>
        /// True when some free prop is touching and closer to the hand than <paramref name="cockpitDistance"/>.
        /// Cockpit systems call this on Grip Down to yield.
        /// </summary>
        public static bool PreferFreeOverCockpit(
            ControllerHand hand,
            Vector3 handPosition,
            Quaternion handRotation,
            SphereCollider handCollider,
            float cockpitDistance)
        {
            float freeDist = NearestTouchingFreeDistance(hand, handPosition, handRotation, handCollider);
            return freeDist + 0.01f < cockpitDistance;
        }

        public static float NearestTouchingFreeDistance(
            ControllerHand hand,
            Vector3 handPosition,
            Quaternion handRotation,
            SphereCollider handCollider)
        {
            float best = float.MaxValue;
            for (int i = 0; i < freeGrabs.Count; i++)
            {
                var g = freeGrabs[i];
                if (g == null || !g.IsEligibleToGrab(hand))
                    continue;
                if (!g.TouchesHand(handPosition, handRotation, handCollider))
                    continue;
                float d = g.DistanceToHand(handPosition);
                if (d < best)
                    best = d;
            }
            return best;
        }

        static void ResolveFrame()
        {
            if (resolvedFrame == Time.frameCount)
                return;
            resolvedFrame = Time.frameCount;
            freeWinner[0] = null;
            freeWinner[1] = null;
            ResolveHand(ControllerHand.Left);
            ResolveHand(ControllerHand.Right);
        }

        static void ResolveHand(ControllerHand hand)
        {
            if (!VrGrip.IsDown(hand))
                return;
            if (!VrHandClaim.IsFree(hand))
                return;
            if (!VrGrip.TryGetHand(hand, out VRHand vrHand) || vrHand.GrabCollider == null)
                return;
            if (!VrGrip.TryGetWorldPose(hand, out Vector3 pos, out Quaternion rot))
                return;

            HandGrabbable best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < freeGrabs.Count; i++)
            {
                var g = freeGrabs[i];
                if (g == null || !g.IsEligibleToGrab(hand))
                    continue;
                if (!g.TouchesHand(pos, rot, vrHand.GrabCollider))
                    continue;
                float d = g.DistanceToHand(pos);
                bool closer = best == null || d < bestDist - 1e-5f;
                bool tieBreak = best != null
                               && Mathf.Abs(d - bestDist) <= 1e-5f
                               && g.Priority > best.Priority;
                if (closer || tieBreak)
                {
                    bestDist = d;
                    best = g;
                }
            }

            if (best == null)
                return;

            // Cockpit wins when it is touching and closer (or nearly tied).
            float cockpit = CockpitTouchDistance(hand, pos, rot, vrHand.GrabCollider);
            if (cockpit <= bestDist + 0.01f)
                return;

            freeWinner[(int)hand] = best;
        }

        static float CockpitTouchDistance(
            ControllerHand hand,
            Vector3 pos,
            Quaternion rot,
            SphereCollider handCol)
        {
            float best = float.MaxValue;

            var wheel = Object.FindAnyObjectByType<SteeringWheelGrab>();
            if (wheel != null)
            {
                bool holding = hand == ControllerHand.Left ? wheel.LeftHolding : wheel.RightHolding;
                if (!holding && wheel.TouchesWheelWorld(pos, rot, handCol))
                    best = Mathf.Min(best, wheel.DistanceToWheel(pos));
            }

            var light = Object.FindAnyObjectByType<LightStalk>();
            if (light != null && !light.IsHolding(hand) && light.Touches(pos, rot, handCol))
                best = Mathf.Min(best, light.DistanceTo(pos));

            var wiper = Object.FindAnyObjectByType<WiperStalk>();
            if (wiper != null && !wiper.IsHolding(hand) && wiper.Touches(pos, rot, handCol))
                best = Mathf.Min(best, wiper.DistanceTo(pos));

            var shift = Object.FindAnyObjectByType<ShiftLever>();
            if (shift != null && !shift.IsHolding(hand) && shift.Touches(pos, rot, handCol))
                best = Mathf.Min(best, shift.DistanceTo(pos));

            return best;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            freeGrabs.Clear();
            resolvedFrame = -1;
            freeWinner[0] = null;
            freeWinner[1] = null;
        }
    }
}
