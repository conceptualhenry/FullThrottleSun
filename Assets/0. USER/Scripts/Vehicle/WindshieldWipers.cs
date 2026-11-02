using FullThrottleSun.Controller;
using UnityEngine;

namespace FullThrottleSun.Vehicle
{
    /// <summary>
    /// Animates the two windshield wiper blades in the glass plane.
    /// Rotation axis = windshield outward normal (from rake angle), so the blades wipe
    /// along the window instead of flapping vertically off it.
    /// </summary>
    public class WindshieldWipers : MonoBehaviour
    {
        public enum Mode
        {
            Off = 0,
            Intermittent = 1,
            Low = 2,
            High = 3
        }

        [Header("Blades")]
        [SerializeField] Transform leftBlade;
        [SerializeField] Transform rightBlade;
        [Tooltip("How far the windshield leans back from vertical (degrees). 0 = upright glass facing +Z; ~35 is a typical sedan.")]
        [Range(0f, 80f)] [SerializeField] float windshieldRake = 40f;
        [Tooltip("If set (non-zero), used instead of the rake-derived glass normal.")]
        [SerializeField] Vector3 axisOverride;
        [Tooltip("Peak swing from the rest pose (degrees).")]
        [Range(10f, 120f)] [SerializeField] float maxAngle = 75f;
        [Tooltip("Negate swing so the blade climbs the glass instead of into the hood.")]
        [SerializeField] bool wipeUp = true;
        [Tooltip("Right blade lags the left by this fraction of a full round trip (0 = locked together).")]
        [Range(0f, 0.45f)] [SerializeField] float rightPhase = 0.1f;
        [Tooltip("Flip only the right blade if it sweeps the wrong way.")]
        [SerializeField] bool invertRight = false;

        [Header("Speeds")]
        [Min(1f)] [SerializeField] float lowSpeed = 200f;
        [Min(1f)] [SerializeField] float highSpeed = 420f;
        [Min(0f)] [SerializeField] float intermittentPause = 2.2f;

        [Header("Debug")]
        [SerializeField] Mode mode = Mode.Off;
        [SerializeField] bool drawGizmos = true;

        Quaternion leftRest = Quaternion.identity;
        Quaternion rightRest = Quaternion.identity;
        float path;
        float pauseTimer;
        bool pausing;

        public Mode CurrentMode => mode;

        /// <summary>Car-local outward normal of the windshield (wipe rotation axis).</summary>
        public Vector3 GlassNormal
        {
            get
            {
                if (axisOverride.sqrMagnitude > 1e-6f)
                    return axisOverride.normalized;
                // Lean glass back: tip +Z toward +Y so the sweep plane matches the window.
                return (Quaternion.AngleAxis(windshieldRake, Vector3.right) * Vector3.forward).normalized;
            }
        }

        void Awake() => CaptureRest();

        void OnDisable()
        {
            path = 0f;
            pausing = false;
            pauseTimer = 0f;
            ApplyPath(0f);
        }

        void Update()
        {
            if (!KeyboardBindings.IsListening
                && !SettingsMenu.IsOpen
                && KeyboardBindings.WasPressedThisFrame(KeyboardAction.Wipers))
                CycleMode();

            if (mode == Mode.Off)
            {
                float a = PathToAngle(path);
                a = Mathf.MoveTowards(a, 0f, highSpeed * Time.deltaTime);
                path = a;
                pausing = false;
                ApplyPath(path);
                return;
            }

            if (mode == Mode.Intermittent && pausing)
            {
                pauseTimer -= Time.deltaTime;
                ApplyPath(0f);
                if (pauseTimer > 0f)
                    return;
                pausing = false;
                path = 0f;
            }

            float speed = mode == Mode.High ? highSpeed : lowSpeed;
            path += speed * Time.deltaTime;
            float loop = maxAngle * 2f;
            if (path >= loop)
            {
                path = 0f;
                if (mode == Mode.Intermittent)
                {
                    pausing = true;
                    pauseTimer = intermittentPause;
                    ApplyPath(0f);
                    return;
                }
            }

            ApplyPath(path);
        }

        public void SetMode(Mode next)
        {
            mode = next;
            if (mode == Mode.Off)
            {
                pausing = false;
                return;
            }

            if (path <= 0.01f || path >= maxAngle * 2f - 0.01f)
            {
                path = 0f;
                pausing = false;
                pauseTimer = 0f;
            }
        }

        public void CycleMode() => SetMode((Mode)(((int)mode + 1) % 4));

        void CaptureRest()
        {
            if (leftBlade != null)
                leftRest = leftBlade.localRotation;
            if (rightBlade != null)
                rightRest = rightBlade.localRotation;
        }

        void ApplyPath(float leftPath)
        {
            float leftAngle = PathToAngle(leftPath);
            float rightAngle = PathToAngle(WrapPath(leftPath - rightPhase * (maxAngle * 2f)));

            float sign = wipeUp ? -1f : 1f;
            leftAngle *= sign;
            rightAngle *= sign;
            if (invertRight)
                rightAngle = -rightAngle;

            Vector3 axis = GlassNormal;
            if (leftBlade != null)
                leftBlade.localRotation = leftRest * Quaternion.AngleAxis(leftAngle, axis);
            if (rightBlade != null)
                rightBlade.localRotation = rightRest * Quaternion.AngleAxis(rightAngle, axis);
        }

        float PathToAngle(float p)
        {
            float loop = maxAngle * 2f;
            p = WrapPath(p);
            return p <= maxAngle ? p : (loop - p);
        }

        float WrapPath(float p)
        {
            float loop = Mathf.Max(0.01f, maxAngle * 2f);
            p %= loop;
            if (p < 0f)
                p += loop;
            return p;
        }

        void OnDrawGizmosSelected()
        {
            if (!drawGizmos)
                return;

            Vector3 axisLocal = GlassNormal;
            DrawBladeAxis(leftBlade, axisLocal, Color.cyan);
            DrawBladeAxis(rightBlade, axisLocal, Color.magenta);

            // Glass plane hint at left pivot.
            if (leftBlade == null)
                return;
            Vector3 origin = leftBlade.position;
            Vector3 axisWorld = transform.TransformDirection(axisLocal);
            Vector3 across = Vector3.Cross(axisWorld, transform.up).normalized;
            if (across.sqrMagnitude < 1e-4f)
                across = Vector3.Cross(axisWorld, transform.right).normalized;
            Vector3 upGlass = Vector3.Cross(across, axisWorld).normalized;
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
            Vector3 a = origin + (-across + upGlass) * 0.35f;
            Vector3 b = origin + (across + upGlass) * 0.35f;
            Vector3 c = origin + (across - upGlass) * 0.15f;
            Vector3 d = origin + (-across - upGlass) * 0.15f;
            Gizmos.DrawLine(a, b);
            Gizmos.DrawLine(b, c);
            Gizmos.DrawLine(c, d);
            Gizmos.DrawLine(d, a);
        }

        void DrawBladeAxis(Transform blade, Vector3 axisLocal, Color color)
        {
            if (blade == null)
                return;
            Gizmos.color = color;
            Vector3 o = blade.position;
            Vector3 n = transform.TransformDirection(axisLocal).normalized;
            Gizmos.DrawLine(o - n * 0.25f, o + n * 0.25f);
            Gizmos.DrawSphere(o, 0.015f);
        }
    }
}
