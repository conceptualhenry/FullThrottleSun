using FullThrottleSun.Controller;
using UnityEngine;

namespace FullThrottleSun.Vehicle
{
    /// <summary>
    /// Wipes along the windshield: first pitch the blade onto the glass, then spin around
    /// that glass normal (local +Z after the pitch). Avoids pure-Y flat sweeps and pure-Z vertical flaps.
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
        [Tooltip("Pitch (degrees) that lays the blade onto the windshield. Negative tips the tip up the glass. Match your glass rake (~ -35 to -50).")]
        [Range(-80f, 80f)] [SerializeField] float layOnGlassPitch = -40f;
        [Tooltip("Extra lay pitch for Brush_R only (degrees). Negative = press a bit more onto the glass.")]
        [Range(-20f, 20f)] [SerializeField] float rightLayExtraPitch = -6f;
        [Tooltip("Brush_R mesh local axes are mirrored vs Brush_L: flip lay pitch sign and spin around −Z.")]
        [SerializeField] bool mirrorRightBlade = true;
        [Tooltip("Peak swing on the glass (degrees).")]
        [Range(10f, 120f)] [SerializeField] float maxAngle = 75f;
        [Tooltip("Negate swing direction if it parks the wrong way.")]
        [SerializeField] bool invertSwing = true;
        [Range(0f, 0.45f)] [SerializeField] float rightPhase = 0.1f;
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

        /// <summary>One step faster: Off → Int → Low → High (clamped).</summary>
        public void StepFaster()
        {
            int next = Mathf.Min((int)mode + 1, (int)Mode.High);
            SetMode((Mode)next);
        }

        /// <summary>One step slower: High → Low → Int → Off (clamped).</summary>
        public void StepSlower()
        {
            int next = Mathf.Max((int)mode - 1, (int)Mode.Off);
            SetMode((Mode)next);
        }

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

            float sign = invertSwing ? -1f : 1f;
            leftAngle *= sign;
            rightAngle *= sign;
            if (invertRight)
                rightAngle = -rightAngle;

            // 1) Pitch onto the glass. 2) Spin around local ±Z (= glass normal after that pitch).
            Quaternion layL = Quaternion.AngleAxis(layOnGlassPitch, Vector3.right);
            float rightLayDeg = layOnGlassPitch + rightLayExtraPitch;
            if (mirrorRightBlade)
                rightLayDeg = -rightLayDeg;
            Quaternion layR = Quaternion.AngleAxis(rightLayDeg, Vector3.right);
            Vector3 rightSpin = mirrorRightBlade ? Vector3.back : Vector3.forward;
            if (leftBlade != null)
                leftBlade.localRotation = leftRest * layL * Quaternion.AngleAxis(leftAngle, Vector3.forward);
            if (rightBlade != null)
                rightBlade.localRotation = rightRest * layR * Quaternion.AngleAxis(rightAngle, rightSpin);
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

            Quaternion layL = Quaternion.AngleAxis(layOnGlassPitch, Vector3.right);
            float rightLayDeg = layOnGlassPitch + rightLayExtraPitch;
            if (mirrorRightBlade)
                rightLayDeg = -rightLayDeg;
            Quaternion layR = Quaternion.AngleAxis(rightLayDeg, Vector3.right);
            Vector3 spinR = mirrorRightBlade ? Vector3.back : Vector3.forward;

            DrawBlade(leftBlade, layL * Vector3.forward, layL * Vector3.left, layL * Vector3.up, Color.cyan);
            DrawBlade(rightBlade, layR * spinR, layR * Vector3.left, layR * Vector3.up, Color.magenta);
        }

        void DrawBlade(Transform blade, Vector3 axisLocal, Vector3 acrossLocal, Vector3 upGlassLocal, Color color)
        {
            if (blade == null)
                return;
            Vector3 o = blade.position;
            Vector3 axis = transform.TransformDirection(axisLocal).normalized;
            Vector3 across = transform.TransformDirection(acrossLocal).normalized;
            Vector3 upG = transform.TransformDirection(upGlassLocal).normalized;

            Gizmos.color = color;
            Gizmos.DrawLine(o - axis * 0.2f, o + axis * 0.2f);
            Gizmos.DrawSphere(o, 0.012f);

            // Glass plane patch.
            Gizmos.color = new Color(color.r, color.g, color.b, 0.35f);
            Vector3 a = o + (-across + upG) * 0.3f;
            Vector3 b = o + (across + upG) * 0.3f;
            Vector3 c = o + (across - upG) * 0.1f;
            Vector3 d = o + (-across - upG) * 0.1f;
            Gizmos.DrawLine(a, b);
            Gizmos.DrawLine(b, c);
            Gizmos.DrawLine(c, d);
            Gizmos.DrawLine(d, a);
        }
    }
}
