using FullThrottleSun.Controller;
using UnityEngine;

namespace FullThrottleSun.Vehicle
{
    /// <summary>
    /// Free prop grab (soda can, etc.): overlap the hand sphere, then press Grip.
    /// When several free props (and/or cockpit controls) overlap, <see cref="VrGrabBroker"/>
    /// awards the grab to whichever surface is closest to the hand centre.
    /// While held, the object follows the hand (kinematic). On release it becomes a free
    /// rigidbody with throw velocity so it collides and falls normally.
    /// </summary>
    [DefaultExecutionOrder(-30)]
    public class HandGrabbable : MonoBehaviour, IHandPinProvider
    {
        [Tooltip("Colliders the hand sphere must touch. Empty = colliders on this object / children.")]
        [SerializeField] Collider[] grabColliders;
        [Tooltip("Tie-break only among free props at the same distance. Higher wins.")]
        [SerializeField] int priority;
        [SerializeField] bool allowLeft = true;
        [SerializeField] bool allowRight = true;
        [Tooltip("Keep world scale while the hand drives the pose.")]
        [SerializeField] bool keepWorldScale = true;
        [Tooltip("Unparent on grab and enable Rigidbody physics on release.")]
        [SerializeField] bool physicsOnRelease = true;
        [Tooltip("Scales hand motion into release linear velocity.")]
        [SerializeField] float throwVelocityScale = 1.25f;
        [SerializeField] float throwAngularScale = 1f;
        [SerializeField] float maxThrowSpeed = 12f;

        [Header("Debug")]
        [SerializeField] bool held;
        [SerializeField] ControllerHand heldHand;

        Rigidbody rb;
        Vector3 localPos;
        Quaternion localRot = Quaternion.identity;
        Vector3 pinLocalPos;
        Quaternion pinLocalRot = Quaternion.identity;
        Vector3 cachedScale;
        Vector3 prevWorldPos;
        Quaternion prevWorldRot = Quaternion.identity;
        Vector3 throwVelocity;
        Vector3 throwAngularVelocity;
        bool hasMotionSample;

        public bool IsHeld => held;
        public ControllerHand HeldHand => heldHand;
        public int Priority => priority;

        public bool IsEligibleToGrab(ControllerHand hand)
        {
            if (held)
                return false;
            if (hand == ControllerHand.Left && !allowLeft)
                return false;
            if (hand == ControllerHand.Right && !allowRight)
                return false;
            return isActiveAndEnabled;
        }

        public bool TouchesHand(Vector3 handPosition, Quaternion handRotation, SphereCollider handCollider)
            => GrabOverlap.TouchesAny(handCollider, handPosition, handRotation, grabColliders);

        public float DistanceToHand(Vector3 handPosition)
        {
            float best = float.MaxValue;
            if (grabColliders == null)
                return best;
            for (int i = 0; i < grabColliders.Length; i++)
            {
                float d = GrabOverlap.DistanceToCollider(grabColliders[i], handPosition);
                if (d < best)
                    best = d;
            }
            return best;
        }

        void Awake()
        {
            if (grabColliders == null || grabColliders.Length == 0)
                grabColliders = GetComponentsInChildren<Collider>();
            cachedScale = transform.lossyScale;

            rb = GetComponent<Rigidbody>();
            if (physicsOnRelease && rb == null)
                rb = gameObject.AddComponent<Rigidbody>();
            if (rb != null)
            {
                // Sit still on the car until first grab; free physics starts on release.
                rb.isKinematic = true;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }
        }

        void OnEnable()
        {
            VrGrabBroker.Register(this);
            HandPinHub.Register(this);
        }

        void OnDisable()
        {
            if (held)
                Release();
            VrGrabBroker.Unregister(this);
            HandPinHub.Unregister(this);
            VrHandClaim.ReleaseAll(this);
        }

        void Update()
        {
            if (held)
            {
                if (!VrGrip.IsHeld(heldHand) || !VrGrip.TryGetWorldPose(heldHand, out Vector3 pos, out Quaternion rot))
                {
                    Release();
                    return;
                }

                Vector3 worldPos = pos + rot * localPos;
                Quaternion worldRot = rot * localRot;
                SampleThrowMotion(worldPos, worldRot);

                if (rb != null)
                {
                    rb.MovePosition(worldPos);
                    rb.MoveRotation(worldRot);
                }
                else
                {
                    transform.SetPositionAndRotation(worldPos, worldRot);
                }

                if (keepWorldScale)
                    SetLossyScale(cachedScale);
                return;
            }

            TryBeginIfSelected(ControllerHand.Left);
            if (!held)
                TryBeginIfSelected(ControllerHand.Right);
        }

        void TryBeginIfSelected(ControllerHand hand)
        {
            if (!VrGrabBroker.ShouldFreeGrabBegin(this, hand))
                return;
            if (!VrGrip.TryGetWorldPose(hand, out Vector3 pos, out Quaternion rot))
                return;
            if (!VrHandClaim.TryClaim(hand, this))
                return;

            held = true;
            heldHand = hand;
            cachedScale = transform.lossyScale;

            // World-space free body while held / after release (parented RB under a moving car is unstable).
            if (physicsOnRelease && transform.parent != null)
                transform.SetParent(null, true);

            if (rb != null)
            {
                rb.isKinematic = true;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            localPos = Quaternion.Inverse(rot) * (transform.position - pos);
            localRot = Quaternion.Inverse(rot) * transform.rotation;
            pinLocalPos = transform.InverseTransformPoint(pos);
            pinLocalRot = Quaternion.Inverse(transform.rotation) * rot;

            prevWorldPos = transform.position;
            prevWorldRot = transform.rotation;
            throwVelocity = Vector3.zero;
            throwAngularVelocity = Vector3.zero;
            hasMotionSample = false;
        }

        void Release()
        {
            if (!held)
                return;

            ControllerHand hand = heldHand;
            held = false;
            VrHandClaim.Release(hand, this);

            if (!physicsOnRelease || rb == null)
                return;

            if (transform.parent != null)
                transform.SetParent(null, true);

            rb.isKinematic = false;
            Vector3 v = Vector3.ClampMagnitude(throwVelocity * throwVelocityScale, maxThrowSpeed);
            rb.linearVelocity = v;
            rb.angularVelocity = throwAngularVelocity * throwAngularScale;
        }

        void SampleThrowMotion(Vector3 worldPos, Quaternion worldRot)
        {
            float dt = Time.deltaTime;
            if (dt < 1e-5f)
                return;

            if (hasMotionSample)
            {
                throwVelocity = (worldPos - prevWorldPos) / dt;
                Quaternion delta = worldRot * Quaternion.Inverse(prevWorldRot);
                delta.ToAngleAxis(out float angleDeg, out Vector3 axis);
                if (angleDeg > 180f)
                    angleDeg -= 360f;
                if (axis.sqrMagnitude > 1e-8f)
                    throwAngularVelocity = axis.normalized * (angleDeg * Mathf.Deg2Rad / dt);
                else
                    throwAngularVelocity = Vector3.zero;
            }

            prevWorldPos = worldPos;
            prevWorldRot = worldRot;
            hasMotionSample = true;
        }

        public bool TryGetPinnedPose(ControllerHand hand, out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = Quaternion.identity;
            if (!held || hand != heldHand)
                return false;
            position = transform.TransformPoint(pinLocalPos);
            rotation = transform.rotation * pinLocalRot;
            return true;
        }

        void SetLossyScale(Vector3 worldScale)
        {
            Transform parent = transform.parent;
            Vector3 parentScale = parent != null ? parent.lossyScale : Vector3.one;
            transform.localScale = new Vector3(
                ApproxDiv(worldScale.x, parentScale.x),
                ApproxDiv(worldScale.y, parentScale.y),
                ApproxDiv(worldScale.z, parentScale.z));
        }

        static float ApproxDiv(float a, float b) => Mathf.Abs(b) < 1e-5f ? a : a / b;
    }
}
