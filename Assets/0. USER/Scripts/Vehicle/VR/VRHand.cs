using FullThrottleSun.Controller;
using UnityEngine;

namespace FullThrottleSun.Vehicle
{
    /// <summary>
    /// Visible hand for one tracked controller. Spawned by <see cref="SteeringWheelGrab"/>.
    /// Follows the tracked pose from <see cref="IVrHandPoseSource"/>, or snaps to a pin from
    /// <see cref="HandPinHub"/> while something is grabbed (wheel, stalk, <see cref="HandGrabbable"/>).
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class VRHand : MonoBehaviour
    {
        [Tooltip("How quickly a released hand slides back onto the real controller. Higher = faster.")]
        [Min(1f)] [SerializeField] float returnSpeed = 20f;

        IVrHandPoseSource poseSource;
        ControllerHand hand;
        Renderer[] renderers;
        bool visible = true;
        bool wasPinned;
        // Leftover offset from a pin after letting go, in pose-source space, fading out.
        Transform offsetSpace;
        Vector3 offsetPosition;
        Quaternion offsetRotation = Quaternion.identity;

        public ControllerHand Side => hand;
        public IVrHandPoseSource PoseSource => poseSource;
        public SphereCollider GrabCollider { get; private set; }

        public void Init(IVrHandPoseSource source, ControllerHand side, GameObject model, float radius, Transform offsetSpaceRoot = null)
        {
            poseSource = source;
            hand = side;
            offsetSpace = offsetSpaceRoot;
            gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");

            if (model != null)
                Instantiate(model, transform, false);
            else
                BuildPlaceholder();
            foreach (var c in GetComponentsInChildren<Collider>())
                Destroy(c);

            GrabCollider = gameObject.AddComponent<SphereCollider>();
            GrabCollider.isTrigger = true;
            GrabCollider.radius = radius;

            renderers = GetComponentsInChildren<Renderer>();
            SetVisible(false);
        }

        void LateUpdate()
        {
            if (poseSource == null || !poseSource.TryGetTrackedPose(hand, out Vector3 trackedPosition, out Quaternion trackedRotation))
            {
                SetVisible(false);
                wasPinned = false;
                offsetPosition = Vector3.zero;
                offsetRotation = Quaternion.identity;
                return;
            }
            SetVisible(true);

            if (HandPinHub.TryGetPinnedPose(hand, out Vector3 pinnedPosition, out Quaternion pinnedRotation))
            {
                transform.SetPositionAndRotation(pinnedPosition, pinnedRotation);
                wasPinned = true;
                return;
            }

            Transform space = offsetSpace != null ? offsetSpace : transform.root;
            if (wasPinned)
            {
                offsetPosition = space.InverseTransformVector(transform.position - trackedPosition);
                offsetRotation = Quaternion.Inverse(trackedRotation) * transform.rotation;
                wasPinned = false;
            }

            float keep = Mathf.Exp(-returnSpeed * Time.deltaTime);
            offsetPosition *= keep;
            offsetRotation = Quaternion.Slerp(Quaternion.identity, offsetRotation, keep);
            transform.SetPositionAndRotation(
                trackedPosition + space.TransformVector(offsetPosition),
                trackedRotation * offsetRotation);
        }

        void SetVisible(bool show)
        {
            if (visible == show)
                return;
            visible = show;
            foreach (var r in renderers)
                r.enabled = show;
        }

        /// <summary>
        /// A fist around the controller grip: OpenXR's grip pose has +Z forward through the fist and the palm facing
        /// +X on the left hand, −X on the right.
        /// </summary>
        void BuildPlaceholder()
        {
            float palm = hand == ControllerHand.Left ? 1f : -1f;
            var color = hand == ControllerHand.Left ? new Color(0.35f, 0.6f, 1f) : new Color(1f, 0.55f, 0.25f);
            AddPart("Fist", new Vector3(-palm * 0.005f, 0f, 0f), new Vector3(0.05f, 0.075f, 0.09f), color);
            AddPart("Thumb", new Vector3(palm * 0.02f, 0.04f, 0.025f), new Vector3(0.022f, 0.022f, 0.055f), color);
            AddPart("Wrist", new Vector3(-palm * 0.005f, -0.01f, -0.08f), new Vector3(0.045f, 0.05f, 0.07f), color * 0.8f);
        }

        void AddPart(string partName, Vector3 localPosition, Vector3 size, Color color)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = partName;
            part.layer = gameObject.layer;
            part.transform.SetParent(transform, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = size;
            part.GetComponent<Renderer>().material.color = color;
        }
    }
}
