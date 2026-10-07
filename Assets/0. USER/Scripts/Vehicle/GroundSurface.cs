using UnityEngine;

namespace FullThrottleSun.Vehicle
{
    /// <summary>
    /// Optional, put on a ground collider to change how much grip tyres get on it.
    /// Ground without this component counts as gripMultiplier = 1.
    /// </summary>
    public class GroundSurface : MonoBehaviour
    {
        [Tooltip("Multiplies the tyre friction coefficients. Dry asphalt ≈ 1, wet ≈ 0.7, grass ≈ 0.5, snow ≈ 0.3, ice ≈ 0.1.")]
        [Min(0f)] public float gripMultiplier = 1f;

        [Tooltip("Multiplies the tyre rolling resistance. Asphalt = 1, grass / sand is much higher.")]
        [Min(0f)] public float rollingResistanceMultiplier = 1f;
    }
}
