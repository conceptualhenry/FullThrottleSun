using FullThrottleSun.Controller;
using System.Collections.Generic;
using UnityEngine;

namespace FullThrottleSun.Vehicle
{
    /// <summary>
    /// Hub horn pad. VR: hold Grip with a free hand near the hub → horn.
    /// Detection uses a world-space sphere around this transform (reliable), not only scaled MeshColliders.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public class SteeringHorn : MonoBehaviour
    {
        static readonly List<SteeringHorn> Active = new();

        [Tooltip("Optional editor colliders for gizmo / legacy. Overlap also uses padRadius around this transform.")]
        [SerializeField] Collider[] hornColliders;
        [SerializeField] CarFollowCamera viewCamera;
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip hornClip;
        [Tooltip("World-space reach from the horn centre (m). Inside this radius the wheel must yield.")]
        [SerializeField] float padRadius = 0.14f;
        [Tooltip("If on, keeps playing while grip+pad; if off, plays once per press.")]
        [SerializeField] bool loopWhileHeld = true;

        [Header("Debug")]
        [SerializeField] bool sounding;
        [SerializeField] ControllerHand soundingHand;

        public bool IsSounding => sounding;

        void Awake()
        {
            if (hornColliders == null || hornColliders.Length == 0)
                hornColliders = GetComponents<Collider>();

            if (viewCamera == null)
                viewCamera = FindAnyObjectByType<CarFollowCamera>();

            EnsureAudio();
        }

        void EnsureAudio()
        {
            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();

            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            audioSource.volume = 1f;
            audioSource.loop = loopWhileHeld;
            audioSource.mute = false;
            audioSource.priority = 64;
            audioSource.bypassListenerEffects = true;

            if (hornClip == null)
                hornClip = Resources.Load<AudioClip>("CarHornSimple");
            if (hornClip == null)
                hornClip = BuildFallbackClip();
            if (hornClip != null && hornClip.loadState == AudioDataLoadState.Unloaded)
                hornClip.LoadAudioData();
            audioSource.clip = hornClip;
        }

        static AudioClip BuildFallbackClip()
        {
            const int sampleRate = 44100;
            const float duration = 0.75f;
            int n = Mathf.RoundToInt(sampleRate * duration);
            var samples = new float[n];
            const float f1 = 392f;
            const float f2 = 494f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sampleRate;
                float env = 1f;
                if (t < 0.02f)
                    env = t / 0.02f;
                else if (t > duration - 0.08f)
                    env = Mathf.Max(0f, (duration - t) / 0.08f);
                float Tone(float f) =>
                    Mathf.Sin(2f * Mathf.PI * f * t)
                    + 0.35f * Mathf.Sin(2f * Mathf.PI * f * 3f * t)
                    + 0.15f * Mathf.Sin(2f * Mathf.PI * f * 5f * t);
                float s = (0.55f * Tone(f1) + 0.45f * Tone(f2))
                          * (0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * 6f * t))
                          * env * 0.45f;
                samples[i] = Mathf.Clamp(s, -1f, 1f);
            }

            var clip = AudioClip.Create("CarHornFallback", n, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        void OnEnable() => Active.Add(this);

        void OnDisable()
        {
            Active.Remove(this);
            SetSounding(false, default);
        }

        void Update()
        {
            // Flat-screen sanity check: H key always honks if audio is alive.
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.hKey.isPressed)
            {
                SetSounding(true, ControllerHand.Right);
                return;
            }

            bool want = false;
            var hand = ControllerHand.Left;

            if (CanUseVrHorn())
            {
                if (TryHornHand(ControllerHand.Left))
                {
                    want = true;
                    hand = ControllerHand.Left;
                }
                else if (TryHornHand(ControllerHand.Right))
                {
                    want = true;
                    hand = ControllerHand.Right;
                }
            }

            SetSounding(want, hand);
        }

        bool CanUseVrHorn()
        {
            if (viewCamera == null)
                viewCamera = FindAnyObjectByType<CarFollowCamera>();
            return VirtualController.IsTrackedController
                   && viewCamera != null
                   && viewCamera.IsHeadTracked
                   && viewCamera.IsFirstPerson;
        }

        bool TryHornHand(ControllerHand hand)
        {
            // Grip + hand on hub pad. Claim ownership doesn't matter — audio should still fire.
            if (!VrGrip.IsHeld(hand))
                return false;
            if (!TryGetHandWorld(hand, out Vector3 pos, out _))
                return false;
            return IsNearPad(pos);
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

        bool IsNearPad(Vector3 worldPosition)
            => (PadCenter() - worldPosition).sqrMagnitude <= padRadius * padRadius;

        /// <summary>
        /// Hub centre in world space. Uses this transform — not the flattened SphereCollider bounds,
        /// which are unreliable under the pad's non-uniform scale.
        /// </summary>
        Vector3 PadCenter() => transform.position;

        public float DistanceTo(Vector3 worldPosition)
            => Vector3.Distance(PadCenter(), worldPosition);

        public float PadRadius => padRadius;

        /// <summary>
        /// Wheel grab should yield / release when the hand is clearly on the hub, or closer to the
        /// horn than to the rim (rim capsules otherwise steal the grip around the pad).
        /// </summary>
        public static bool ShouldYieldWheel(Vector3 worldPosition, float wheelDistance)
        {
            float bestPad = float.MaxValue;
            float bestRadius = 0.14f;
            for (int i = 0; i < Active.Count; i++)
            {
                var horn = Active[i];
                if (horn == null || !horn.isActiveAndEnabled)
                    continue;
                float d = horn.DistanceTo(worldPosition);
                if (d < bestPad)
                {
                    bestPad = d;
                    bestRadius = horn.padRadius;
                }
            }
            if (bestPad == float.MaxValue)
                return false;
            if (bestPad <= bestRadius)
                return true;
            return bestPad + 0.01f < wheelDistance;
        }

        bool OverlapsHand(SphereCollider handCollider, Vector3 handPosition, Quaternion handRotation)
        {
            if (IsNearPad(handPosition))
                return true;
            if (handCollider == null || hornColliders == null)
                return false;
            return GrabOverlap.TouchesAny(handCollider, handPosition, handRotation, hornColliders);
        }

        void SetSounding(bool on, ControllerHand hand)
        {
            if (on == sounding && (!on || soundingHand == hand))
            {
                if (on && audioSource != null && loopWhileHeld && !audioSource.isPlaying)
                    PlayHorn();
                return;
            }

            sounding = on;
            soundingHand = hand;

            if (audioSource == null)
                return;

            if (on)
                PlayHorn();
            else
                audioSource.Stop();
        }

        void PlayHorn()
        {
            if (hornClip != null)
                audioSource.clip = hornClip;
            if (audioSource.clip == null)
                return;
            audioSource.spatialBlend = 0f;
            audioSource.loop = loopWhileHeld;
            audioSource.Play();
        }

        public static bool OverlapsPad(
            SphereCollider handCollider,
            Vector3 handPosition,
            Quaternion handRotation)
        {
            for (int i = 0; i < Active.Count; i++)
            {
                var horn = Active[i];
                if (horn == null || !horn.isActiveAndEnabled)
                    continue;
                if (horn.OverlapsHand(handCollider, handPosition, handRotation))
                    return true;
            }
            return false;
        }

        public static float DistanceToNearestPad(Vector3 worldPosition)
        {
            float best = float.MaxValue;
            for (int i = 0; i < Active.Count; i++)
            {
                var horn = Active[i];
                if (horn == null || !horn.isActiveAndEnabled)
                    continue;
                float d = horn.DistanceTo(worldPosition);
                if (d < best)
                    best = d;
            }
            return best;
        }

        /// <summary>True when the hand is inside any active horn pad radius (no collider pose needed).</summary>
        public static bool IsHandOnPad(Vector3 worldPosition)
        {
            for (int i = 0; i < Active.Count; i++)
            {
                var horn = Active[i];
                if (horn == null || !horn.isActiveAndEnabled)
                    continue;
                if (horn.IsNearPad(worldPosition))
                    return true;
            }
            return false;
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.35f);
            Gizmos.DrawSphere(transform.position, padRadius);
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, padRadius);
        }
#endif
    }
}
