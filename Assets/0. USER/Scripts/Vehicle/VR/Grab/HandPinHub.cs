using System.Collections.Generic;
using FullThrottleSun.Controller;
using UnityEngine;

namespace FullThrottleSun.Vehicle
{
    /// <summary>
    /// Shared registry of hand pin providers (steering wheel, light stalk, <see cref="HandGrabbable"/>, …).
    /// <see cref="VRHand"/> asks here each frame so new grab gameplay does not edit VRHand itself.
    /// </summary>
    public static class HandPinHub
    {
        static readonly List<IHandPinProvider> providers = new List<IHandPinProvider>(8);

        public static void Register(IHandPinProvider provider)
        {
            if (provider == null || providers.Contains(provider))
                return;
            providers.Add(provider);
        }

        public static void Unregister(IHandPinProvider provider)
        {
            if (provider == null)
                return;
            providers.Remove(provider);
        }

        public static bool TryGetPinnedPose(ControllerHand hand, out Vector3 position, out Quaternion rotation)
        {
            for (int i = 0; i < providers.Count; i++)
            {
                var p = providers[i];
                if (p != null && p.TryGetPinnedPose(hand, out position, out rotation))
                    return true;
            }
            position = default;
            rotation = Quaternion.identity;
            return false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => providers.Clear();
    }
}
