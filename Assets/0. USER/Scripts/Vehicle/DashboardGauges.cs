using UnityEngine;

namespace FullThrottleSun.Vehicle
{
    /// <summary>
    /// Placeholder speedo + tacho needles. Speed reads <see cref="TireFrictionCar.SpeedKmh"/>.
    /// RPM is faked from gear + throttle + speed (linked to R/N/D).
    /// </summary>
    public class DashboardGauges : MonoBehaviour
    {
        [SerializeField] TireFrictionCar car;
        [SerializeField] Transform speedNeedle;
        [SerializeField] Transform rpmNeedle;

        [Header("Speedo")]
        [SerializeField] float maxSpeedKmh = 160f;
        [SerializeField] float speedNeedleMinAngle = 120f;
        [SerializeField] float speedNeedleMaxAngle = -120f;

        [Header("Tacho (fake)")]
        [SerializeField] float idleRpm = 800f;
        [SerializeField] float redlineRpm = 7000f;
        [SerializeField] float rpmNeedleMinAngle = 120f;
        [SerializeField] float rpmNeedleMaxAngle = -120f;
        [Tooltip("How fast needles ease toward the target.")]
        [SerializeField] float needleSmooth = 10f;

        float shownSpeed;
        float shownRpm;

        void Awake()
        {
            if (car == null)
                car = GetComponentInParent<TireFrictionCar>()
                      ?? FindAnyObjectByType<TireFrictionCar>(FindObjectsInactive.Include);
        }

        void LateUpdate()
        {
            if (car == null)
                return;

            float targetSpeed = Mathf.Abs(car.SpeedKmh);
            float targetRpm = FakeRpm(car);

            float k = 1f - Mathf.Exp(-needleSmooth * Time.deltaTime);
            shownSpeed = Mathf.Lerp(shownSpeed, targetSpeed, k);
            shownRpm = Mathf.Lerp(shownRpm, targetRpm, k);

            SetNeedle(speedNeedle, shownSpeed / Mathf.Max(1f, maxSpeedKmh), speedNeedleMinAngle, speedNeedleMaxAngle);
            SetNeedle(rpmNeedle, (shownRpm - idleRpm) / Mathf.Max(1f, redlineRpm - idleRpm), rpmNeedleMinAngle, rpmNeedleMaxAngle);
        }

        float FakeRpm(TireFrictionCar c)
        {
            float throttle = Mathf.Clamp01(c.ThrottleInput);
            float speed01 = Mathf.Clamp01(Mathf.Abs(c.SpeedKmh) / Mathf.Max(1f, maxSpeedKmh));

            switch (c.Gear)
            {
                case TireFrictionCar.AutoGear.Neutral:
                    // N: revs follow the pedal, barely care about road speed.
                    return Mathf.Lerp(idleRpm, redlineRpm, Mathf.Pow(throttle, 0.85f));

                case TireFrictionCar.AutoGear.Reverse:
                    // R: lower ceiling, climbs with throttle + reverse speed.
                    return Mathf.Lerp(idleRpm, redlineRpm * 0.75f, Mathf.Clamp01(speed01 * 1.4f * 0.55f + throttle * 0.55f));

                case TireFrictionCar.AutoGear.Drive:
                default:
                    // D: mostly road speed, throttle lifts it under load / pull-away.
                    float pull = Mathf.Clamp01(throttle * (1.15f - speed01));
                    float cruise = speed01 * 0.85f + pull * 0.5f;
                    // Coasting with no throttle settles nearer idle as speed drops.
                    if (throttle < 0.05f)
                        cruise *= 0.65f;
                    return Mathf.Lerp(idleRpm, redlineRpm, Mathf.Clamp01(cruise));
            }
        }

        static void SetNeedle(Transform needle, float t01, float minAngle, float maxAngle)
        {
            if (needle == null)
                return;
            float z = Mathf.Lerp(minAngle, maxAngle, Mathf.Clamp01(t01));
            Vector3 e = needle.localEulerAngles;
            needle.localEulerAngles = new Vector3(e.x, e.y, z);
        }
    }
}
