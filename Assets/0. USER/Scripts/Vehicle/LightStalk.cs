using FullThrottleSun.Controller;
using UnityEngine;

namespace FullThrottleSun.Vehicle
{
    /// <summary>
    /// Light stalk. Third person: left stick always controls lights (no grab).
    /// First person: grab Lever2, then that hand's stick —
    /// forward/back = high/low beam (hold to flash-toggle), right/left = turn signals.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public class LightStalk : MonoBehaviour
    {
        [SerializeField] CarLights lights;
        [SerializeField] CarFollowCamera viewCamera;
        [SerializeField] SteeringWheelGrab wheelGrab;
        [SerializeField] Transform carRoot;

        [Header("Grab")]
        [SerializeField] float touchRadius = 0.08f;
        [SerializeField] float thirdPersonGrabDistance = 0.35f;

        [Header("Stick")]
        [Tooltip("Deadzone for turn (X) and beam (Y) flicks.")]
        [SerializeField] float stickThreshold = 0.65f;

        [Header("Hand — beam")]
        [Tooltip("How far (m) to push/pull the hand along the car to flick high/low.")]
        [SerializeField] float beamPushMeters = 0.04f;
        [Tooltip("Seconds after a beam flick before holding that pose starts high↔low toggling.")]
        [SerializeField] float beamHoldDelay = 0.2f;
        [SerializeField] float beamToggleInterval = 0.25f;

        [Header("Stalk pose (visual)")]
        [SerializeField] float turnTiltDegrees = 18f;
        [SerializeField] float beamTiltDegrees = 12f;
        [SerializeField] float poseLerp = 12f;

        [Header("Debug")]
        [SerializeField] bool held;
        [SerializeField] ControllerHand heldHand;

        Collider leverCollider;
        Quaternion restLocalRotation;
        bool stickWasUp, stickWasDown;
        Vector3 grabHandLocal;
        Vector3 pinLocalPosition;
        Quaternion pinLocalRotation = Quaternion.identity;
        bool beamWasFwd, beamWasBack;
        float beamHoldTimer = -1f;
        int beamHoldDir;
        float beamToggleTimer;

        public static bool IsHeld { get; private set; }
        public static LightStalk Active { get; private set; }

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

        /// <summary>World pose the hand should freeze at while gripping the stalk.</summary>
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
            if (lights == null)
                lights = GetComponentInParent<CarLights>();
            if (carRoot == null)
                carRoot = GetComponentInParent<TireFrictionCar>()?.transform ?? transform.root;
            if (viewCamera == null)
                viewCamera = FindAnyObjectByType<CarFollowCamera>();
            if (wheelGrab == null)
                wheelGrab = GetComponentInParent<SteeringWheelGrab>()
                            ?? FindAnyObjectByType<SteeringWheelGrab>();

            restLocalRotation = transform.localRotation;
            EnsureCollider();
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
                size = Vector3.Max(size, Vector3.one * 0.02f) * 1.5f;
                box.size = size;
            }
            else
            {
                box.size = new Vector3(0.12f, 0.04f, 0.06f);
            }
            box.isTrigger = true;
            leverCollider = box;
        }

        void OnDisable()
        {
            held = false;
            IsHeld = false;
            if (Active == this)
                Active = null;
        }

        void Update()
        {
            if (lights == null)
                return;

            bool thirdPerson = viewCamera == null || !viewCamera.IsFirstPerson;

            float turnPose = 0f;
            float beamPose = 0f;

            if (thirdPerson)
            {
                // Free left stick — no grab. Drop any first-person grip.
                if (held)
                    held = false;
                IsHeld = false;
                if (Active == this)
                    Active = null;

                Vector2 stick = VirtualController.GetThumbstick(ControllerHand.Left);
                HandleTurn(stick.x);
                HandleBeam(stick.y);
                turnPose = Mathf.Abs(stick.x) > stickThreshold ? Mathf.Sign(stick.x) : 0f;
                beamPose = Mathf.Abs(stick.y) > stickThreshold ? Mathf.Sign(stick.y) : 0f;
            }
            else
            {
                UpdateGrab();
                IsHeld = held;
                Active = held ? this : (Active == this ? null : Active);

                if (held)
                {
                    Vector2 stick = VirtualController.GetThumbstick(heldHand);
                    HandleTurn(stick.x);
                    HandleBeam(stick.y);
                    turnPose = Mathf.Abs(stick.x) > stickThreshold ? Mathf.Sign(stick.x) : 0f;
                    beamPose = Mathf.Abs(stick.y) > stickThreshold ? Mathf.Sign(stick.y) : 0f;
                }
                else
                {
                    stickWasUp = stickWasDown = false;
                    beamWasFwd = beamWasBack = false;
                    beamHoldTimer = -1f;
                    beamHoldDir = 0;
                }
            }

            Quaternion target = restLocalRotation
                                * Quaternion.Euler(beamPose * beamTiltDegrees, 0f, -turnPose * turnTiltDegrees);
            transform.localRotation = Quaternion.Slerp(
                transform.localRotation, target, 1f - Mathf.Exp(-poseLerp * Time.deltaTime));
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
                if (!VirtualController.GetButton(ControllerButton.Grip, heldHand)
                    || !VirtualController.GetState(heldHand).isTracked)
                    held = false;
                return;
            }

            TryGrab(ControllerHand.Left);
            if (!held)
                TryGrab(ControllerHand.Right);
        }

        void TryGrab(ControllerHand hand)
        {
            if (!VirtualController.GetButtonDown(ControllerButton.Grip, hand))
                return;
            if (wheelGrab != null
                && ((hand == ControllerHand.Left && wheelGrab.LeftHolding)
                    || (hand == ControllerHand.Right && wheelGrab.RightHolding)))
                return;
            if (!TryGetHandWorld(hand, out Vector3 worldPos, out Quaternion worldRot))
                return;

            SphereCollider handCol = null;
            foreach (var vrHand in FindObjectsByType<VRHand>())
                if (vrHand.GrabCollider != null)
                {
                    handCol = vrHand.GrabCollider;
                    break;
                }

            if (!Touches(worldPos, worldRot, handCol))
                return;

            // Prefer whichever surface is closer to the hand centre (hands are large and overlap both).
            float leverDist = DistanceTo(worldPos);
            float wheelDist = wheelGrab != null ? wheelGrab.DistanceToWheel(worldPos) : float.MaxValue;
            bool wheelTouch = wheelGrab != null && handCol != null
                              && wheelGrab.TouchesWheelWorld(worldPos, worldRot, handCol);
            if (wheelTouch && wheelDist + 0.01f < leverDist)
                return;

            held = true;
            heldHand = hand;
            grabHandLocal = carRoot.InverseTransformPoint(worldPos);
            pinLocalPosition = transform.InverseTransformPoint(worldPos);
            pinLocalRotation = Quaternion.Inverse(transform.rotation) * worldRot;
            beamHoldTimer = -1f;
            beamHoldDir = 0;
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

        void HandleTurn(float stickY)
        {
            bool up = stickY > stickThreshold;
            bool down = stickY < -stickThreshold;
            if (up && !stickWasUp)
                lights.PulseTurn(CarLights.TurnSignal.Right);
            if (down && !stickWasDown)
                lights.PulseTurn(CarLights.TurnSignal.Left);
            stickWasUp = up;
            stickWasDown = down;
        }

        void HandleBeam(float axis)
        {
            // + = 前 / 远光, − = 后 / 近光
            bool fwd = axis > stickThreshold;
            bool back = axis < -stickThreshold;

            if (fwd && !beamWasFwd)
            {
                lights.SetBeam(CarLights.BeamMode.High);
                beamHoldDir = 1;
                beamHoldTimer = beamHoldDelay;
                beamToggleTimer = 0f;
            }
            if (back && !beamWasBack)
            {
                lights.SetBeam(CarLights.BeamMode.Low);
                beamHoldDir = -1;
                beamHoldTimer = beamHoldDelay;
                beamToggleTimer = 0f;
            }

            bool holding = (beamHoldDir > 0 && fwd) || (beamHoldDir < 0 && back);
            if (!holding)
            {
                beamHoldTimer = -1f;
            }
            else if (beamHoldTimer >= 0f)
            {
                beamHoldTimer -= Time.deltaTime;
                if (beamHoldTimer <= 0f)
                {
                    beamToggleTimer -= Time.deltaTime;
                    if (beamToggleTimer <= 0f)
                    {
                        lights.ToggleBeamHighLow();
                        beamToggleTimer = beamToggleInterval;
                    }
                }
            }

            beamWasFwd = fwd;
            beamWasBack = back;
        }
    }
}
