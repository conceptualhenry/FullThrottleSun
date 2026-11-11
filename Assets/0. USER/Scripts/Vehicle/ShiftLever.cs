using FullThrottleSun.Controller;
using UnityEngine;

namespace FullThrottleSun.Vehicle
{
    /// <summary>
    /// Automatic R / N / D shifter.
    /// Grab <c>Shift_Lever</c>, then push/pull the hand (or flick that hand's stick)
    /// to step R ↔ N ↔ D. Keyboard: 1 = R, 2 = N, 3 = D.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public class ShiftLever : MonoBehaviour, IHandPinProvider
    {
        [SerializeField] TireFrictionCar car;
        [SerializeField] CarFollowCamera viewCamera;
        [SerializeField] SteeringWheelGrab wheelGrab;
        [SerializeField] Transform carRoot;

        [Header("Grab")]
        [SerializeField] float touchRadius = 0.12f;

        [Header("Hand throw (car-local Z)")]
        [Tooltip("Hand travel from Neutral toward Drive / Reverse before a step fires (m).")]
        [SerializeField] float handStepMeters = 0.045f;
        [SerializeField] float stickThreshold = 0.65f;

        [Header("Lever pose (visual)")]
        [Tooltip("Tip angle at Drive (+) / Reverse (−). Neutral is 0.")]
        [SerializeField] float tiltDegrees = 18f;
        [SerializeField] float poseLerp = 14f;

        [Header("Start")]
        [SerializeField] TireFrictionCar.AutoGear startGear = TireFrictionCar.AutoGear.Drive;

        [Header("Debug")]
        [SerializeField] bool held;
        [SerializeField] ControllerHand heldHand;
        [SerializeField] TireFrictionCar.AutoGear gear;

        Collider leverCollider;
        Quaternion restLocalRotation;
        Vector3 pinLocalPosition;
        Quaternion pinLocalRotation = Quaternion.identity;
        Vector3 grabHandCarLocal;
        float handAccum;
        bool stickWasFwd;
        bool stickWasBack;
        float tiltPose;

        public static ShiftLever Active { get; private set; }

        public bool IsHolding(ControllerHand hand) => held && heldHand == hand;
        public TireFrictionCar.AutoGear Gear => gear;

        public float DistanceTo(Vector3 worldPosition)
        {
            if (leverCollider != null)
                return Vector3.Distance(leverCollider.ClosestPoint(worldPosition), worldPosition);
            return Vector3.Distance(transform.position, worldPosition);
        }

        public bool Touches(Vector3 worldPosition, Quaternion worldRotation, SphereCollider handCollider)
        {
            float maxDist = touchRadius;
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
            ResolveRefs();
            restLocalRotation = transform.localRotation;
            gear = startGear;
            tiltPose = PoseForGear(gear);
            transform.localRotation = restLocalRotation * Quaternion.Euler(tiltPose * tiltDegrees, 0f, 0f);
            ApplyGearToCar();
            ResolveCollider();
            HandPinHub.Register(this);
        }

        void ResolveRefs()
        {
            if (car == null)
                car = GetComponentInParent<TireFrictionCar>();
            if (carRoot == null)
                carRoot = car != null ? car.transform : transform.root;
            if (viewCamera == null)
                viewCamera = FindAnyObjectByType<CarFollowCamera>();
            if (wheelGrab == null)
                wheelGrab = GetComponentInParent<SteeringWheelGrab>()
                            ?? FindAnyObjectByType<SteeringWheelGrab>();
        }

        void ResolveCollider()
        {
            leverCollider = GetComponent<Collider>();
            if (leverCollider == null)
                Debug.LogWarning("[ShiftLever] No Collider on Shift_Lever — add one in the editor (trigger recommended).", this);
        }

        void OnDisable()
        {
            if (held)
                VrHandClaim.Release(heldHand, this);
            held = false;
            if (Active == this)
                Active = null;
            HandPinHub.Unregister(this);
            VrHandClaim.ReleaseAll(this);
        }

        void Update()
        {
            if (car == null || viewCamera == null)
                ResolveRefs();

            HandleKeyboard();

            float poseTarget = PoseForGear(gear);

            if (VirtualController.IsTrackedController)
                UpdateGrab();
            else if (held)
            {
                VrHandClaim.Release(heldHand, this);
                held = false;
            }

            Active = held ? this : (Active == this ? null : Active);

            if (held && TryGetHandWorld(heldHand, out Vector3 worldPos, out _))
            {
                HandleHandThrow(worldPos);
                Vector2 stick = VirtualController.GetThumbstick(heldHand);
                HandleStick(stick.y);
                if (Mathf.Abs(stick.y) > stickThreshold)
                    poseTarget = Mathf.Sign(stick.y);
                else if (Mathf.Abs(handAccum) > 0.01f)
                    poseTarget = Mathf.Clamp(handAccum / Mathf.Max(0.01f, handStepMeters), -1f, 1f);
            }
            else
            {
                stickWasFwd = stickWasBack = false;
                handAccum = 0f;
            }

            tiltPose = Mathf.MoveTowards(tiltPose, poseTarget, 10f * Time.deltaTime);
            Quaternion target = restLocalRotation * Quaternion.Euler(tiltPose * tiltDegrees, 0f, 0f);
            transform.localRotation = Quaternion.Slerp(
                transform.localRotation, target, 1f - Mathf.Exp(-poseLerp * Time.deltaTime));
        }

        void HandleHandThrow(Vector3 worldPos)
        {
            if (carRoot == null)
                return;

            Vector3 now = carRoot.InverseTransformPoint(worldPos);
            // Automatic gate: forward (car +Z) toward Drive, back toward Reverse.
            float delta = now.z - grabHandCarLocal.z;
            grabHandCarLocal = now;
            handAccum += delta;

            float step = Mathf.Max(0.02f, handStepMeters);
            while (handAccum >= step)
            {
                handAccum -= step;
                StepGear(+1);
            }
            while (handAccum <= -step)
            {
                handAccum += step;
                StepGear(-1);
            }
        }

        void HandleStick(float stickY)
        {
            bool fwd = stickY > stickThreshold;
            bool back = stickY < -stickThreshold;
            if (fwd && !stickWasFwd)
                StepGear(+1);
            if (back && !stickWasBack)
                StepGear(-1);
            stickWasFwd = fwd;
            stickWasBack = back;
        }

        void StepGear(int delta)
        {
            int next = Mathf.Clamp((int)gear + delta, (int)TireFrictionCar.AutoGear.Reverse, (int)TireFrictionCar.AutoGear.Drive);
            SetGear((TireFrictionCar.AutoGear)next);
        }

        void HandleKeyboard()
        {
            if (SettingsMenu.IsOpen || KeyboardBindings.IsListening)
                return;
            if (KeyboardBindings.WasPressedThisFrame(KeyboardAction.GearReverse))
                SetGear(TireFrictionCar.AutoGear.Reverse);
            else if (KeyboardBindings.WasPressedThisFrame(KeyboardAction.GearNeutral))
                SetGear(TireFrictionCar.AutoGear.Neutral);
            else if (KeyboardBindings.WasPressedThisFrame(KeyboardAction.GearDrive))
                SetGear(TireFrictionCar.AutoGear.Drive);
        }

        public void SetGear(TireFrictionCar.AutoGear next)
        {
            gear = next;
            ApplyGearToCar();
        }

        void ApplyGearToCar()
        {
            if (car != null)
                car.SetGear(gear);
        }

        static float PoseForGear(TireFrictionCar.AutoGear g)
        {
            switch (g)
            {
                case TireFrictionCar.AutoGear.Drive: return 1f;
                case TireFrictionCar.AutoGear.Reverse: return -1f;
                default: return 0f;
            }
        }

        void UpdateGrab()
        {
            if (held)
            {
                if (!VrGrip.IsHeld(heldHand)
                    || !VirtualController.GetState(heldHand).isTracked)
                {
                    VrHandClaim.Release(heldHand, this);
                    held = false;
                    handAccum = 0f;
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
            if (LightStalk.Active != null && LightStalk.Active.IsHolding(hand))
                return;
            if (WiperStalk.Active != null && WiperStalk.Active.IsHolding(hand))
                return;
            if (!TryGetHandWorld(hand, out Vector3 worldPos, out Quaternion worldRot))
                return;

            SphereCollider handCol = null;
            if (VrGrip.TryGetHand(hand, out VRHand vrHand))
                handCol = vrHand.GrabCollider;

            if (!Touches(worldPos, worldRot, handCol))
                return;

            if (VrGrabBroker.PreferFreeOverCockpit(hand, worldPos, worldRot, handCol, DistanceTo(worldPos)))
                return;

            if (!VrHandClaim.TryClaim(hand, this))
                return;

            held = true;
            heldHand = hand;
            pinLocalPosition = transform.InverseTransformPoint(worldPos);
            pinLocalRotation = Quaternion.Inverse(transform.rotation) * worldRot;
            grabHandCarLocal = carRoot != null ? carRoot.InverseTransformPoint(worldPos) : worldPos;
            handAccum = 0f;
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
