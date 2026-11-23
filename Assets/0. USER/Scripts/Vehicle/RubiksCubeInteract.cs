using FullThrottleSun.Controller;
using UnityEngine;

namespace FullThrottleSun.Vehicle
{
    /// <summary>
    /// Rubik two-hand logic:
    /// - First hand that collides + Grip becomes the holding hand (cube adsorbs via HandGrabbable).
    /// - Other hand that collides + Grip twists the face near it.
    /// - If the holding hand releases while the other is still colliding + Grip, hold transfers.
    /// Twist uses the visible <see cref="VRHand"/> mesh rotation (not raw tracked/gyro pose).
    /// </summary>
    [RequireComponent(typeof(HandGrabbable))]
    // Before HandGrabbable (-30) so hold can transfer before a physics drop.
    [DefaultExecutionOrder(-40)]
    public class RubiksCubeInteract : MonoBehaviour
    {
        [SerializeField] HandGrabbable grab;
        [SerializeField] RubiksCubeBuilder builder;
        [SerializeField] float twistReach = 0.28f;
        [SerializeField] float twistSensitivity = 1.15f;
        [SerializeField] float snapSpeed = 720f;
        [SerializeField] bool debugLog;

        RubiksCubie[] cubies;
        Transform cubiesRoot;
        Transform turnPivot;

        bool twisting;
        ControllerHand twistHand;
        int axis;
        int layer;
        Vector3 localAxis;
        float angle;
        Quaternion prevMeshRot;
        bool hasPrevMeshRot;
        bool snapping;
        float snapTarget;

        bool twistGripPrev;
        bool twistInReachPrev;

        public bool IsHeld => grab != null && grab.IsHeld;

        void Awake()
        {
            if (grab == null)
                grab = GetComponent<HandGrabbable>();
            if (builder == null)
                builder = GetComponent<RubiksCubeBuilder>();
        }

        void OnEnable() => EnsureStructure();

        void OnDisable()
        {
            CancelTwist();
            VrHandClaim.ReleaseAll(this);
            twistGripPrev = false;
            twistInReachPrev = false;
        }

        void Update()
        {
            EnsureStructure();

            // Before HandGrabbable drops: if hold Grip released but the other hand
            // still collides + Grips, transfer adsorb to that hand.
            if (IsHeld && !VrGrip.IsHeld(grab.HeldHand))
            {
                ControllerHand other = Opposite(grab.HeldHand);
                if (VrGrip.IsHeld(other) && HandCollidesCube(other))
                {
                    if (twisting && twistHand == other)
                        FinishTwist();
                    if (grab.TryForceGrab(other))
                        Log($"hold transferred → {other}");
                }
            }
        }

        void LateUpdate()
        {
            if (!IsHeld && twisting)
                FinishTwist();

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

            if (IsHeld)
                TryBeginTwist();
        }

        void TryBeginTwist()
        {
            ControllerHand free = Opposite(grab.HeldHand);
            bool grip = VrGrip.IsHeld(free);
            bool gripDown = grip && !twistGripPrev;
            twistGripPrev = grip;

            if (!TryGetHandMesh(free, out VRHand mesh, out Vector3 handPos, out Quaternion meshRot, out SphereCollider handCol))
            {
                twistInReachPrev = false;
                return;
            }

            bool colliding = HandCollidesCube(free, handPos, meshRot, handCol);
            bool inReach = colliding || (handPos - transform.position).sqrMagnitude <= twistReach * twistReach;
            bool entered = inReach && !twistInReachPrev;
            twistInReachPrev = inReach;

            if (!grip || !inReach)
                return;
            if (!gripDown && !entered)
                return;

            if (!TryPickCubieLayer(handPos, meshRot, handCol, out axis, out layer, out localAxis))
                return;

            if (!VrHandClaim.TryClaim(free, this) && !VrHandClaim.IsOwnedBy(free, this))
            {
                Log("twist claim blocked");
                return;
            }

            angle = 0f;
            prevMeshRot = meshRot;
            hasPrevMeshRot = true;
            BeginTwist(free);
        }

        void BeginTwist(ControllerHand hand)
        {
            twisting = true;
            twistHand = hand;

            turnPivot.SetParent(transform, false);
            turnPivot.localPosition = Vector3.zero;
            turnPivot.localRotation = Quaternion.identity;

            cubies = GetComponentsInChildren<RubiksCubie>(true);
            int parented = 0;
            for (int i = 0; i < cubies.Length; i++)
            {
                var c = cubies[i];
                if (c == null || CoordOnAxis(c.Coord, axis) != layer)
                    continue;
                c.transform.SetParent(turnPivot, true);
                parented++;
            }

            Log($"BeginTwist axis={axis} layer={layer} parented={parented}");
            if (parented == 0)
            {
                twisting = false;
                VrHandClaim.Release(hand, this);
            }
        }

        void TickTwist()
        {
            // Holding hand gone, or twist hand released grip / lost mesh → finish.
            if (!IsHeld || !VrGrip.IsHeld(twistHand)
                || !TryGetHandMesh(twistHand, out _, out _, out Quaternion meshRot, out _))
            {
                FinishTwist();
                return;
            }

            // If hold transferred to the twist hand, stop twisting with that hand.
            if (grab.HeldHand == twistHand)
            {
                FinishTwist();
                return;
            }

            Vector3 worldAxis = transform.TransformDirection(localAxis).normalized;
            if (hasPrevMeshRot)
            {
                // Mesh rotation delta projected onto the face axis (not position/gyro orbit).
                Quaternion delta = meshRot * Quaternion.Inverse(prevMeshRot);
                delta.ToAngleAxis(out float deg, out Vector3 ax);
                if (deg > 180f)
                    deg -= 360f;
                if (ax.sqrMagnitude > 1e-8f)
                {
                    float contrib = deg * Vector3.Dot(ax.normalized, worldAxis);
                    angle += contrib * twistSensitivity;
                    turnPivot.localRotation = Quaternion.AngleAxis(angle, localAxis);
                }
            }

            prevMeshRot = meshRot;
            hasPrevMeshRot = true;
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
            Log($"FinishTwist angle={angle:F1} snap={snapTarget}");

            if (Mathf.Abs(snapTarget - angle) < 0.5f)
            {
                ApplySnapImmediate(snapTarget);
                return;
            }

            snapping = true;
        }

        void TickSnap()
        {
            angle = Mathf.MoveTowards(angle, snapTarget, snapSpeed * Time.deltaTime);
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

            float step = builder != null ? builder.Step : 0.02f;

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
            hasPrevMeshRot = false;
            cubies = GetComponentsInChildren<RubiksCubie>(true);
        }

        void CancelTwist()
        {
            if (!twisting && !snapping)
                return;
            if (turnPivot != null && cubiesRoot != null)
            {
                for (int i = turnPivot.childCount - 1; i >= 0; i--)
                    turnPivot.GetChild(i).SetParent(cubiesRoot, true);
                turnPivot.localRotation = Quaternion.identity;
            }
            twisting = false;
            snapping = false;
            // Don't ReleaseAll — hold hand may still be claimed by HandGrabbable.
            VrHandClaim.Release(twistHand, this);
        }

        bool HandCollidesCube(ControllerHand hand)
        {
            if (!TryGetHandMesh(hand, out _, out Vector3 pos, out Quaternion rot, out SphereCollider col))
                return false;
            return HandCollidesCube(hand, pos, rot, col);
        }

        bool HandCollidesCube(ControllerHand hand, Vector3 handPos, Quaternion handRot, SphereCollider handCol)
        {
            // Root grab collider.
            var rootCol = GetComponent<Collider>();
            if (rootCol != null && handCol != null && GrabOverlap.Touches(handCol, handPos, handRot, rootCol))
                return true;

            if (cubies == null || cubies.Length == 0)
                cubies = GetComponentsInChildren<RubiksCubie>(true);

            for (int i = 0; i < cubies.Length; i++)
            {
                var c = cubies[i];
                if (c == null || c.PickCollider == null || handCol == null)
                    continue;
                if (GrabOverlap.Touches(handCol, handPos, handRot, c.PickCollider))
                    return true;
            }

            return (handPos - transform.position).sqrMagnitude <= twistReach * twistReach * 0.25f;
        }

        /// <summary>Visible hand-mesh pose (VRHand transform), not raw tracked/gyro pose.</summary>
        static bool TryGetHandMesh(
            ControllerHand hand,
            out VRHand mesh,
            out Vector3 pos,
            out Quaternion rot,
            out SphereCollider handCol)
        {
            mesh = null;
            handCol = null;
            pos = default;
            rot = Quaternion.identity;
            if (!VrGrip.TryGetHand(hand, out mesh) || mesh == null)
                return false;

            handCol = mesh.GrabCollider;
            pos = mesh.transform.position;
            rot = mesh.transform.rotation;
            return true;
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

            for (int i = 0; i < cubies.Length; i++)
            {
                if (cubies[i] == null || cubies[i].Coord != Vector3Int.zero)
                    continue;
                if (TryParseCoordFromName(cubies[i].name, out Vector3Int parsed))
                    cubies[i].SetCoord(parsed);
            }

            if (turnPivot == null)
            {
                Transform existing = transform.Find("TurnPivot");
                if (existing != null)
                    turnPivot = existing;
                else
                {
                    turnPivot = new GameObject("TurnPivot").transform;
                    turnPivot.SetParent(transform, false);
                }
            }
        }

        static bool TryParseCoordFromName(string name, out Vector3Int coord)
        {
            coord = Vector3Int.zero;
            if (string.IsNullOrEmpty(name) || !name.StartsWith("Cubie_"))
                return false;
            string[] parts = name.Split('_');
            if (parts.Length != 4)
                return false;
            if (!int.TryParse(parts[1], out int x)) return false;
            if (!int.TryParse(parts[2], out int y)) return false;
            if (!int.TryParse(parts[3], out int z)) return false;
            coord = new Vector3Int(x, y, z);
            return true;
        }

        bool TryPickCubieLayer(Vector3 handPos, Quaternion handRot, SphereCollider handCol, out int faceAxis, out int faceLayer, out Vector3 axisLocal)
        {
            faceAxis = 1;
            faceLayer = 1;
            axisLocal = Vector3.up;

            if (cubies == null || cubies.Length == 0)
                cubies = GetComponentsInChildren<RubiksCubie>(true);

            RubiksCubie best = null;
            float bestDist = float.MaxValue;

            for (int i = 0; i < cubies.Length; i++)
            {
                var c = cubies[i];
                if (c == null)
                    continue;

                float d = (c.transform.position - handPos).sqrMagnitude;
                bool touch = handCol != null && c.PickCollider != null
                    && GrabOverlap.Touches(handCol, handPos, handRot, c.PickCollider);
                if (!touch && d > twistReach * twistReach)
                    continue;
                if (touch)
                    d *= 0.01f;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = c;
                }
            }

            Vector3 localHand = transform.InverseTransformPoint(handPos);
            if (best == null)
            {
                Vector3 abs = new Vector3(Mathf.Abs(localHand.x), Mathf.Abs(localHand.y), Mathf.Abs(localHand.z));
                if (abs.y >= abs.x && abs.y >= abs.z)
                {
                    faceAxis = 1;
                    faceLayer = localHand.y >= 0f ? 1 : -1;
                }
                else if (abs.x >= abs.z)
                {
                    faceAxis = 0;
                    faceLayer = localHand.x >= 0f ? 1 : -1;
                }
                else
                {
                    faceAxis = 2;
                    faceLayer = localHand.z >= 0f ? 1 : -1;
                }
            }
            else
            {
                Vector3Int coord = best.Coord;
                float sx = Mathf.Abs(localHand.x);
                float sy = Mathf.Abs(localHand.y);
                float sz = Mathf.Abs(localHand.z);
                if (Mathf.Abs(coord.x) == 1 && sx >= sy && sx >= sz)
                {
                    faceAxis = 0;
                    faceLayer = coord.x >= 0 ? 1 : -1;
                }
                else if (Mathf.Abs(coord.y) == 1 && sy >= sx && sy >= sz)
                {
                    faceAxis = 1;
                    faceLayer = coord.y >= 0 ? 1 : -1;
                }
                else if (Mathf.Abs(coord.z) == 1)
                {
                    faceAxis = 2;
                    faceLayer = coord.z >= 0 ? 1 : -1;
                }
                else if (Mathf.Abs(coord.y) == 1)
                {
                    faceAxis = 1;
                    faceLayer = coord.y;
                }
                else if (Mathf.Abs(coord.x) == 1)
                {
                    faceAxis = 0;
                    faceLayer = coord.x;
                }
                else
                {
                    faceAxis = 2;
                    faceLayer = coord.z >= 0 ? 1 : -1;
                }
            }

            if (faceAxis == 0) axisLocal = Vector3.right * faceLayer;
            else if (faceAxis == 1) axisLocal = Vector3.up * faceLayer;
            else axisLocal = Vector3.forward * faceLayer;
            return true;
        }

        static ControllerHand Opposite(ControllerHand hand)
            => hand == ControllerHand.Left ? ControllerHand.Right : ControllerHand.Left;

        static int CoordOnAxis(Vector3Int c, int faceAxis)
        {
            if (faceAxis == 0) return c.x;
            if (faceAxis == 1) return c.y;
            return c.z;
        }

        static Vector3Int RotateCoord(Vector3Int c, int faceAxis, bool outwardPositive)
        {
            Vector3Int r;
            if (faceAxis == 0) r = new Vector3Int(c.x, -c.z, c.y);
            else if (faceAxis == 1) r = new Vector3Int(c.z, c.y, -c.x);
            else r = new Vector3Int(-c.y, c.x, c.z);

            if (!outwardPositive)
            {
                if (faceAxis == 0) r = new Vector3Int(c.x, c.z, -c.y);
                else if (faceAxis == 1) r = new Vector3Int(-c.z, c.y, c.x);
                else r = new Vector3Int(c.y, -c.x, c.z);
            }
            return r;
        }

        void Log(string msg)
        {
            if (debugLog)
                Debug.Log($"[RubiksCube] {msg}", this);
        }
    }
}
