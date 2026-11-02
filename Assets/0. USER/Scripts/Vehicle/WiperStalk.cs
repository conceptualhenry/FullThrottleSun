using FullThrottleSun.Controller;
using UnityEngine;

namespace FullThrottleSun.Vehicle
{
    /// <summary>
    /// Lever1 stalk for windshield wipers. No thumbstick click —
    /// flick stick forward = one step faster, back = one step slower (edge-triggered).
    /// Third person: left stick. First person: grab Lever1, then that hand's stick.
    /// Lights stay on Lever2 / <see cref="LightStalk"/>.
    /// </summary>
    [DefaultExecutionOrder(-35)]
    public class WiperStalk : MonoBehaviour, IHandPinProvider
    {
        [SerializeField] WindshieldWipers wipers;
        [SerializeField] CarFollowCamera viewCamera;
        [SerializeField] SteeringWheelGrab wheelGrab;
        [SerializeField] LightStalk lightStalk;
        [SerializeField] Transform carRoot;

        [Header("Grab")]
        [SerializeField] float touchRadius = 0.055f;
        [SerializeField] float thirdPersonGrabDistance = 0.35f;

        [Header("Stick")]
        [Tooltip("Deadzone for forward / back flicks.")]
        [SerializeField] float stickThreshold = 0.65f;

        [Header("Stalk pose (visual)")]
        [SerializeField] float tiltDegrees = 14f;
        [SerializeField] float poseLerp = 12f;

        [Header("Debug")]
        [SerializeField] bool held;
        [SerializeField] ControllerHand heldHand;

        Collider leverCollider;
        Quaternion restLocalRotation;
        Vector3 pinLocalPosition;
        Quaternion pinLocalRotation = Quaternion.identity;
        bool stickWasFwd;
        bool stickWasBack;
        float tiltPose;

        public static bool IsHeld { get; private set; }
        public static WiperStalk Active { get; private set; }

        public bool IsHolding(ControllerHand hand) => held && heldHand == hand;

        public float DistanceTo(Vector3 worldPosition)
        {
            if (leverCollider != null)
                return Vector3.Distance(leverCollider.ClosestPoint(worldPosition), worldPosition);
            return Vector3.Distance(transform.position, worldPosition);
        }

        public bool Touches(Vector3 worldPosition) => Touches(worldPosition, Quaternion.identity, null);

        public bool Touches(Vector3 worldPosition, Quaternion worldRotation, SphereCollider handCollider)
        {
            bool firstPerson = viewCamera != null && viewCamera.IsFirstPerson;
            float maxDist = firstPerson ? touchRadius : thirdPersonGrabDistance;
            if (leverCollider != null)
            {
                if ((leverCollider.ClosestPoint(worldPosition) - worldPosition).sqrMagnitude <= maxDist * maxDist)
                    return true;
                if (handCollider != null
                    && Physics.ComputePenetration(
                        handCollider, worldPosition, worldRotation,
                        leverCollider, leverCollider.transform.position, leverCollider.transform.rotation,
                        out _, out _))
                    return true;
            }
            return (transform.position - worldPosition).sqrMagnitude <= maxDist * maxDist;
        }

        public bool TryGetPinnedPose(ControllerHand hand, out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = Quaternion.identity;
            if (!held || hand != heldHand)
                return false;
            position = transform.TransformPoint(pinLocalPosition);
            rotation = transform.rotation * pinLocalRotation;
            return true;
        }

        void Awake()
        {
            if (wipers == null)
                wipers = GetComponentInParent<WindshieldWipers>()
                         ?? FindAnyObjectByType<WindshieldWipers>();
            if (carRoot == null)
                carRoot = GetComponentInParent<TireFrictionCar>()?.transform ?? transform.root;
            if (viewCamera == null)
                viewCamera = FindAnyObjectByType<CarFollowCamera>();
            if (wheelGrab == null)
                wheelGrab = GetComponentInParent<SteeringWheelGrab>()
                            ?? FindAnyObjectByType<SteeringWheelGrab>();
            if (lightStalk == null)
                lightStalk = FindAnyObjectByType<LightStalk>();

            restLocalRotation = transform.localRotation;
            EnsureCollider();
            HandPinHub.Register(this);
        }

        void EnsureCollider()
        {
            leverCollider = GetComponent<Collider>();
            if (leverCollider != null)
            {
                leverCollider.isTrigger = true;
                return;
            }

            var box = gameObject.AddComponent<BoxCollider>();
            var mf = GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                box.center = mf.sharedMesh.bounds.center;
                Vector3 size = mf.sharedMesh.bounds.size;
                size = Vector3.Max(size, Vector3.one * 0.015f) * 1.1f;
                box.size = size;
            }
            else
            {
                box.size = new Vector3(0.1f, 0.035f, 0.05f);
            }
            box.isTrigger = true;
            leverCollider = box;
        }

        void OnDisable()
        {
            if (held)
                VrHandClaim.Release(heldHand, this);
            held = false;
            IsHeld = false;
            if (Active == this)
                Active = null;
            HandPinHub.Unregister(this);
            VrHandClaim.ReleaseAll(this);
        }

        void Update()
        {
            if (wipers == null)
                return;

            bool thirdPerson = viewCamera == null || !viewCamera.IsFirstPerson;
            float poseTarget = 0f;

            if (thirdPerson)
            {
                if (held)
                    held = false;
                IsHeld = false;
                if (Active == this)
                    Active = null;

                Vector2 stick = VirtualController.GetThumbstick(ControllerHand.Left);
                HandleStick(stick.y);
                if (Mathf.Abs(stick.y) > stickThreshold)
                    poseTarget = Mathf.Sign(stick.y);
            }
            else
            {
                UpdateGrab();
                IsHeld = held;
                Active = held ? this : (Active == this ? null : Active);

                if (held)
                {
                    Vector2 stick = VirtualController.GetThumbstick(heldHand);
                    HandleStick(stick.y);
                    if (Mathf.Abs(stick.y) > stickThreshold)
                        poseTarget = Mathf.Sign(stick.y);
                }
                else
                {
                    stickWasFwd = stickWasBack = false;
                }
            }

            tiltPose = Mathf.MoveTowards(tiltPose, poseTarget, 10f * Time.deltaTime);
            Quaternion target = restLocalRotation * Quaternion.Euler(tiltPose * tiltDegrees, 0f, 0f);
            transform.localRotation = Quaternion.Slerp(
                transform.localRotation, target, 1f - Mathf.Exp(-poseLerp * Time.deltaTime));
        }

        /// <summary>+Y = forward = faster one gear; −Y = back = slower one gear. Edge only.</summary>
        void HandleStick(float stickY)
        {
            bool fwd = stickY > stickThreshold;
            bool back = stickY < -stickThreshold;
            if (fwd && !stickWasFwd)
                wipers.StepFaster();
            if (back && !stickWasBack)
                wipers.StepSlower();
            stickWasFwd = fwd;
            stickWasBack = back;
        }

        void UpdateGrab()
        {
            if (!VirtualController.IsTrackedController)
            {
                held = false;
                return;
            }

            if (held)
            {
                if (!VrGrip.IsHeld(heldHand)
                    || !VirtualController.GetState(heldHand).isTracked)
                {
                    VrHandClaim.Release(heldHand, this);
                    held = false;
                }
                return;
            }

            TryGrab(ControllerHand.Left);
            if (!held)
                TryGrab(ControllerHand.Right);
        }

        void TryGrab(ControllerHand hand)
        {
            if (!VrGrip.IsDown(hand))
                return;
            if (!VrHandClaim.IsFree(hand) && !VrHandClaim.IsOwnedBy(hand, this))
                return;
            if (wheelGrab != null
                && ((hand == ControllerHand.Left && wheelGrab.LeftHolding)
                    || (hand == ControllerHand.Right && wheelGrab.RightHolding)))
                return;
            if (!TryGetHandWorld(hand, out Vector3 worldPos, out Quaternion worldRot))
                return;

            SphereCollider handCol = null;
            if (VrGrip.TryGetHand(hand, out VRHand vrHand))
                handCol = vrHand.GrabCollider;

            if (!Touches(worldPos, worldRot, handCol))
                return;

            float leverDist = DistanceTo(worldPos);
            float wheelDist = wheelGrab != null ? wheelGrab.DistanceToWheel(worldPos) : float.MaxValue;
            bool wheelTouch = wheelGrab != null && handCol != null
                              && wheelGrab.TouchesWheelWorld(worldPos, worldRot, handCol);
            if (wheelTouch && wheelDist + 0.01f < leverDist)
                return;

            // Same column as Lever2: yield when lights stalk is closer or nearly tied.
            if (lightStalk != null
                && lightStalk.Touches(worldPos, worldRot, handCol)
                && lightStalk.DistanceTo(worldPos) <= leverDist + 0.02f)
                return;

            if (!VrHandClaim.TryClaim(hand, this))
                return;

            held = true;
            heldHand = hand;
            pinLocalPosition = transform.InverseTransformPoint(worldPos);
            pinLocalRotation = Quaternion.Inverse(transform.rotation) * worldRot;
            stickWasFwd = stickWasBack = false;
        }

        bool TryGetHandWorld(ControllerHand hand, out Vector3 worldPos, out Quaternion worldRot)
        {
            worldPos = default;
            worldRot = Quaternion.identity;
            if (viewCamera == null || !viewCamera.IsHeadTracked || viewCamera.Target == null)
                return false;
            var state = VirtualController.GetState(hand);
            if (!state.isTracked)
                return false;
            if (!viewCamera.TryTrackedToTarget(state.position, state.rotation, out Vector3 localPos, out Quaternion localRot))
                return false;
            worldPos = viewCamera.Target.TransformPoint(localPos);
            worldRot = viewCamera.Target.rotation * localRot;
            return true;
        }
    }
}
