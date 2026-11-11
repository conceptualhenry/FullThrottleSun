using UnityEngine;

namespace FullThrottleSun.Vehicle
{
    /// <summary>
    /// Same overlap test the cockpit uses: pose the hand sphere explicitly and
    /// <see cref="Physics.ComputePenetration"/> against target colliders (works with triggers).
    /// </summary>
    public static class GrabOverlap
    {
        public static bool Touches(
            SphereCollider handCollider,
            Vector3 handPosition,
            Quaternion handRotation,
            Collider target)
        {
            if (handCollider == null || target == null || !target.enabled || !target.gameObject.activeInHierarchy)
                return false;
            return Physics.ComputePenetration(
                handCollider, handPosition, handRotation,
                target, target.transform.position, target.transform.rotation,
                out _, out _);
        }

        public static bool TouchesAny(
            SphereCollider handCollider,
            Vector3 handPosition,
            Quaternion handRotation,
            Collider[] targets)
        {
            if (targets == null)
                return false;
            for (int i = 0; i < targets.Length; i++)
            {
                if (Touches(handCollider, handPosition, handRotation, targets[i]))
                    return true;
            }
            return false;
        }

        public static float DistanceToCollider(Collider target, Vector3 worldPosition)
        {
            if (target == null)
                return float.MaxValue;
            return Vector3.Distance(target.ClosestPoint(worldPosition), worldPosition);
        }
    }
}
