using FullThrottleSun.Controller;
using UnityEngine;

namespace FullThrottleSun.Vehicle
{
    /// <summary>
    /// Car driven purely by tyre-ground friction. Each wheel is a raycast suspension; the tyre force comes from
    /// longitudinal slip ratio and lateral slip angle, limited by a friction circle (μ × load × surface grip).
    /// A wheel entry is one spinning body: give it several contact offsets when one mesh is a solid axle.
    ///
    /// Gamepad only, through VirtualController. On a PlayStation pad: R2 throttle, L2 brake / reverse,
    /// left stick steer, Cross handbrake. Gyro steering: hold the DualSense still and press Options to bind, again to recenter.
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
        [Tooltip("Steering wheel degrees per degree of front wheel angle.")]
        [SerializeField] float steeringWheelRatio = 10f;

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

        Rigidbody rb;
        Quaternion steeringWheelRest;
        readonly RaycastHit[] hitBuffer = new RaycastHit[8];
        Contact[] contacts = new Contact[4];

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.centerOfMass = centerOfMass;

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
        }

        void OnValidate()
        {
            if (rb != null)
                rb.centerOfMass = centerOfMass;
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
            float steerLimit = Mathf.Lerp(maxSteerAngle, highSpeedSteerAngle, speedBlend);
            float steerRate = Mathf.Lerp(steerSpeed, highSpeedSteerSpeed, speedBlend);
            steerAngle = Mathf.MoveTowards(steerAngle, steerInput * steerLimit, steerRate * dt);

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
                steeringWheel.localRotation = steeringWheelRest * Quaternion.AngleAxis(-steerAngle * steeringWheelRatio, Vector3.forward);

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
                rb.AddForceAtPosition(up * load, hit.point);
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
            int hitCount = Physics.RaycastNonAlloc(origin, direction, hitBuffer, length, groundLayers, QueryTriggerInteraction.Ignore);
            nearest = default;
            float best = float.MaxValue;
            for (int i = 0; i < hitCount; i++)
            {
                if (hitBuffer[i].rigidbody == rb || hitBuffer[i].distance >= best)
                    continue;
                best = hitBuffer[i].distance;
                nearest = hitBuffer[i];
            }
            return best < float.MaxValue;
        }

        void ReadInput()
        {
            throttleInput = VirtualController.GetTrigger(ControllerHand.Right);
            brakeInput = VirtualController.GetTrigger(ControllerHand.Left);
            steerInput = VirtualController.GetThumbstick(ControllerHand.Left).x;
            handbrakeInput = VirtualController.GetButton(ControllerButton.Primary, ControllerHand.Right);

            if (useGyroSteering)
                ReadGyroSteering();

            steerInput = Mathf.Clamp(steerInput, -1f, 1f);
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
