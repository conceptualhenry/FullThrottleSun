using FullThrottleSun.Controller;
using UnityEngine;

namespace FullThrottleSun.Vehicle
{
    /// <summary>
    /// One owner per hand while a grip grab is active. Prevents the wheel, light stalk, and
    /// custom <see cref="HandGrabbable"/> props from fighting over the same Grip press.
    /// </summary>
    public static class VrHandClaim
    {
        static readonly object[] owners = new object[2];

        public static bool IsFree(ControllerHand hand) => owners[(int)hand] == null;

        public static bool IsOwnedBy(ControllerHand hand, object owner)
            => owner != null && owners[(int)hand] == owner;

        public static object GetOwner(ControllerHand hand) => owners[(int)hand];

        /// <summary>Returns false if another system already owns this hand.</summary>
        public static bool TryClaim(ControllerHand hand, object owner)
        {
            if (owner == null)
                return false;
            int i = (int)hand;
            if (owners[i] != null && owners[i] != owner)
                return false;
            owners[i] = owner;
            return true;
        }

        public static void Release(ControllerHand hand, object owner)
        {
            int i = (int)hand;
            if (owners[i] == owner)
                owners[i] = null;
        }

        public static void ReleaseAll(object owner)
        {
            for (int i = 0; i < owners.Length; i++)
            {
                if (owners[i] == owner)
                    owners[i] = null;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            owners[0] = null;
            owners[1] = null;
        }
    }
}
