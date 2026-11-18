using FullThrottleSun.Controller;
using UnityEngine;

namespace FullThrottleSun.Vehicle
{
    /// <summary>
    /// Hold the cube with one hand (<see cref="HandGrabbable"/>), then hold Grip with the
    /// free hand near a face and twist to turn that layer. Release snaps to 90°.
    /// </summary>
    [RequireComponent(typeof(HandGrabbable))]
    [DefaultExecutionOrder(-20)]
    public class RubiksCubeInteract : MonoBehaviour
    {
        [SerializeField] HandGrabbable grab;
        [SerializeField] RubiksCubeBuilder builder;
        [Tooltip("Free-hand distance from cube centre to start / keep a twist.")]
        [SerializeField] float twistReach = 0.22f;
        [SerializeField] float twistSensitivity = 1.25f;
        [SerializeField] float snapSpeed = 720f;

        RubiksCubie[] cubies;
        Transform cubiesRoot;
        Transform turnPivot;

        bool twisting;
        ControllerHand twistHand;
        int axis;                   // 0=x 1=y 2=z
        int layer;                  // -1 or +1
        Vector3 localAxis;          // axis in cube local space
        float angle;
        Vector3 prevPlanar;
        bool hasPrevPlanar;
        bool snapping;
        float snapTarget;

        bool freeGripPrev;
        bool freeInReachPrev;

        public HandGrabbable Grab => grab;
        public bool IsHeld => grab != null && grab.IsHeld;
        public bool IsTwisting => twisting || snapping;

        void Awake()
        {
            if (grab == null)
                grab = GetComponent<HandGrabbable>();
            if (builder == null)
                builder = GetComponent<RubiksCubeBuilder>();
        }

        void OnEnable()
        {
            EnsureStructure();
        }

        void OnDisable()
        {
            CancelTwist(false);
            VrHandClaim.ReleaseAll(this);
            freeGripPrev = false;
            freeInReachPrev = false;
        }

        void Update()
        {
            EnsureStructure();

            if (snapping)
            {
                TickSnap();
                return;
            }

            if (twisting)
            {
                TickTwist();
                return;
            }

            if (!IsHeld)
            {
                freeGripPrev = false;
                freeInReachPrev = false;
                return;
            }

            ControllerHand holdHand = grab.HeldHand;
            ControllerHand free = Opposite(holdHand);

            bool freeGrip = VrGrip.IsHeld(free);
            bool freeGripDown = freeGrip && !freeGripPrev;
            freeGripPrev = freeGrip;

            if (!TryGetHandPos(free, out Vector3 handPos))
            {
                freeInReachPrev = false;
                return;
            }

            float reachSq = twistReach * twistReach;
            bool inReach = (handPos - transform.position).sqrMagnitude <= reachSq;
            bool enteredReach = inReach && !freeInReachPrev;
            freeInReachPrev = inReach;

            // Start when: grip presses while near, OR already gripping and hand enters reach.
            if (!inReach || !freeGrip)
                return;
            if (!freeGripDown && !enteredReach)
                return;
            if (!TryPickLayer(handPos, out axis, out layer, out localAxis))
                return;
            if (!VrHandClaim.TryClaim(free, this) && !VrHandClaim.IsOwnedBy(free, this))
                return;

            angle = 0f;
            hasPrevPlanar = false;
            Vector3 planar0 = Vector3.ProjectOnPlane(handPos - transform.position, transform.TransformDirection(localAxis));
            if (planar0.sqrMagnitude > 1e-8f)
            {
                prevPlanar = planar0.normalized;
                hasPrevPlanar = true;
            }

            BeginTwist(free);
        }

        static ControllerHand Opposite(ControllerHand hand)
            => hand == ControllerHand.Left ? ControllerHand.Right : ControllerHand.Left;

        static bool TryGetHandPos(ControllerHand hand, out Vector3 pos)
        {
            if (VrGrip.TryGetWorldPose(hand, out pos, out _))
                return true;
            // Fallback: VRHand transform if pose source is briefly unavailable.
            if (VrGrip.TryGetHand(hand, out VRHand view) && view != null)
            {
                pos = view.transform.position;
                return true;
            }
            pos = default;
            return false;
        }

        void EnsureStructure()
        {
            if (cubiesRoot == null)
                cubiesRoot = transform.Find("Cubies");
            if (cubiesRoot == null && builder != null)
            {
                builder.Rebuild();
                cubiesRoot = transform.Find("Cubies");
            }

            if (cubies == null || cubies.Length < 20)
                cubies = GetComponentsInChildren<RubiksCubie>(true);

            if (turnPivot == null)
            {
                Transform existing = transform.Find("TurnPivot");
                if (existing != null)
                    turnPivot = existing;
                else
                {
                    var go = new GameObject("TurnPivot");
                    turnPivot = go.transform;
                    turnPivot.SetParent(transform, false);
                }
            }
        }

        bool TryPickLayer(Vector3 handWorld, out int faceAxis, out int faceLayer, out Vector3 axisLocal)
        {
            faceAxis = 1;
            faceLayer = 1;
            axisLocal = Vector3.up;

            Vector3 local = transform.InverseTransformPoint(handWorld);
            Vector3 abs = new Vector3(Mathf.Abs(local.x), Mathf.Abs(local.y), Mathf.Abs(local.z));

            if (abs.y >= abs.x && abs.y >= abs.z)
            {
                faceAxis = 1;
                faceLayer = local.y >= 0f ? 1 : -1;
                axisLocal = Vector3.up * faceLayer;
            }
            else if (abs.x >= abs.z)
            {
                faceAxis = 0;
                faceLayer = local.x >= 0f ? 1 : -1;
                axisLocal = Vector3.right * faceLayer;
            }
            else
            {
                faceAxis = 2;
                faceLayer = local.z >= 0f ? 1 : -1;
                axisLocal = Vector3.forward * faceLayer;
            }

            return true;
        }

        void BeginTwist(ControllerHand hand)
        {
            twisting = true;
            twistHand = hand;

            turnPivot.SetParent(transform, false);
            turnPivot.localPosition = Vector3.zero;
            turnPivot.localRotation = Quaternion.identity;

            if (cubies == null || cubies.Length == 0)
                cubies = GetComponentsInChildren<RubiksCubie>(true);

            int parented = 0;
            for (int i = 0; i < cubies.Length; i++)
            {
                var c = cubies[i];
                if (c == null)
                    continue;
                if (CoordOnAxis(c.Coord, axis) != layer)
                    continue;
                c.transform.SetParent(turnPivot, true);
                parented++;
            }

            if (parented == 0)
            {
                // No cubies on that layer — abort cleanly.
                twisting = false;
                VrHandClaim.Release(hand, this);
            }
        }

        void TickTwist()
        {
            if (!IsHeld || !VrGrip.IsHeld(twistHand) || !TryGetHandPos(twistHand, out Vector3 handPos))
            {
                FinishTwist();
                return;
            }

            if ((handPos - transform.position).sqrMagnitude > twistReach * twistReach * 2.25f)
            {
                FinishTwist();
                return;
            }

            Vector3 worldAxis = transform.TransformDirection(localAxis);
            Vector3 planar = Vector3.ProjectOnPlane(handPos - transform.position, worldAxis);
            if (planar.sqrMagnitude < 1e-8f)
                return;

            planar.Normalize();
            if (hasPrevPlanar)
            {
                float delta = Vector3.SignedAngle(prevPlanar, planar, worldAxis);
                angle += delta * twistSensitivity;
                turnPivot.localRotation = Quaternion.AngleAxis(angle, localAxis);
            }

            prevPlanar = planar;
            hasPrevPlanar = true;
        }

        void FinishTwist()
        {
            if (!twisting)
                return;

            twisting = false;
            VrHandClaim.Release(twistHand, this);

            snapTarget = Mathf.Round(angle / 90f) * 90f;
            snapTarget = Mathf.Clamp(snapTarget, angle - 90f, angle + 90f);
            snapTarget = Mathf.Round(snapTarget / 90f) * 90f;

            if (Mathf.Abs(snapTarget - angle) < 0.5f)
            {
                ApplySnapImmediate(snapTarget);
                return;
            }

            snapping = true;
        }

        void TickSnap()
        {
            float step = snapSpeed * Time.deltaTime;
            angle = Mathf.MoveTowards(angle, snapTarget, step);
            turnPivot.localRotation = Quaternion.AngleAxis(angle, localAxis);
            if (Mathf.Abs(angle - snapTarget) > 0.05f)
                return;

            ApplySnapImmediate(snapTarget);
        }

        void ApplySnapImmediate(float finalAngle)
        {
            snapping = false;
            twisting = false;
            angle = finalAngle;
            turnPivot.localRotation = Quaternion.AngleAxis(finalAngle, localAxis);

            int turns = Mathf.RoundToInt(finalAngle / 90f) % 4;
            if (turns < 0)
                turns += 4;

            float step = ComputeStep();

            for (int i = turnPivot.childCount - 1; i >= 0; i--)
            {
                Transform child = turnPivot.GetChild(i);
                var cubie = child.GetComponent<RubiksCubie>();
                child.SetParent(cubiesRoot, true);
                if (cubie == null)
                    continue;

                Vector3Int c = cubie.Coord;
                for (int t = 0; t < turns; t++)
                    c = RotateCoord(c, axis, layer > 0);
                cubie.SetCoord(c);
                child.localPosition = new Vector3(c.x, c.y, c.z) * step;
            }

            turnPivot.localRotation = Quaternion.identity;
            turnPivot.localPosition = Vector3.zero;
            angle = 0f;
            hasPrevPlanar = false;
            cubies = GetComponentsInChildren<RubiksCubie>(true);
        }

        void CancelTwist(bool restore)
        {
            if (!twisting && !snapping)
                return;

            if (restore && twisting)
                ApplySnapImmediate(0f);
            else if (turnPivot != null && cubiesRoot != null)
            {
                for (int i = turnPivot.childCount - 1; i >= 0; i--)
                    turnPivot.GetChild(i).SetParent(cubiesRoot, true);
                turnPivot.localRotation = Quaternion.identity;
            }

            twisting = false;
            snapping = false;
            VrHandClaim.ReleaseAll(this);
        }

        float ComputeStep()
        {
            if (builder != null)
                return builder.CubieSize + 0.0015f;
            return 0.019f;
        }

        static int CoordOnAxis(Vector3Int c, int faceAxis)
        {
            if (faceAxis == 0) return c.x;
            if (faceAxis == 1) return c.y;
            return c.z;
        }

        static Vector3Int RotateCoord(Vector3Int c, int faceAxis, bool outwardPositive)
        {
            Vector3Int r;
            if (faceAxis == 0)
                r = new Vector3Int(c.x, -c.z, c.y);
            else if (faceAxis == 1)
                r = new Vector3Int(c.z, c.y, -c.x);
            else
                r = new Vector3Int(-c.y, c.x, c.z);

            if (!outwardPositive)
            {
                if (faceAxis == 0)
                    r = new Vector3Int(c.x, c.z, -c.y);
                else if (faceAxis == 1)
                    r = new Vector3Int(-c.z, c.y, c.x);
                else
                    r = new Vector3Int(c.y, -c.x, c.z);
            }

            return r;
        }
    }
}
