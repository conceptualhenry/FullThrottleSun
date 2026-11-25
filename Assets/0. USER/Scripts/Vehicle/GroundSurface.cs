using UnityEngine;

namespace FullThrottleSun.Vehicle
{
    /// <summary>
    /// Marks a collider as a <b>drive surface</b> for the tyre-ray car.
    /// <para>
    /// Architecture: the car is held up by suspension raycasts, not by the chassis
    /// bumping into the road. Any collider with this component is ignored by the car
    /// body (when <see cref="supportCarBody"/> is true) but is still hit by tyre rays.
    /// </para>
    /// <para>
    /// To add a new interactive road: put a Collider on the mesh and add this component.
    /// Tune <see cref="gripMultiplier"/> / <see cref="rollingResistanceMultiplier"/> as needed.
    /// Do <b>not</b> put this on scenery the car should bump into (walls, props, curbs you want solid).
    /// </para>
    /// </summary>
    public class GroundSurface : MonoBehaviour
    {
        [Tooltip("Multiplies the tyre friction coefficients. Dry asphalt ≈ 1, wet ≈ 0.7, grass ≈ 0.5, snow ≈ 0.3, ice ≈ 0.1.")]
        [Min(0f)] public float gripMultiplier = 1f;

        [Tooltip("Multiplies the tyre rolling resistance. Asphalt = 1, grass / sand is much higher.")]
        [Min(0f)] public float rollingResistanceMultiplier = 1f;

        [Tooltip(
            "When true, TireFrictionCar ignores body↔this collider contact so only tyre rays support the car. " +
            "Turn off for props that should physically block the chassis.")]
        public bool supportCarBody = true;
    }
}
