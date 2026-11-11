using FullThrottleSun.Controller;
using UnityEngine;

namespace FullThrottleSun.Vehicle
{
    /// <summary>
    /// Supplies each controller's tracked pose in world space (usually car-mapped via CarFollowCamera).
    /// Implemented by <see cref="SteeringWheelGrab"/>; custom grab systems can also implement this.
    /// </summary>
    public interface IVrHandPoseSource
    {
        /// <summary>Where the real controller is right now (world space).</summary>
        bool TryGetTrackedPose(ControllerHand hand, out Vector3 position, out Quaternion rotation);
    }
}
