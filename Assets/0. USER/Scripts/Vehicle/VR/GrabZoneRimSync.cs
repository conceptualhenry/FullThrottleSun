using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FullThrottleSun.Vehicle
{
    /// <summary>
    /// Keeps Grab Zone rim capsules matched to <c>Rim 0</c> size / ring radius.
    /// Right-click component → Sync From Rim 0 (also available on the GameObject).
    /// </summary>
    [ExecuteAlways]
    public class GrabZoneRimSync : MonoBehaviour
    {
        const string Rim0Name = "Rim 0";

        [ContextMenu("Sync From Rim 0")]
        public void SyncFromRim0()
        {
            Transform rim0 = transform.Find(Rim0Name);
            if (rim0 == null)
            {
                Debug.LogWarning("[GrabZoneRimSync] No child named Rim 0.", this);
                return;
            }

            var col0 = rim0.GetComponent<CapsuleCollider>();
            if (col0 == null)
            {
                Debug.LogWarning("[GrabZoneRimSync] Rim 0 has no CapsuleCollider.", this);
                return;
            }

            float ringRadius = new Vector2(rim0.localPosition.x, rim0.localPosition.y).magnitude;
            if (ringRadius < 1e-4f)
                ringRadius = Mathf.Abs(rim0.localPosition.x);
            if (ringRadius < 1e-4f)
            {
                Debug.LogWarning("[GrabZoneRimSync] Rim 0 ring radius is ~0.", this);
                return;
            }

            Vector3 scale = rim0.localScale;
            float capsuleRadius = col0.radius;
            float capsuleHeight = col0.height;
            Vector3 capsuleCenter = col0.center;
            int capsuleDir = col0.direction;

            var rims = new System.Collections.Generic.List<Transform>();
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform c = transform.GetChild(i);
                if (c.name.StartsWith("Rim "))
                    rims.Add(c);
            }

            rims.Sort((a, b) =>
            {
                int.TryParse(a.name.Substring(4), out int ia);
                int.TryParse(b.name.Substring(4), out int ib);
                return ia.CompareTo(ib);
            });

            int n = rims.Count;
            if (n == 0)
                return;

            for (int i = 0; i < n; i++)
            {
                Transform rim = rims[i];
                float ang = 360f * i / n;
                rim.localRotation = Quaternion.Euler(0f, 0f, ang);
                rim.localPosition = rim.localRotation * Vector3.right * ringRadius;
                rim.localScale = scale;

                var col = rim.GetComponent<CapsuleCollider>();
                if (col == null)
                {
                    Debug.LogWarning($"[GrabZoneRimSync] {rim.name} has no CapsuleCollider — add it in the editor.", this);
                    continue;
                }
                col.isTrigger = true;
                col.radius = capsuleRadius;
                col.height = capsuleHeight;
                col.center = capsuleCenter;
                col.direction = capsuleDir;
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
                EditorUtility.SetDirty(gameObject);
#endif
            Debug.Log($"[GrabZoneRimSync] Synced {n} rims from Rim 0 (ring={ringRadius:F3}, radius={capsuleRadius:F3}, height={capsuleHeight:F3}).", this);
        }
    }
}
