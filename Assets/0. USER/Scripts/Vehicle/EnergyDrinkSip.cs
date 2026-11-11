using UnityEngine;

namespace FullThrottleSun.Vehicle
{
    /// <summary>
    /// While the energy drink can is held near the head camera, refill
    /// <see cref="SleepTimer"/> via <see cref="SleepTimer.RestartTimer"/>.
    /// Uses a distance check (not physics triggers) so kinematic hand-follow still counts.
    /// </summary>
    [RequireComponent(typeof(HandGrabbable))]
    public class EnergyDrinkSip : MonoBehaviour
    {
        [SerializeField] SleepTimer sleepTimer;
        [SerializeField] HandGrabbable grab;
        [SerializeField] Transform head;
        [SerializeField] float sipRadius = 0.28f;
        [SerializeField] float cooldown = 1.5f;

        float nextSipTime;

        void Awake()
        {
            if (grab == null)
                grab = GetComponent<HandGrabbable>();
            if (sleepTimer == null)
                sleepTimer = FindAnyObjectByType<SleepTimer>(FindObjectsInactive.Include);
            ResolveHead();
        }

        void ResolveHead()
        {
            if (head != null)
                return;

            var cam = FindAnyObjectByType<CarFollowCamera>();
            if (cam != null)
            {
                head = cam.transform;
                return;
            }

            var tagged = GameObject.FindWithTag("PlayerHead");
            if (tagged != null)
                head = tagged.transform;
        }

        void Update()
        {
            if (grab == null || !grab.IsHeld)
                return;
            if (Time.time < nextSipTime)
                return;

            if (head == null)
                ResolveHead();
            if (head == null)
                return;

            float reach = sipRadius;
            var sphere = head.GetComponent<SphereCollider>();
            if (sphere != null && sphere.enabled)
                reach = Mathf.Max(reach, sphere.radius * MaxAbsScale(head) + 0.05f);

            if ((transform.position - head.position).sqrMagnitude > reach * reach)
                return;

            if (sleepTimer == null)
                sleepTimer = FindAnyObjectByType<SleepTimer>(FindObjectsInactive.Include);
            if (sleepTimer == null)
                return;

            sleepTimer.RestartTimer();
            nextSipTime = Time.time + Mathf.Max(0.1f, cooldown);
        }

        static float MaxAbsScale(Transform t)
        {
            Vector3 s = t.lossyScale;
            return Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
        }
    }
}
