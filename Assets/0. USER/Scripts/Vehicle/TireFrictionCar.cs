using FullThrottleSun.Controller;
using UnityEngine;

namespace FullThrottleSun.Vehicle
{
    /// <summary>
    /// Car driven purely by tyre-ground friction. Each wheel is a raycast suspension; the tyre force comes from
    /// longitudinal slip ratio and lateral slip angle, limited by a friction circle (μ × load × surface grip).
    /// A wheel entry is one spinning body: give it several contact offsets when one mesh is a solid axle.
    ///
    /// Input through VirtualController. PlayStation pad: R2 throttle, L2 brake / reverse, Cross handbrake.
    /// Steering: DualSense gyro (Options to bind) and/or the cockpit wheel via SteeringWheelGrab (Quest).
    /// Left stick is not used for steering — in third person it drives the light stalk instead.
    /// The steering wheel always shows the input (full lock = steeringWheelLock); speed only shrinks how far the front
    /// wheels follow it, like a variable steering ratio.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class TireFrictionCar : MonoBehaviour
    {
        public enum GyroGrip { Upright, Flat }

        [System.Serializable]
        public class Wheel
        {
            public string name;
            public Transform visual;
            [Tooltip("Tyre contact points along the car's right axis, relative to the visual's pivot. One entry per tyre on the ground.")]
            public float[] contactOffsets = { 0f };
            public bool steer;
            public bool drive;
            public bool handbrake;
            [Tooltip("Multiplies brake torque on this wheel. Less on the rear keeps it from locking first and spinning the car.")]
            [Min(0f)] public float brakeScale = 1f;

            [Header("Debug (read only)")]
            public bool grounded;
            [Tooltip("rad/s")] public float angularVelocity;
            public float slipRatio;
            [Tooltip("Degrees")] public float slipAngle;
            [Tooltip("N, summed over contacts")] public float load;
            public bool sliding;

            [System.NonSerialized] public Vector3 restPosition;
            [System.NonSerialized] public Quaternion restRotation;
            [System.NonSerialized] public float spinAngle;
            [System.NonSerialized] public float suspensionOffset;
            [System.NonSerialized] public float[] previousCompression;
        }

        struct Contact
        {
            public Vector3 point;
            public Vector3 forward;
            public Vector3 right;
            public float load;
            public float grip;
            public float rollingResistance;
            public float forwardSpeed;
            public float sideSpeed;
            public float stiffness;
        }

        [SerializeField] Wheel[] wheels =
        {
            new Wheel { name = "Front Left", contactOffsets = new[] { -0.134f }, steer = true },
            new Wheel { name = "Front Right", contactOffsets = new[] { 0.134f }, steer = true },
            new Wheel { name = "Rear Axle", contactOffsets = new[] { -0.822f, 0.822f }, drive = true, handbrake = true, brakeScale = 0.6f },
        };

        [Header("Tyre friction")]
        [Tooltip("Peak friction coefficient μ between tyre and ground, reached right before the tyre starts sliding.")]
        [Min(0f)] [SerializeField] float peakFriction = 1.0f;
        [Tooltip("Friction coefficient μ once the tyre is fully sliding. Lower than peak makes slides harder to recover from.")]
        [Min(0f)] [SerializeField] float slidingFriction = 0.75f;
        [Tooltip("How far past the grip limit (as a fraction of it) friction drops all the way from peak to sliding.")]
        [Min(0.01f)] [SerializeField] float slideTransition = 0.3f;
        [Tooltip("Longitudinal force per unit slip ratio, as a multiple of tyre load. Higher = traction builds with less wheelspin.")]
        [Min(0f)] [SerializeField] float longitudinalStiffness = 10f;
        [Tooltip("Lateral force per radian of slip angle, as a multiple of tyre load. Higher = sharper turn-in.")]
        [Min(0f)] [SerializeField] float lateralStiffness = 10f;
        [Tooltip("Rolling resistance coefficient, as a fraction of tyre load.")]
        [Min(0f)] [SerializeField] float rollingResistance = 0.015f;
        [Tooltip("Speeds below this use it as the slip reference, which keeps the tyre model stable near standstill (m/s).")]
        [Min(0.5f)] [SerializeField] float lowSpeedReference = 3f;

        [Header("Engine and brakes")]
        [Tooltip("Total drive torque at full throttle, split across driven tyres (N·m).")]
        [Min(0f)] [SerializeField] float maxDriveTorque = 2000f;
        [Tooltip("Drive torque fades to zero as wheel surface speed approaches this (m/s).")]
        [Min(0.1f)] [SerializeField] float maxSpeed = 40f;
        [Range(0f, 1f)] [SerializeField] float reverseTorqueScale = 0.5f;
        [Min(0.1f)] [SerializeField] float maxReverseSpeed = 8f;
        [Tooltip("Brake torque per tyre at full brake (N·m).")]
        [Min(0f)] [SerializeField] float brakeTorque = 1200f;
        [Tooltip("Brake torque per tyre on handbrake wheels (N·m).")]
        [Min(0f)] [SerializeField] float handbrakeTorque = 3000f;
        [Tooltip("Releases the foot brake on a wheel whose slip ratio goes past the threshold, so it keeps steering instead of locking.")]
        [SerializeField] bool antiLockBrakes = true;
        [Range(0.05f, 1f)] [SerializeField] float absSlipThreshold = 0.15f;

        [Header("Steering")]
        [Tooltip("Full-lock steering angle when standing still.")]
        [Range(0f, 60f)] [SerializeField] float maxSteerAngle = 35f;
        [Tooltip("Degrees per second the front wheels turn toward the input, when standing still.")]
        [Min(1f)] [SerializeField] float steerSpeed = 150f;
        [Tooltip("Full-lock steering angle at and above highSpeed.")]
        [Range(0f, 60f)] [SerializeField] float highSpeedSteerAngle = 8f;
        [Tooltip("Steering speed (degrees per second) at and above highSpeed.")]
        [Min(1f)] [SerializeField] float highSpeedSteerSpeed = 40f;
        [Tooltip("Speed (km/h) where steering has fully shrunk to the high speed values. Blends linearly below it.")]
        [Min(1f)] [SerializeField] float highSpeed = 100f;
        [Tooltip("Below this front-wheel slip angle (degrees), returning the wheel to centre is still at full steerSpeed.")]
        [Min(0f)] [SerializeField] float returnSlipSoft = 8f;
        [Tooltip("At and above this slip angle, returning the wheel uses minReturnSteerSpeed. Blends between soft and hard.")]
        [Min(0f)] [SerializeField] float returnSlipHard = 25f;
        [Tooltip("Degrees per second the front wheels may return toward the stick when the car is fully sliding.")]
        [Min(0f)] [SerializeField] float minReturnSteerSpeed = 12f;
        [Tooltip("Yaw rate (rad/s) × this counts as slip for the return damper, so a handbrake spin also slows the return.")]
        [Min(0f)] [SerializeField] float yawReturnScale = 20f;
        [SerializeField] bool useGyroSteering = true;
        [Tooltip("Upright: held like a steering wheel, gravity keeps the centre from drifting. Flat: turned flat like on a table, gyro only, drifts over time.")]
        [SerializeField] GyroGrip gyroGrip = GyroGrip.Upright;
        [Tooltip("Controller rotation (degrees) that gives full steering lock.")]
        [Min(1f)] [SerializeField] float gyroFullLockDegrees = 90f;
        [SerializeField] bool invertGyro = true;
        [SerializeField] ControllerButton gyroBindButton = ControllerButton.Menu;
        [SerializeField] ControllerHand gyroBindHand = ControllerHand.Left;
        [Tooltip("Optional cockpit steering wheel, spun around its local Z axis.")]
        [SerializeField] Transform steeringWheel;
        [Tooltip("Steering wheel rotation (degrees) from centre to full steering input.")]
        [Range(0f, 720f)] [SerializeField] float steeringWheelLock = 225f;

        [Header("Suspension")]
        [Min(0.01f)] [SerializeField] float wheelRadius = 0.355f;
        [Tooltip("Spin inertia per tyre (kg·m²). A solid axle with two tyres gets twice this.")]
        [Min(0.01f)] [SerializeField] float tyreInertia = 1.2f;
        [Min(0.01f)] [SerializeField] float suspensionLength = 0.25f;
        [Min(0f)] [SerializeField] float springStiffness = 35000f;
        [Min(0f)] [SerializeField] float damperStiffness = 3500f;
        [SerializeField] LayerMask groundLayers = ~0;

        [Header("Body")]
        [Tooltip("Centre of mass in the car's local space. Lower = less body roll.")]
        [SerializeField] Vector3 centerOfMass = new Vector3(0f, 0.4f, 0f);
        [Tooltip("Aerodynamic drag: force = this × speed².")]
        [Min(0f)] [SerializeField] float airDrag = 0.4f;

        [Header("Debug (read only)")]
        [SerializeField] float speedKmh;
        [SerializeField] float throttleInput;
        [SerializeField] float brakeInput;
        [SerializeField] float steerInput;
        [SerializeField] bool handbrakeInput;
        [SerializeField] float steerAngle;
        [SerializeField] bool gyroBound;
        [SerializeField] float wheelInput;
        [SerializeField] bool wheelHeld;

        Rigidbody rb;
        Quaternion steeringWheelRest;
        float steerLimit;

        public Transform SteeringWheel => steeringWheel;
        public float SteeringWheelLock => steeringWheelLock;
        /// <summary>0..1 foot brake from the left trigger (not reverse).</summary>
        public float BrakeInput => brakeInput;
        public bool HandbrakeInput => handbrakeInput;
        /// <summary>Front wheel steer angle in degrees; right positive.</summary>
        public float SteerAngle => steerAngle;

        /// <summary>
        /// Cockpit / virtual steering-wheel angle in degrees (right positive), same value that spins the wheel model.
        /// First person = grabbed wheel; third person = gyro / input mapped through <see cref="SteeringWheelLock"/>.
        /// </summary>
        public float SteeringWheelAngle
        {
            get
            {
                float turn = wheelHeld
                    ? wheelInput
                    : steerLimit > 0f ? Mathf.Clamp(steerAngle / steerLimit, -1f, 1f) : 0f;
                return turn * steeringWheelLock;
            }
        }

        /// <summary>
        /// Steering from a hand on the cockpit wheel, −1..1 with right positive. Adds to the stick. While held, the
        /// wheel model follows this exactly so the hand stays on it.
        /// </summary>
        public void SetWheelInput(float input, bool held)
        {
            wheelInput = Mathf.Clamp(input, -1f, 1f);
            wheelHeld = held;
        }
        // Large enough for dense road meshes; RaycastNonAlloc does not guarantee nearest-first order.
        readonly RaycastHit[] hitBuffer = new RaycastHit[64];
        Contact[] contacts = new Contact[4];

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.centerOfMass = centerOfMass;
            steerLimit = maxSteerAngle;

            foreach (var wheel in wheels)
            {
                wheel.previousCompression = new float[wheel.contactOffsets.Length];
                for (int i = 0; i < wheel.previousCompression.Length; i++)
                    wheel.previousCompression[i] = float.NaN;
                if (wheel.visual == null)
                    continue;
                wheel.restPosition = transform.InverseTransformPoint(wheel.visual.position);
                wheel.restRotation = Quaternion.Inverse(transform.rotation) * wheel.visual.rotation;
            }

            if (steeringWheel != null)
                steeringWheelRest = steeringWheel.localRotation;

            // Body colliders must not rest on the drive surface — this car is held up by tyre rays only.
            // If the box catches the road, the mesh sits underground while LateUpdate still snaps wheels to the surface.
            IgnoreBodyVsDriveSurface();
        }

        void Start()
        {
            // Road may be spawned after Awake in some scenes.
            IgnoreBodyVsDriveSurface();
        }

        void IgnoreBodyVsDriveSurface()
        {
            var bodyCols = GetComponentsInChildren<Collider>(true);
            // SplineInstantiate block* clones are Untagged and not returned by FindObjectsByType —
            // walk loaded scene roots so body box ignores the drive surface (tyre rays still hit it).
            // Land Plane stays collidable for off-road. ControllerTesting uses name "Ground".
            for (int s = 0; s < UnityEngine.SceneManagement.SceneManager.sceneCount; s++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(s);
                if (!scene.isLoaded)
                    continue;
                var roots = scene.GetRootGameObjects();
                for (int r = 0; r < roots.Length; r++)
                {
                    var cols = roots[r].GetComponentsInChildren<Collider>(true);
                    for (int i = 0; i < cols.Length; i++)
                    {
                        var ground = cols[i];
                        if (ground == null || ground.attachedRigidbody == rb)
                            continue;
                        string n = ground.gameObject.name;
                        if (n != "Ground" && !n.StartsWith("block", System.StringComparison.Ordinal))
                            continue;
                        for (int b = 0; b < bodyCols.Length; b++)
                        {
                            var body = bodyCols[b];
                            if (body != null && body.enabled && !body.isTrigger)
                                Physics.IgnoreCollision(body, ground, true);
                        }
                    }
                }
            }
        }

        void OnValidate()
        {
            if (rb != null)
                rb.centerOfMass = centerOfMass;
            if (returnSlipHard < returnSlipSoft)
                returnSlipHard = returnSlipSoft;
        }

        void Update()
        {
            ReadInput();
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
            speedKmh = forwardSpeed * 3.6f;

            float speedBlend = Mathf.Clamp01(Mathf.Abs(speedKmh) / highSpeed);
            steerLimit = Mathf.Lerp(maxSteerAngle, highSpeedSteerAngle, speedBlend);
            float steerRate = Mathf.Lerp(steerSpeed, highSpeedSteerSpeed, speedBlend);
            float targetSteer = steerInput * steerLimit;
            // Opening the wheel stays sharp; closing it slows down when the tyres (or the car) are sliding.
            if (IsClosingSteer(steerAngle, targetSteer))
            {
                float span = Mathf.Max(0.01f, returnSlipHard - returnSlipSoft);
                float slide = Mathf.Clamp01((ReturnSlip() - returnSlipSoft) / span);
                steerRate = Mathf.Lerp(steerRate, minReturnSteerSpeed, slide);
            }
            steerAngle = Mathf.MoveTowards(steerAngle, targetSteer, steerRate * dt);

            float drive = throttleInput;
            float brake = 0f;
            if (brakeInput > 0f)
            {
                if (forwardSpeed > 1f)
                    brake = brakeInput;
                else
                    drive -= brakeInput * reverseTorqueScale;
            }
            if (throttleInput > 0f && forwardSpeed < -1f)
            {
                brake = Mathf.Max(brake, throttleInput);
                drive = 0f;
            }

            int drivenTyres = 0;
            foreach (var wheel in wheels)
                if (wheel.drive)
                    drivenTyres += wheel.contactOffsets.Length;

            foreach (var wheel in wheels)
            {
                int tyres = wheel.contactOffsets.Length;
                float driveTorque = 0f;
                if (wheel.drive && drivenTyres > 0)
                {
                    driveTorque = drive * maxDriveTorque * tyres / drivenTyres;
                    if (driveTorque * wheel.angularVelocity > 0f)
                    {
                        float limit = drive > 0f ? maxSpeed : maxReverseSpeed;
                        driveTorque *= 1f - Mathf.Clamp01(Mathf.Abs(wheel.angularVelocity) * wheelRadius / limit);
                    }
                }
                float resistTorque = brake * brakeTorque * wheel.brakeScale * tyres;
                if (antiLockBrakes && wheel.sliding && wheel.slipRatio * Mathf.Sign(forwardSpeed) < -absSlipThreshold)
                    resistTorque = 0f;
                if (wheel.handbrake && handbrakeInput)
                    resistTorque += handbrakeTorque * tyres;
                SimulateWheel(wheel, driveTorque, resistTorque, dt);
            }

            float speed = rb.linearVelocity.magnitude;
            if (speed > 0.01f)
                rb.AddForce(-rb.linearVelocity * (airDrag * speed));
        }

        void LateUpdate()
        {
            // Positive Z rotation is counter-clockwise seen from the driver, so a right turn spins it negative.
            if (steeringWheel != null)
            {
                float turn = wheelHeld ? wheelInput : steerLimit > 0f ? Mathf.Clamp(steerAngle / steerLimit, -1f, 1f) : 0f;
                steeringWheel.localRotation = steeringWheelRest * Quaternion.AngleAxis(-turn * steeringWheelLock, Vector3.forward);
            }

            foreach (var wheel in wheels)
            {
                if (wheel.visual == null)
                    continue;
                wheel.spinAngle = Mathf.Repeat(wheel.spinAngle + wheel.angularVelocity * Mathf.Rad2Deg * Time.deltaTime, 360f);
                float steer = wheel.steer ? steerAngle : 0f;
                wheel.visual.SetPositionAndRotation(
                    transform.TransformPoint(wheel.restPosition) + transform.up * wheel.suspensionOffset,
                    transform.rotation * Quaternion.AngleAxis(steer, Vector3.up) * Quaternion.AngleAxis(wheel.spinAngle, Vector3.right) * wheel.restRotation);
            }
        }

        void SimulateWheel(Wheel wheel, float driveTorque, float resistTorque, float dt)
        {
            int count = wheel.contactOffsets.Length;
            if (contacts.Length < count)
                contacts = new Contact[count];

            Vector3 up = transform.up;
            Quaternion steerRotation = Quaternion.AngleAxis(wheel.steer ? steerAngle : 0f, up);
            Vector3 wheelForward = steerRotation * transform.forward;
            float rayLength = suspensionLength + wheelRadius;
            float inertia = tyreInertia * count;

            int grounded = 0;
            float offsetSum = 0f;
            float stiffnessSum = 0f;
            float stiffnessSpeedSum = 0f;
            float loadSum = 0f;
            float forwardSpeedSum = 0f;
            float sideSpeedSum = 0f;

            for (int i = 0; i < count; i++)
            {
                Vector3 origin = transform.TransformPoint(wheel.restPosition + Vector3.right * wheel.contactOffsets[i])
                                 + up * (suspensionLength * 0.5f);

                if (!CastToGround(origin, -up, rayLength, out RaycastHit hit))
                {
                    contacts[i].load = 0f;
                    wheel.previousCompression[i] = 0f;
                    offsetSum += -suspensionLength * 0.5f;
                    continue;
                }

                float compression = rayLength - hit.distance;
                float previous = float.IsNaN(wheel.previousCompression[i]) ? compression : wheel.previousCompression[i];
                float compressionSpeed = (compression - previous) / dt;
                wheel.previousCompression[i] = compression;
                float load = Mathf.Max(0f, springStiffness * compression + damperStiffness * compressionSpeed);
                // Push the chassis at the wheel mount so the body is lifted with the suspension,
                // not only “supported” at the ground contact while the body sinks past the wheels.
                Vector3 mount = transform.TransformPoint(wheel.restPosition + Vector3.right * wheel.contactOffsets[i]);
                rb.AddForceAtPosition(up * load, mount);
                offsetSum += suspensionLength * 0.5f - (hit.distance - wheelRadius);

                Vector3 forward = Vector3.ProjectOnPlane(wheelForward, hit.normal).normalized;
                Vector3 right = Vector3.Cross(hit.normal, forward);
                Vector3 velocity = rb.GetPointVelocity(hit.point);
                if (hit.rigidbody != null)
                    velocity -= hit.rigidbody.GetPointVelocity(hit.point);

                var surface = hit.collider.GetComponentInParent<GroundSurface>();
                ref Contact c = ref contacts[i];
                c.point = hit.point;
                c.forward = forward;
                c.right = right;
                c.load = load;
                c.grip = surface != null ? surface.gripMultiplier : 1f;
                c.rollingResistance = rollingResistance * (surface != null ? surface.rollingResistanceMultiplier : 1f);
                c.forwardSpeed = Vector3.Dot(velocity, forward);
                c.sideSpeed = Vector3.Dot(velocity, right);
                // Linearised longitudinal force: Fx = stiffness × (ω·r − vx).
                c.stiffness = load * longitudinalStiffness / Mathf.Max(Mathf.Abs(c.forwardSpeed), lowSpeedReference);

                grounded++;
                stiffnessSum += c.stiffness;
                stiffnessSpeedSum += c.stiffness * c.forwardSpeed;
                loadSum += load;
                forwardSpeedSum += c.forwardSpeed;
                sideSpeedSum += c.sideSpeed;
                resistTorque += c.rollingResistance * load * wheelRadius;
            }

            wheel.grounded = grounded > 0;
            wheel.load = loadSum;
            wheel.suspensionOffset = offsetSum / count;

            float omega0 = wheel.angularVelocity;
            float meanForwardSpeed = grounded > 0 ? forwardSpeedSum / grounded : 0f;
            float resistSign = Mathf.Abs(omega0) > 0.01f ? Mathf.Sign(omega0) : Mathf.Sign(meanForwardSpeed);

            // Implicit step: the tyre is far stiffer than the wheel's inertia, an explicit step would explode.
            float r = wheelRadius;
            float omegaGrip = (omega0 + dt / inertia * (driveTorque - resistSign * resistTorque + r * stiffnessSpeedSum))
                              / (1f + dt * r * r * stiffnessSum / inertia);
            omegaGrip = StopAtZero(omegaGrip, resistSign, driveTorque, resistTorque);

            bool sliding = false;
            float forceSum = 0f;
            for (int i = 0; i < count; i++)
            {
                ref Contact c = ref contacts[i];
                if (c.load <= 0f)
                    continue;

                float fx = c.stiffness * (omegaGrip * r - c.forwardSpeed);
                float slipAngle = Mathf.Atan(c.sideSpeed / Mathf.Max(Mathf.Abs(c.forwardSpeed), lowSpeedReference));
                float fy = -c.load * lateralStiffness * slipAngle;

                float demand = Mathf.Sqrt(fx * fx + fy * fy);
                float peakLimit = peakFriction * c.grip * c.load;
                if (demand > peakLimit && demand > 0f)
                {
                    float over = peakLimit > 0f ? demand / peakLimit - 1f : 1f;
                    float mu = Mathf.Lerp(peakFriction, slidingFriction, Mathf.Clamp01(over / slideTransition));
                    float scale = mu * c.grip * c.load / demand;
                    fx *= scale;
                    fy *= scale;
                    sliding = true;
                }

                forceSum += fx;
                rb.AddForceAtPosition(c.forward * fx + c.right * fy, c.point);
            }

            float omega = omegaGrip;
            if (sliding)
            {
                // Saturated tyre force is constant, so the wheel can spin freely: integrate explicitly.
                // Crossing the free-rolling speed flips the force direction, which would make it chatter: grip there instead.
                float omegaSlide = omega0 + dt / inertia * (driveTorque - resistSign * resistTorque - r * forceSum);
                omegaSlide = StopAtZero(omegaSlide, resistSign, driveTorque, resistTorque);
                float omegaRolling = meanForwardSpeed / r;
                omega = (omegaSlide - omegaRolling) * (omega0 - omegaRolling) < 0f ? omegaGrip : omegaSlide;
            }
            wheel.angularVelocity = omega;
            wheel.sliding = sliding;

            if (grounded > 0)
            {
                float vx = meanForwardSpeed;
                float vy = sideSpeedSum / grounded;
                float reference = Mathf.Max(Mathf.Abs(vx), lowSpeedReference);
                wheel.slipRatio = (omega * r - vx) / reference;
                wheel.slipAngle = Mathf.Atan(vy / reference) * Mathf.Rad2Deg;
            }
            else
            {
                wheel.slipRatio = 0f;
                wheel.slipAngle = 0f;
            }
        }

        /// <summary>Brakes and rolling resistance can stop a wheel but never spin it backwards.</summary>
        static float StopAtZero(float omega, float resistSign, float driveTorque, float resistTorque)
        {
            if (resistTorque > 0f && omega * resistSign < 0f && Mathf.Abs(driveTorque) <= resistTorque)
                return 0f;
            return omega;
        }

        bool CastToGround(Vector3 origin, Vector3 direction, float length, out RaycastHit nearest)
        {
            // RaycastAll: dense overlapping road boxes can exceed NonAlloc buffers and miss the true nearest hit.
            var hits = Physics.RaycastAll(origin, direction, length, groundLayers, QueryTriggerInteraction.Ignore);
            nearest = default;
            float best = float.MaxValue;
            for (int i = 0; i < hits.Length; i++)
            {
                // Skip the car, backfaces, and underside exits (e.g. ray starts inside a block* and
                // would otherwise lock onto the land Plane under the road).
                if (hits[i].rigidbody == rb || hits[i].distance >= best)
                    continue;
                if (Vector3.Dot(hits[i].normal, -direction) < 0.5f)
                    continue;
                best = hits[i].distance;
                nearest = hits[i];
            }
            return best < float.MaxValue;
        }

        /// <summary>True when the target is smaller in magnitude than the current angle, or has crossed through zero.</summary>
        static bool IsClosingSteer(float current, float target)
        {
            if (current * target < 0f)
                return true;
            return Mathf.Abs(target) < Mathf.Abs(current) - 0.01f;
        }

        /// <summary>Front-wheel slip angle, or yaw-derived slip if the car is spinning harder than the fronts are sliding.</summary>
        float ReturnSlip()
        {
            float front = 0f;
            int count = 0;
            foreach (var wheel in wheels)
            {
                if (!wheel.steer || !wheel.grounded)
                    continue;
                front += Mathf.Abs(wheel.slipAngle);
                count++;
            }
            if (count > 0)
                front /= count;
            float yaw = Mathf.Abs(rb.angularVelocity.y) * yawReturnScale;
            return Mathf.Max(front, yaw);
        }

        void ReadInput()
        {
            throttleInput = VirtualController.GetTrigger(ControllerHand.Right);
            brakeInput = VirtualController.GetTrigger(ControllerHand.Left);
            handbrakeInput = VirtualController.GetButton(ControllerButton.Primary, ControllerHand.Right);

            steerInput = 0f;
            if (useGyroSteering && VirtualController.ActiveKind == ControllerKind.PlayStation)
                ReadGyroSteering();

            // Keyboard A/D (remappable). Kept out of the virtual right stick so it does not fight camera look.
            if (!SettingsMenu.IsOpen)
                steerInput += KeyboardBindings.GetAxis(KeyboardAction.SteerLeft, KeyboardAction.SteerRight);

            steerInput = Mathf.Clamp(steerInput + wheelInput, -1f, 1f);
        }

        void ReadGyroSteering()
        {
            if (VirtualController.GetButtonDown(gyroBindButton, gyroBindHand))
            {
                if (DualSenseMotion.IsAvailable)
                {
                    DualSenseMotion.Calibrate();
                    gyroBound = true;
                }
                else
                {
                    Debug.LogWarning($"[TireFrictionCar] No gyro data: {DualSenseMotion.Current.status}", this);
                }
            }

            if (!gyroBound || !DualSenseMotion.IsAvailable || DualSenseMotion.IsCalibrating)
                return;

            float raw = gyroGrip == GyroGrip.Upright ? DualSenseMotion.UprightAngle : DualSenseMotion.IntegratedAngle.y;
            float angle = raw * (invertGyro ? -1f : 1f);
            steerInput += angle / gyroFullLockDegrees;
        }
    }
}
