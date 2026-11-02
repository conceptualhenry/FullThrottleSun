using FullThrottleSun.Controller;
using UnityEngine;

namespace FullThrottleSun.Vehicle
{
    /// <summary>
    /// Drop this on any prop to make it grabbable with the Quest grip button using the same
    /// overlap + claim rules as the steering wheel / light stalk.
    /// While held, the object follows the hand and the visible <see cref="VRHand"/> pins to the grab pose.
    /// </summary>
    [DefaultExecutionOrder(-30)]
    public class HandGrabbable : MonoBehaviour, IHandPinProvider
    {
        [Tooltip("Colliders the hand sphere must touch. Empty = colliders on this object.")]
        [SerializeField] Collider[] grabColliders;
        [Tooltip("Higher wins when several grabbables overlap the same hand (wheel/stalk still use their own logic).")]
        [SerializeField] int priority;
        [Tooltip("If set, only this hand can grab. Leave as both by using two components, or leave default and either hand works.")]
        [SerializeField] bool allowLeft = true;
        [SerializeField] bool allowRight = true;
        [Tooltip("Keep world scale when parenting to the hand pose.")]
        [SerializeField] bool keepWorldScale = true;

        [Header("Debug")]
        [SerializeField] bool held;
        [SerializeField] ControllerHand heldHand;

        Vector3 localPos;
        Quaternion localRot = Quaternion.identity;
        Vector3 pinLocalPos;
        Quaternion pinLocalRot = Quaternion.identity;
        Vector3 cachedScale;

        public bool IsHeld => held;
        public ControllerHand HeldHand => heldHand;
        public int Priority => priority;

        void Awake()
        {
            if (grabColliders == null || grabColliders.Length == 0)
                grabColliders = GetComponentsInChildren<Collider>();
            cachedScale = transform.lossyScale;
        }

        void OnEnable() => HandPinHub.Register(this);

        void OnDisable()
        {
            if (held)
                Release();
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

                // Keep the prop glued to the hand at the offset captured on grab.
                transform.SetPositionAndRotation(pos + rot * localPos, rot * localRot);
                if (keepWorldScale)
                    SetLossyScale(cachedScale);
                return;
            }

            TryBeginGrab(ControllerHand.Left);
            if (!held)
                TryBeginGrab(ControllerHand.Right);
        }

        void TryBeginGrab(ControllerHand hand)
        {
            if (hand == ControllerHand.Left && !allowLeft)
                return;
            if (hand == ControllerHand.Right && !allowRight)
                return;
            if (!VrGrip.IsDown(hand))
                return;
            if (!VrHandClaim.IsFree(hand))
                return;
            if (!VrGrip.TryGetHand(hand, out VRHand vrHand) || vrHand.GrabCollider == null)
                return;
            if (!VrGrip.TryGetWorldPose(hand, out Vector3 pos, out Quaternion rot))
                return;
            if (!GrabOverlap.TouchesAny(vrHand.GrabCollider, pos, rot, grabColliders))
                return;

            // Yield if cockpit systems already own a closer grip this frame (they run earlier).
            var wheel = FindAnyObjectByType<SteeringWheelGrab>();
            if (wheel != null
                && ((hand == ControllerHand.Left && wheel.LeftHolding)
                    || (hand == ControllerHand.Right && wheel.RightHolding)))
                return;
            if (LightStalk.Active != null && LightStalk.Active.IsHolding(hand))
                return;

            if (!VrHandClaim.TryClaim(hand, this))
                return;

            held = true;
            heldHand = hand;
            // Offset from hand → object at grab time.
            localPos = Quaternion.Inverse(rot) * (transform.position - pos);
            localRot = Quaternion.Inverse(rot) * transform.rotation;
            pinLocalPos = transform.InverseTransformPoint(pos);
            pinLocalRot = Quaternion.Inverse(transform.rotation) * rot;
            cachedScale = transform.lossyScale;
        }

        void Release()
        {
            if (!held)
                return;
            VrHandClaim.Release(heldHand, this);
            held = false;
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
