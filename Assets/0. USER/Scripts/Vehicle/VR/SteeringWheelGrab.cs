using FullThrottleSun.Controller;
using UnityEngine;

namespace FullThrottleSun.Vehicle
{
    /// <summary>
    /// Lets tracked controllers (Meta Quest) grab the cockpit steering wheel. A hand touching the wheel's colliders grabs
    /// when the grip (side trigger) is pressed, stays pinned where it grabbed and turns the wheel:
    /// one hand by the controller's own rotation around the steering column, two hands by the angle of the line between
    /// them. With no hand on it the wheel eases back to centre. Needs the headset on (first-person only).
    /// Grab volume: assign scene-authored colliders under <c>Grab Zone</c> (editable in the Inspector).
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(TireFrictionCar))]
    public class SteeringWheelGrab : MonoBehaviour, IVrHandPoseSource, IHandPinProvider
    {
        [SerializeField] CarFollowCamera viewCamera;

        [Header("Grab zone")]
        [Tooltip("Scene-authored grab colliders (usually Grab Zone / Rim * capsules). Assigned in the editor — never built at runtime.")]
        [SerializeField] Collider[] wheelColliders;
        [Tooltip("Fallback ring radius used only for DistanceToWheel when no collider closest-point is available.")]
        [Min(0.01f)] [SerializeField] float rimRadius = 0.2f;

        [Header("Steering")]
        [Tooltip("Degrees per second the wheel turns back to centre with no hand on it.")]
        [Min(0f)] [SerializeField] float returnSpeed = 90f;
        [Tooltip("Two hands closer than this (m) give no usable line between them; the first hand steers alone.")]
        [Min(0.01f)] [SerializeField] float minHandSpacing = 0.1f;

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
            ResolveWheelColliders();

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
            firstPerson = active && viewCamera.IsFirstPerson;

            bool recentered = viewCamera != null && viewCamera.CalibrationVersion != calibrationVersion;
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
                // While gripping the rim, stay locked — never yield to the horn mid-turn.
                if (!h.tracked || !VrGrip.IsHeld(hand))
                {
                    h.holding = false;
                    VrHandClaim.Release(hand, this);
                }
            }
            else if (h.tracked && firstPerson && VrGrip.IsDown(hand) && TouchesWheel(h))
            {
                Vector3 world = transform.TransformPoint(h.position);
                Quaternion worldRot = transform.rotation * h.rotation;
                var handCol = h.view != null ? h.view.GrabCollider : null;
                if (PreferCloserOverWheel(hand, world, handCol, worldRot))
                    return;
                Grab(h, hand);
            }
        }

        bool PreferCloserOverWheel(
            ControllerHand hand, Vector3 world, SphereCollider handCol, Quaternion worldRot = default)
        {
            float wheelDist = DistanceToWheel(world);
            if (lightStalk != null && lightStalk.Touches(world, worldRot, handCol)
                && lightStalk.DistanceTo(world) < wheelDist - 0.01f)
                return true;
            if (wiperStalk != null && wiperStalk.Touches(world, worldRot, handCol)
                && wiperStalk.DistanceTo(world) < wheelDist - 0.01f)
                return true;
            if (VrGrabBroker.PreferFreeOverCockpit(hand, world, worldRot, handCol, wheelDist))
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
        /// Uses inspector-assigned colliders, or finds an existing scene <c>Grab Zone</c>. Never creates colliders.
        /// </summary>
        void ResolveWheelColliders()
        {
            if (wheelColliders != null && wheelColliders.Length > 0)
            {
                bool any = false;
                for (int i = 0; i < wheelColliders.Length; i++)
                {
                    if (wheelColliders[i] != null)
                    {
                        any = true;
                        break;
                    }
                }
                if (any)
                    return;
            }

            Transform existing = wheel != null ? wheel.Find("Grab Zone") : null;
            if (existing != null)
            {
                var found = existing.GetComponentsInChildren<Collider>(true);
                if (found != null && found.Length > 0)
                {
                    wheelColliders = found;
                    return;
                }
            }

            Debug.LogWarning(
                "[SteeringWheelGrab] No grab colliders assigned. Add CapsuleColliders under Grab Zone in the scene.",
                this);
            wheelColliders = System.Array.Empty<Collider>();
        }
    }
}
