using FullThrottleSun.Controller;
using UnityEngine;

namespace FullThrottleSun.Vehicle
{
    /// <summary>
    /// Dual windshield wipers. Park = scene rest pose; swing around local +Z after optional lay pitch.
    /// </summary>
    public class WindshieldWipers : MonoBehaviour
    {
        public enum Mode { Off = 0, Intermittent = 1, Low = 2, High = 3 }

        [Header("Blades")]
        [SerializeField] Transform leftBlade;
        [SerializeField] Transform rightBlade;
        [SerializeField] float layOnGlassPitch;
        [SerializeField] float maxAngle = 75f;
        [SerializeField] bool invertSwing = true;
        [SerializeField] float rightPhase = 0.1f;
        [SerializeField] bool invertRight;

        [Header("Speeds")]
        [SerializeField] float lowSpeed = 200f;
        [SerializeField] float highSpeed = 420f;
        [SerializeField] float intermittentPause = 2.2f;

        [SerializeField] Mode mode = Mode.Off;

        Quaternion leftRest = Quaternion.identity;
        Quaternion rightRest = Quaternion.identity;
        float path;
        float pauseTimer;
        bool pausing;

        public Mode CurrentMode => mode;

        void Awake()
        {
            if (leftBlade != null) leftRest = leftBlade.localRotation;
            if (rightBlade != null) rightRest = rightBlade.localRotation;
        }

        void OnDisable()
        {
            path = 0f;
            pausing = false;
            pauseTimer = 0f;
            Apply(0f);
        }

        void Update()
        {
            if (!KeyboardBindings.IsListening
                && !SettingsMenu.IsOpen
                && KeyboardBindings.WasPressedThisFrame(KeyboardAction.Wipers))
                CycleMode();

            if (mode == Mode.Off)
            {
                path = Mathf.MoveTowards(Triangle(path), 0f, highSpeed * Time.deltaTime);
                pausing = false;
                Apply(path);
                return;
            }

            if (mode == Mode.Intermittent && pausing)
            {
                pauseTimer -= Time.deltaTime;
                Apply(0f);
                if (pauseTimer > 0f) return;
                pausing = false;
                path = 0f;
            }

            path += (mode == Mode.High ? highSpeed : lowSpeed) * Time.deltaTime;
            float loop = Mathf.Max(0.01f, maxAngle * 2f);
            if (path >= loop)
            {
                path = 0f;
                if (mode == Mode.Intermittent)
                {
                    pausing = true;
                    pauseTimer = intermittentPause;
                    Apply(0f);
                    return;
                }
            }

            Apply(path);
        }

        public void SetMode(Mode next)
        {
            mode = next;
            if (mode == Mode.Off)
            {
                pausing = false;
                return;
            }

            float loop = maxAngle * 2f;
            if (path <= 0.01f || path >= loop - 0.01f)
            {
                path = 0f;
                pausing = false;
                pauseTimer = 0f;
            }
        }

        public void CycleMode() => SetMode((Mode)(((int)mode + 1) % 4));

        public void StepFaster() => SetMode((Mode)Mathf.Min((int)mode + 1, (int)Mode.High));

        public void StepSlower() => SetMode((Mode)Mathf.Max((int)mode - 1, (int)Mode.Off));

        void Apply(float leftPath)
        {
            float loop = Mathf.Max(0.01f, maxAngle * 2f);
            float sign = invertSwing ? -1f : 1f;
            float leftAngle = Triangle(leftPath) * sign;
            float rightAngle = Triangle(Wrap(leftPath - rightPhase * loop, loop)) * sign;
            if (invertRight) rightAngle = -rightAngle;

            Quaternion lay = Quaternion.AngleAxis(layOnGlassPitch, Vector3.right);
            if (leftBlade != null)
                leftBlade.localRotation = leftRest * lay * Quaternion.AngleAxis(leftAngle, Vector3.forward);
            if (rightBlade != null)
                rightBlade.localRotation = rightRest * lay * Quaternion.AngleAxis(rightAngle, Vector3.forward);
        }

        float Triangle(float p)
        {
            float loop = Mathf.Max(0.01f, maxAngle * 2f);
            p = Wrap(p, loop);
            return p <= maxAngle ? p : loop - p;
        }

        static float Wrap(float p, float loop)
        {
            p %= loop;
            if (p < 0f) p += loop;
            return p;
        }
    }
}
