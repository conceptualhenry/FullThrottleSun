using FullThrottleSun.Controller;
using UnityEngine;

namespace FullThrottleSun.Vehicle
{
    /// <summary>
    /// Lets tracked controllers (Meta Quest) grab the cockpit steering wheel. A hand touching the wheel's colliders grabs
    /// when the grip (side trigger) is pressed, stays pinned where it grabbed and turns the wheel:
    /// one hand by the controller's own rotation around the steering column, two hands by the angle of the line between
    /// them. With no hand on it the wheel eases back to centre. Only active in first person with the headset on.
    /// Spawns the hands, and a ring of trigger colliders around the rim when none are assigned.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(TireFrictionCar))]
    public class SteeringWheelGrab : MonoBehaviour
    {
        [SerializeField] CarFollowCamera viewCamera;

        [Header("Grab zone")]
        [Tooltip("Colliders a hand must touch to grab. Empty = a ring of trigger capsules generated around the rim.")]
        [SerializeField] Collider[] wheelColliders;
        [Tooltip("Generated ring: rim radius (m), measured to the middle of the rim.")]
        [Min(0.01f)] [SerializeField] float rimRadius = 0.18f;
        [Tooltip("Generated ring: tube radius (m). Larger = easier to grab.")]
        [Min(0.005f)] [SerializeField] float rimThickness = 0.035f;
        [Range(6, 32)] [SerializeField] int rimSegments = 12;

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
        [Min(0.01f)] [SerializeField] float handRadius = 0.05f;

        [Header("Debug (read only)")]
        [Tooltip("Steering wheel angle in degrees, right positive.")]
        [SerializeField] float wheelAngle;
        [SerializeField] bool leftHolding;
        [SerializeField] bool rightHolding;

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
            if (wheelColliders == null || wheelColliders.Length == 0)
                BuildRimColliders();

            for (int i = 0; i < hands.Length; i++)
            {
                var hand = (ControllerHand)i;
                var go = new GameObject(hand == ControllerHand.Left ? "[VR Hand L]" : "[VR Hand R]");
                hands[i].view = go.AddComponent<VRHand>();
                hands[i].view.Init(this, hand, hand == ControllerHand.Left ? leftHandModel : rightHandModel, handRadius);
            }
        }

        void OnDisable()
        {
            foreach (var hand in hands)
            {
                hand.holding = false;
                hand.tracked = false;
            }
            if (car != null)
                car.SetWheelInput(0f, false);
        }

        void OnDestroy()
        {
            foreach (var hand in hands)
                if (hand.view != null)
                    Destroy(hand.view.gameObject);
        }

        void Update()
        {
            bool active = viewCamera != null && VirtualController.IsTrackedController
                          && viewCamera.IsHeadTracked && viewCamera.IsFirstPerson;

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

        /// <summary>Where the real controller is, in world space.</summary>
        public bool TryGetTrackedPose(ControllerHand hand, out Vector3 position, out Quaternion rotation)
        {
            var h = hands[(int)hand];
            position = transform.TransformPoint(h.position);
            rotation = transform.rotation * h.rotation;
            return enabled && h.tracked;
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

            if (h.holding)
            {
                if (!h.tracked || !VirtualController.GetButton(ControllerButton.Grip, hand))
                    h.holding = false;
            }
            else if (h.tracked && VirtualController.GetButtonDown(ControllerButton.Grip, hand) && TouchesWheel(h))
            {
                Grab(h);
            }
        }

        bool TouchesWheel(Hand h)
        {
            // Posed explicitly: collider poses synced to physics lag the interpolated car by up to a physics step.
            var handCollider = h.view.GrabCollider;
            Vector3 position = transform.TransformPoint(h.position);
            Quaternion rotation = transform.rotation * h.rotation;
            foreach (var c in wheelColliders)
            {
                if (c == null || !c.enabled || !c.gameObject.activeInHierarchy)
                    continue;
                if (Physics.ComputePenetration(handCollider, position, rotation,
                        c, c.transform.position, c.transform.rotation, out _, out _))
                    return true;
            }
            return false;
        }

        void Grab(Hand h)
        {
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

        void BuildRimColliders()
        {
            var root = new GameObject("Grab Zone").transform;
            root.SetParent(wheel, false);
            var mesh = wheel.GetComponent<MeshFilter>();
            if (mesh != null && mesh.sharedMesh != null)
                root.localPosition = new Vector3(0f, 0f, mesh.sharedMesh.bounds.center.z);

            int ignoreRaycast = LayerMask.NameToLayer("Ignore Raycast");
            root.gameObject.layer = ignoreRaycast;
            float chord = 2f * rimRadius * Mathf.Sin(Mathf.PI / rimSegments);
            wheelColliders = new Collider[rimSegments];
            for (int i = 0; i < rimSegments; i++)
            {
                var segment = new GameObject("Rim " + i);
                segment.layer = ignoreRaycast;
                segment.transform.SetParent(root, false);
                // Each capsule lies along its local Y, tangent to the rim.
                segment.transform.localRotation = Quaternion.Euler(0f, 0f, 360f * i / rimSegments);
                segment.transform.localPosition = segment.transform.localRotation * Vector3.right * rimRadius;
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
