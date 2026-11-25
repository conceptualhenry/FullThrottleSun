using UnityEngine;

namespace FullThrottleSun.Vehicle
{
    /// <summary>
    /// Placeholder speedo + tacho needles. Hierarchy under this object is scene-editable:
    /// drag <c>Speedometer</c> / <c>Tachometer</c> (or this root) in the Scene view.
    /// Materials live at <c>Assets/0. USER/Materials/Dashboard</c> — edit those for color.
    /// Speed reads <see cref="TireFrictionCar.SpeedKmh"/>; RPM is faked from gear + throttle + speed.
    /// </summary>
    public class DashboardGauges : MonoBehaviour
    {
        [SerializeField] TireFrictionCar car;
        [SerializeField] Transform speedNeedle;
        [SerializeField] Transform rpmNeedle;
        [SerializeField] TextMesh gearLabel;

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

        [Header("Gear readout")]
        [SerializeField] Color gearColorDrive = new Color(0.35f, 0.95f, 0.45f);
        [SerializeField] Color gearColorNeutral = new Color(0.95f, 0.9f, 0.35f);
        [SerializeField] Color gearColorReverse = new Color(0.95f, 0.35f, 0.3f);

        float shownSpeed;
        float shownRpm;
        TireFrictionCar.AutoGear shownGear = (TireFrictionCar.AutoGear)(-1);

        void Awake()
        {
            ResolveRefs();
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            ResolveRefs();
        }
#endif

        void ResolveRefs()
        {
            if (car == null)
                car = GetComponentInParent<TireFrictionCar>()
                      ?? FindAnyObjectByType<TireFrictionCar>(FindObjectsInactive.Include);

            if (speedNeedle == null)
            {
                var t = transform.Find("Speedometer/NeedlePivot");
                if (t != null) speedNeedle = t;
            }
            if (rpmNeedle == null)
            {
                var t = transform.Find("Tachometer/NeedlePivot");
                if (t != null) rpmNeedle = t;
            }
            if (gearLabel == null)
            {
                var t = transform.Find("GearLabel");
                if (t != null) gearLabel = t.GetComponent<TextMesh>();
            }
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
            UpdateGearLabel(car.Gear);
        }

        void UpdateGearLabel(TireFrictionCar.AutoGear gear)
        {
            if (gearLabel == null || gear == shownGear)
                return;
            shownGear = gear;
            switch (gear)
            {
                case TireFrictionCar.AutoGear.Reverse:
                    gearLabel.text = "R";
                    gearLabel.color = gearColorReverse;
                    break;
                case TireFrictionCar.AutoGear.Neutral:
                    gearLabel.text = "N";
                    gearLabel.color = gearColorNeutral;
                    break;
                default:
                    gearLabel.text = "D";
                    gearLabel.color = gearColorDrive;
                    break;
            }
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
