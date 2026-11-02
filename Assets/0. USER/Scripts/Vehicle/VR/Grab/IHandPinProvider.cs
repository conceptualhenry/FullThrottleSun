using FullThrottleSun.Controller;
using UnityEngine;

namespace FullThrottleSun.Vehicle
{
    /// <summary>
    /// While a hand is "pinned" (holding a wheel, stalk, prop, …), report the world pose the
    /// visible <see cref="VRHand"/> should snap to. Register with <see cref="HandPinHub"/>.
    /// </summary>
    public interface IHandPinProvider
    {
        bool TryGetPinnedPose(ControllerHand hand, out Vector3 position, out Quaternion rotation);
    }
}
