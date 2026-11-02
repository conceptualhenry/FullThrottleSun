using FullThrottleSun.Controller;
using UnityEngine;

namespace FullThrottleSun.Vehicle
{
    /// <summary>
    /// Lets tracked controllers (Meta Quest) grab the cockpit steering wheel. A hand touching the wheel's colliders grabs
    /// when the grip (side trigger) is pressed, stays pinned where it grabbed and turns the wheel:
    /// one hand by the controller's own rotation around the steering column, two hands by the angle of the line between
    /// them. With no hand on it the wheel eases back to centre. In third person the hands are hidden and pressing the
    /// grip takes the wheel straight away, wherever the hand is, holding it at a default spot on the rim.
    /// Switching views keeps holding until the grip is let go. Needs the headset on.
    /// Grab volume: assign colliders, or leave empty to build a rim ring of CapsuleColliders
    /// (sphere+cylinder hybrids) under <c>Grab Zone</c> — leaves the hub open for the horn.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(TireFrictionCar))]
    public class SteeringWheelGrab : MonoBehaviour, IVrHandPoseSource, IHandPinProvider
    {
        [SerializeField] CarFollowCamera viewCamera;

        [Header("Grab zone")]
        [Tooltip("Colliders a hand must touch to grab. Empty = rim ring of CapsuleColliders (sphere+cylinder).")]
        [SerializeField] Collider[] wheelColliders;
        [Tooltip("Rim ring radius (m), to the capsule centres. Auto-filled from mesh when possible.")]
        [Min(0.01f)] [SerializeField] float rimRadius = 0.2f;
        [Tooltip("Capsule tube radius (m). Larger = easier to grab.")]
        [Min(0.005f)] [SerializeField] float rimThickness = 0.07f;
        [Range(6, 32)] [SerializeField] int rimSegments = 16;

        [Header("Steering")]
        [Tooltip("Degrees per second the wheel turns back to centre with no hand on it.")]
        [Min(0f)] [SerializeField] float returnSpeed = 90f;
        [Tooltip("Two hands closer than this (m) give no usable line between them; the first hand steers alone.")]
        [Min(0.01f)] [SerializeField] float minHandSpacing = 0.1f;
        [Tooltip("Where a third person grab holds the rim: degrees from the top, left hand to the left and right hand " +
                 "to the right. 90 = quarter to three, 60 = ten to two.")]
        [Range(0f, 180f)] [SerializeField] float defaultGripAngle = 90f;

        [Header("Hands")]
        [Tooltip("Optional hand models, posed like a fist around the controller grip. Empty = placeholder shapes.")]
        [SerializeField] GameObject leftHandModel;
        [SerializeField] GameObject rightHandModel;
        [Tooltip("Radius of each hand's grab collider (m).")]
        [Min(0.01f)] [SerializeField] float handRadius = 0.065f;

        [Header("Debug (read only)")]
        [Tooltip("Steering wheel angle in degrees, right positive.")]
        [SerializeField] float wheelAngle;
        [SerializeField] bool leftHolding;
        [SerializeField] bool rightHolding;

        public bool LeftHolding => leftHolding;
        public bool RightHolding => rightHolding;

        LightStalk lightStalk;
        WiperStalk wiperStalk;

        public float DistanceToWheel(Vector3 worldPosition)
        {
            float best = float.MaxValue;
            if (wheelColliders != null)
            {
                foreach (var c in wheelColliders)
                {
                    if (c == null || !c.enabled)
                        continue;
                    float d = (c.ClosestPoint(worldPosition) - worldPosition).sqrMagnitude;
                    if (d < best)
                        best = d;
                }
            }
            if (best < float.MaxValue)
                return Mathf.Sqrt(best);
            if (wheel != null)
                return Vector3.Distance(worldPosition, wheel.position);
            return float.MaxValue;
        }

        public bool TouchesWheelWorld(Vector3 worldPosition, Quaternion worldRotation, SphereCollider handCollider)
        {
            if (handCollider == null || wheelColliders == null)
                return false;
            foreach (var c in wheelColliders)
            {
                if (c == null || !c.enabled || !c.gameObject.activeInHierarchy)
                    continue;
                if (Physics.ComputePenetration(handCollider, worldPosition, worldRotation,
                        c, c.transform.position, c.transform.rotation, out _, out _))
                    return true;
            }
            return false;
        }

        class Hand
        {
            public VRHand view;
            public bool tracked;
            // Car local space.
            public Vector3 position;
            public Quaternion rotation = Quaternion.identity;
            public Vector3 previousPosition;
            public Quaternion previousRotation = Quaternion.identity;
            public bool holding;
            public int grabOrder;
            // Steering wheel local space.
            public Vector3 grabPosition;
            public Quaternion grabRotation;
        }

        TireFrictionCar car;
        Transform wheel;
        readonly Hand[] hands = { new Hand(), new Hand() };
        int grabCounter;
        int calibrationVersion;
        bool firstPerson;
        // Middle of the rim, steering wheel local space.
        Vector3 rimCenter;

        void Awake()
        {
            car = GetComponent<TireFrictionCar>();
            wheel = car.SteeringWheel;
            if (wheel == null)
            {
                Debug.LogWarning("[SteeringWheelGrab] TireFrictionCar has no steering wheel assigned.", this);
                enabled = false;
                return;
            }

            if (viewCamera == null)
                viewCamera = FindAnyObjectByType<CarFollowCamera>();
            lightStalk = GetComponentInChildren<LightStalk>(true) ?? FindAnyObjectByType<LightStalk>();
            wiperStalk = GetComponentInChildren<WiperStalk>(true) ?? FindAnyObjectByType<WiperStalk>();
            var mesh = wheel.GetComponent<MeshFilter>();
            if (mesh != null && mesh.sharedMesh != null)
            {
                Bounds b = mesh.sharedMesh.bounds;
                rimCenter = new Vector3(0f, 0f, b.center.z);
                float outer = 0.5f * Mathf.Max(b.size.x, b.size.y) * Mathf.Max(wheel.lossyScale.x, wheel.lossyScale.y);
                if (outer > 0.05f)
                    rimRadius = Mathf.Max(0.08f, outer * 0.78f);
            }
            if (wheelColliders == null || wheelColliders.Length == 0)
                CollectOrBuildRimColliders();

            for (int i = 0; i < hands.Length; i++)
            {
                var hand = (ControllerHand)i;
                var go = new GameObject(hand == ControllerHand.Left ? "[VR Hand L]" : "[VR Hand R]");
                hands[i].view = go.AddComponent<VRHand>();
                hands[i].view.Init(this, hand, hand == ControllerHand.Left ? leftHandModel : rightHandModel, handRadius, transform);
            }

            HandPinHub.Register(this);
        }

        void OnDisable()
        {
            for (int i = 0; i < hands.Length; i++)
            {
                if (hands[i].holding)
                    VrHandClaim.Release((ControllerHand)i, this);
                hands[i].holding = false;
                hands[i].tracked = false;
            }
            VrHandClaim.ReleaseAll(this);
            if (car != null)
                car.SetWheelInput(0f, false);
        }

        void OnDestroy()
        {
            HandPinHub.Unregister(this);
            VrHandClaim.ReleaseAll(this);
            foreach (var hand in hands)
                if (hand.view != null)
                    Destroy(hand.view.gameObject);
        }

        void Update()
        {
            bool active = viewCamera != null && VirtualController.IsTrackedController && viewCamera.IsHeadTracked;

            // Switching views keeps the grip, but the controller pose mapping changes with it (first person adds the
            // stick look), so that frame's motion isn't steering.
            bool nowFirstPerson = active && viewCamera.IsFirstPerson;
            bool viewChanged = nowFirstPerson != firstPerson;
            firstPerson = nowFirstPerson;

            bool recentered = viewChanged || (viewCamera != null && viewCamera.CalibrationVersion != calibrationVersion);
            if (viewCamera != null)
                calibrationVersion = viewCamera.CalibrationVersion;

            for (int i = 0; i < hands.Length; i++)
                UpdateHand(hands[i], (ControllerHand)i, active, recentered);

            float lockDegrees = car.SteeringWheelLock;
            bool held = hands[0].holding || hands[1].holding;
            if (held)
                // SteerDelta turns counter-clockwise as seen from the driver, which is a left turn.
                wheelAngle = Mathf.Clamp(wheelAngle - SteerDelta(), -lockDegrees, lockDegrees);
            else
                wheelAngle = Mathf.MoveTowards(wheelAngle, 0f, returnSpeed * Time.deltaTime);

            foreach (var hand in hands)
            {
                hand.previousPosition = hand.position;
                hand.previousRotation = hand.rotation;
            }

            leftHolding = hands[0].holding;
            rightHolding = hands[1].holding;
            car.SetWheelInput(lockDegrees > 0f ? wheelAngle / lockDegrees : 0f, held);
        }

        /// <summary>Where the real controller is, in world space. Only while the hands are shown (first person).</summary>
        public bool TryGetTrackedPose(ControllerHand hand, out Vector3 position, out Quaternion rotation)
        {
            var h = hands[(int)hand];
            position = transform.TransformPoint(h.position);
            rotation = transform.rotation * h.rotation;
            return enabled && firstPerson && h.tracked;
        }

        /// <summary>Where the hand is held on the wheel, in world space, while it holds it.</summary>
        public bool TryGetPinnedPose(ControllerHand hand, out Vector3 position, out Quaternion rotation)
        {
            var h = hands[(int)hand];
            position = wheel.TransformPoint(h.grabPosition);
            rotation = wheel.rotation * h.grabRotation;
            return enabled && h.holding;
        }

        void UpdateHand(Hand h, ControllerHand hand, bool active, bool recentered)
        {
            var state = VirtualController.GetState(hand);
            bool wasTracked = h.tracked;
            h.tracked = active && state.isTracked
                        && viewCamera.TryTrackedToTarget(state.position, state.rotation, out h.position, out h.rotation);

            // A pose from before tracking resumed or before a recenter isn't comparable: don't steer from it.
            if (h.tracked && (!wasTracked || recentered))
            {
                h.previousPosition = h.position;
                h.previousRotation = h.rotation;
            }

            // Another system (stalk / HandGrabbable) owns this hand.
            if (!VrHandClaim.IsFree(hand) && !VrHandClaim.IsOwnedBy(hand, this))
            {
                if (h.holding)
                {
                    h.holding = false;
                    VrHandClaim.Release(hand, this);
                }
                return;
            }

            if (h.holding)
            {
                if (!h.tracked || !VrGrip.IsHeld(hand))
                {
                    h.holding = false;
                    VrHandClaim.Release(hand, this);
                }
            }
            else if (h.tracked && VrGrip.IsDown(hand))
            {
                if (!firstPerson)
                {
                    // Third person: still prefer stalk if it's clearly closer.
                    Vector3 world = transform.TransformPoint(h.position);
                    if (PreferStalkOverWheel(world, null))
                        return;
                    GrabAtDefault(h, hand);
                }
                else if (TouchesWheel(h))
                {
                    Vector3 world = transform.TransformPoint(h.position);
                    Quaternion worldRot = transform.rotation * h.rotation;
                    var handCol = h.view != null ? h.view.GrabCollider : null;
                    // Hub horn: leave the hand free when the pad is closer than the rim.
                    if (handCol != null
                        && SteeringHorn.OverlapsPad(handCol, world, worldRot)
                        && SteeringHorn.DistanceToNearestPad(world) < DistanceToWheel(world) + 0.01f)
                        return;
                    if (PreferStalkOverWheel(world, handCol, worldRot))
                        return;
                    Grab(h, hand);
                }
            }
        }

        bool PreferStalkOverWheel(Vector3 world, SphereCollider handCol, Quaternion worldRot = default)
        {
            float wheelDist = DistanceToWheel(world);
            if (lightStalk != null && lightStalk.Touches(world, worldRot, handCol)
                && lightStalk.DistanceTo(world) < wheelDist - 0.01f)
                return true;
            if (wiperStalk != null && wiperStalk.Touches(world, worldRot, handCol)
                && wiperStalk.DistanceTo(world) < wheelDist - 0.01f)
                return true;
            return false;
        }

        bool TouchesWheel(Hand h)
        {
            // Posed explicitly: collider poses synced to physics lag the interpolated car by up to a physics step.
            var handCollider = h.view.GrabCollider;
            Vector3 position = transform.TransformPoint(h.position);
            Quaternion rotation = transform.rotation * h.rotation;
            return GrabOverlap.TouchesAny(handCollider, position, rotation, wheelColliders);
        }

        void Grab(Hand h, ControllerHand hand)
        {
            if (!VrHandClaim.TryClaim(hand, this))
                return;
            h.holding = true;
            h.grabOrder = ++grabCounter;
            h.grabPosition = wheel.InverseTransformPoint(transform.TransformPoint(h.position));
            h.grabRotation = Quaternion.Inverse(wheel.rotation) * (transform.rotation * h.rotation);
        }

        /// <summary>
        /// Holds the rim at defaultGripAngle on the hand's side, measured upright however the wheel is turned, with the
        /// fist wrapped around the rim and the palm towards the hub.
        /// </summary>
        void GrabAtDefault(Hand h, ControllerHand hand)
        {
            if (!VrHandClaim.TryClaim(hand, this))
                return;
            h.holding = true;
            h.grabOrder = ++grabCounter;

            float side = hand == ControllerHand.Left ? -1f : 1f;
            Vector3 axis = wheel.forward;
            Vector3 up = Vector3.ProjectOnPlane(transform.up, axis).normalized;
            Vector3 right = Vector3.Cross(up, axis);
            float angle = defaultGripAngle * Mathf.Deg2Rad;
            Vector3 radial = up * Mathf.Cos(angle) + right * (side * Mathf.Sin(angle));
            // Tangent along the rim, pointing up on either side; the grip pose's +Z runs through the fist.
            Vector3 along = -side * Vector3.Cross(radial, axis);

            Vector3 position = wheel.TransformPoint(rimCenter) + radial * (rimRadius * wheel.lossyScale.x);
            h.grabPosition = wheel.InverseTransformPoint(position);
            h.grabRotation = Quaternion.Inverse(wheel.rotation) * Quaternion.LookRotation(along, -axis);
        }

        /// <summary>This frame's wheel rotation around the steering column (degrees, Quaternion.AngleAxis convention).</summary>
        float SteerDelta()
        {
            Vector3 axis = transform.InverseTransformDirection(wheel.forward);
            Hand left = hands[0];
            Hand right = hands[1];

            if (left.holding && right.holding)
            {
                Vector3 now = Vector3.ProjectOnPlane(right.position - left.position, axis);
                Vector3 before = Vector3.ProjectOnPlane(right.previousPosition - left.previousPosition, axis);
                if (now.magnitude >= minHandSpacing && before.magnitude >= minHandSpacing)
                    return Vector3.SignedAngle(before, now, axis);
            }

            Hand first = !right.holding || (left.holding && left.grabOrder < right.grabOrder) ? left : right;
            return TwistAngle(first.rotation * Quaternion.Inverse(first.previousRotation), axis);
        }

        /// <summary>Signed angle (degrees) of the part of a rotation that turns around the given unit axis.</summary>
        static float TwistAngle(Quaternion rotation, Vector3 axis)
        {
            float angle = 2f * Mathf.Atan2(Vector3.Dot(new Vector3(rotation.x, rotation.y, rotation.z), axis), rotation.w);
            return Mathf.DeltaAngle(0f, angle * Mathf.Rad2Deg);
        }

        /// <summary>
        /// Prefer a scene-authored <c>Grab Zone</c> under the wheel. Only build at runtime if missing.
        /// </summary>
        void CollectOrBuildRimColliders()
        {
            Transform existing = wheel.Find("Grab Zone");
            if (existing != null)
            {
                var found = existing.GetComponentsInChildren<Collider>(true);
                if (found != null && found.Length > 0)
                {
                    wheelColliders = found;
                    return;
                }
            }

            BuildRimColliders();
        }

        /// <summary>
        /// Rim ring of CapsuleColliders (sphere ends + cylinder body). Hub stays open for the horn.
        /// </summary>
        void BuildRimColliders()
        {
            // Only under the steering wheel — never touch the horn Cylinder / pad.
            Transform legacyMesh = wheel.Find("GrabVolume");
            if (legacyMesh != null)
                Destroy(legacyMesh.gameObject);

            Transform existing = wheel.Find("Grab Zone");
            if (existing != null)
                Destroy(existing.gameObject);

            var root = new GameObject("Grab Zone").transform;
            root.SetParent(wheel, false);
            root.localPosition = rimCenter;
            root.SetAsFirstSibling();

            float hornClear = 0.09f;
            float radius = Mathf.Max(hornClear + rimThickness, rimRadius);
            float chord = 2f * radius * Mathf.Sin(Mathf.PI / rimSegments);
            wheelColliders = new Collider[rimSegments];
            for (int i = 0; i < rimSegments; i++)
            {
                var segment = new GameObject("Rim " + i);
                segment.transform.SetParent(root, false);
                segment.transform.localRotation = Quaternion.Euler(0f, 0f, 360f * i / rimSegments);
                segment.transform.localPosition = segment.transform.localRotation * Vector3.right * radius;
                var capsule = segment.AddComponent<CapsuleCollider>();
                capsule.isTrigger = true;
                capsule.direction = 1;
                capsule.radius = rimThickness;
                capsule.height = chord + 2f * rimThickness;
                wheelColliders[i] = capsule;
            }
        }
    }
}
