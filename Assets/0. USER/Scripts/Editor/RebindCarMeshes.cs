#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FullThrottleSun.EditorTools
{
    /// <summary>One-shot: reconnect ControllerTesting Car to Classic/Car.fbx after a Blender re-export.</summary>
    public static class RebindCarMeshes
    {
        const string FbxPath = "Assets/0. USER/Art&Models/High Matters/Free American Sedans/Art/Meshes/Classic/Car.fbx";

        [MenuItem("FullThrottleSun/Rebind Scene Car Meshes From FBX")]
        public static void Rebind()
        {
            var fbxRoot = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            var car = GameObject.Find("Car");
            if (fbxRoot == null || car == null)
            {
                Debug.LogError("[RebindCarMeshes] Missing FBX or scene Car.");
                return;
            }

            var remap = BuildRemap();

            if (PrefabUtility.IsPartOfPrefabInstance(car))
                PrefabUtility.UnpackPrefabInstance(car, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            var fbxByName = new Dictionary<string, Transform>();
            foreach (var t in fbxRoot.GetComponentsInChildren<Transform>(true))
            {
                if (t == fbxRoot.transform)
                    continue;
                if (!fbxByName.ContainsKey(t.name))
                    fbxByName[t.name] = t;
            }

            int rebound = 0, added = 0;
            foreach (var kv in fbxByName)
            {
                string name = kv.Key;
                if (name.StartsWith("Untitled"))
                    continue;

                var src = kv.Value;
                var srcMf = src.GetComponent<MeshFilter>();
                var srcMr = src.GetComponent<MeshRenderer>();
                if (srcMf == null || srcMf.sharedMesh == null)
                    continue;

                Transform dst = null;
                foreach (var t in car.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == name)
                    {
                        dst = t;
                        break;
                    }
                }

                if (dst == null)
                {
                    Transform parent = car.transform;
                    string parentName = src.parent != null ? src.parent.name : "Car";
                    if (parentName != fbxRoot.name && parentName != "Car")
                    {
                        foreach (var t in car.GetComponentsInChildren<Transform>(true))
                        {
                            if (t.name == parentName)
                            {
                                parent = t;
                                break;
                            }
                        }
                    }

                    var go = new GameObject(name);
                    Undo.RegisterCreatedObjectUndo(go, "Add car part");
                    go.transform.SetParent(parent, false);
                    dst = go.transform;
                    added++;
                }

                var mf = dst.GetComponent<MeshFilter>();
                if (mf == null)
                    mf = Undo.AddComponent<MeshFilter>(dst.gameObject);
                var mr = dst.GetComponent<MeshRenderer>();
                if (mr == null)
                    mr = Undo.AddComponent<MeshRenderer>(dst.gameObject);

                Undo.RecordObject(mf, "Rebind mesh");
                Undo.RecordObject(mr, "Rebind mats");
                Undo.RecordObject(dst, "Rebind transform");

                mf.sharedMesh = srcMf.sharedMesh;
                mr.sharedMaterials = RemapMats(srcMr.sharedMaterials, remap);
                if (name.IndexOf("Mirror", System.StringComparison.OrdinalIgnoreCase) >= 0
                    && remap.TryGetValue("Car_Mirror_Mat", out var mirrorMat))
                    mr.sharedMaterial = mirrorMat;
                dst.localPosition = src.localPosition;
                dst.localRotation = src.localRotation;
                dst.localScale = src.localScale;
                rebound++;
            }

            EditorSceneManager.MarkSceneDirty(car.scene);
            EditorSceneManager.SaveScene(car.scene);

            int nullMeshes = 0, mirrors = 0;
            foreach (var r in car.GetComponentsInChildren<Renderer>(true))
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null)
                    nullMeshes++;
                if (r.name.Contains("Mirror"))
                    mirrors++;
            }

            Debug.Log($"[RebindCarMeshes] rebound={rebound} added={added} nullMeshes={nullMeshes} mirrors={mirrors}");
        }

        static Dictionary<string, Material> BuildRemap()
        {
            var remap = new Dictionary<string, Material>();
            void Add(string from, string guid)
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (m != null)
                    remap[from] = m;
            }

            Add("Car_base_color", "589a951359bc42244877dc077fa874b9");
            Add("Car_Color_Mat", "589a951359bc42244877dc077fa874b9");
            Add("Car_detail", "8a96de6a4d0a971488524f3cf67371f2");
            Add("Car_details_Mat", "8a96de6a4d0a971488524f3cf67371f2");
            Add("Glass", "19f725798471f1849bf461a95ab8f3da");
            Add("Car_Glass_Mat", "19f725798471f1849bf461a95ab8f3da");
            Add("Car_number", "ae399ab536426b54fb6eb6c6b079e23c");
            Add("Car_Number_Mat", "ae399ab536426b54fb6eb6c6b079e23c");
            Add("Car_LightForward", "714536274e3ceef4898c5ee641fb8dc3");
            Add("Headlights_Mat", "714536274e3ceef4898c5ee641fb8dc3");
            Add("Car_Turnlight_L", "6550362f900c7254a910631b3ad601a6");
            Add("Turn signal_L_Mat", "6550362f900c7254a910631b3ad601a6");
            Add("Car_Turnlight_R", "9c6f93c757bd66742bd094a2e94293b2");
            Add("Turn signal_R_Mat", "9c6f93c757bd66742bd094a2e94293b2");
            Add("Car_backLight", "07f70d45f361982488eca993b45e378b");
            Add("Rear_lights_Mat", "07f70d45f361982488eca993b45e378b");
            Add("Car_stopLight", "c668dda1c47d89f4ea6837734d817a83");
            Add("StopSignal_Mat", "c668dda1c47d89f4ea6837734d817a83");
            Add("Car_Mirror_Mat", "556898e2ba0102f43930ec7c5e0e57fd");
            Add("Mirror", "556898e2ba0102f43930ec7c5e0e57fd");
            return remap;
        }

        static Material[] RemapMats(Material[] src, Dictionary<string, Material> remap)
        {
            var dst = new Material[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                if (src[i] == null)
                    continue;
                dst[i] = remap.TryGetValue(src[i].name, out var m) ? m : src[i];
            }
            return dst;
        }
    }
}
#endif
