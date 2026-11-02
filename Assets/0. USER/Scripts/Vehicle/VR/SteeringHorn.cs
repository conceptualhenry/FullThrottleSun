using FullThrottleSun.Controller;
using System.Collections.Generic;
using UnityEngine;

namespace FullThrottleSun.Vehicle
{
    /// <summary>
    /// Hub horn pad. VR first person: hold Grip with a free hand while overlapping the assigned
    /// pad collider(s) → horn. Does not create or rewrite colliders — use whatever you put on this object.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public class SteeringHorn : MonoBehaviour
    {
        static readonly List<SteeringHorn> Active = new();

        [Tooltip("Pad colliders. Empty = existing colliders on this object only (not children).")]
        [SerializeField] Collider[] hornColliders;
        [SerializeField] CarFollowCamera viewCamera;
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip hornClip;
        [Tooltip("If on, keeps playing while grip+pad; if off, plays once per press.")]
        [SerializeField] bool loopWhileHeld = true;
        [Tooltip("Extra reach beyond the hand sphere when using distance fallback.")]
        [SerializeField] float touchPadding = 0.02f;

        [Header("Debug")]
        [SerializeField] bool sounding;
        [SerializeField] ControllerHand soundingHand;

        public bool IsSounding => sounding;

        void Awake()
        {
            ResolveHornColliders();

            if (viewCamera == null)
                viewCamera = FindAnyObjectByType<CarFollowCamera>();

            EnsureAudio();
        }

        void ResolveHornColliders()
        {
            if (hornColliders != null)
            {
                int live = 0;
                for (int i = 0; i < hornColliders.Length; i++)
                {
                    if (hornColliders[i] != null)
                        live++;
                }
                if (live == 0)
                    hornColliders = null;
            }

            // Only pick up colliders already on THIS object — never add/disable/rewrite them.
            if (hornColliders == null || hornColliders.Length == 0)
                hornColliders = GetComponents<Collider>();
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

            if (hornClip == null)
                hornClip = Resources.Load<AudioClip>("CarHornSimple");
            if (hornClip == null)
                hornClip = BuildFallbackClip();
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
            return VirtualController.IsTrackedController
                   && viewCamera != null
                   && viewCamera.IsHeadTracked
                   && viewCamera.IsFirstPerson;
        }

        bool TryHornHand(ControllerHand hand)
        {
            if (!VrHandClaim.IsFree(hand))
                return false;
            if (!VrGrip.IsHeld(hand))
                return false;
            if (!VrGrip.TryGetHand(hand, out VRHand vrHand) || vrHand.GrabCollider == null)
                return false;
            if (!VrGrip.TryGetWorldPose(hand, out Vector3 pos, out Quaternion rot))
                return false;
            return OverlapsHand(vrHand.GrabCollider, pos, rot);
        }

        bool OverlapsHand(SphereCollider handCollider, Vector3 handPosition, Quaternion handRotation)
        {
            if (hornColliders == null || hornColliders.Length == 0)
                return false;

            if (GrabOverlap.TouchesAny(handCollider, handPosition, handRotation, hornColliders))
                return true;

            float reach = (handCollider != null ? handCollider.radius : 0.05f) + touchPadding;
            return DistanceTo(handPosition) <= reach;
        }

        public float DistanceTo(Vector3 worldPosition)
        {
            float best = float.MaxValue;
            if (hornColliders == null)
                return Vector3.Distance(transform.position, worldPosition);
            for (int i = 0; i < hornColliders.Length; i++)
            {
                var c = hornColliders[i];
                if (c == null || !c.enabled)
                    continue;
                float d = Vector3.Distance(c.ClosestPoint(worldPosition), worldPosition);
                if (d < best)
                    best = d;
            }
            return best < float.MaxValue ? best : Vector3.Distance(transform.position, worldPosition);
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
    }
}
