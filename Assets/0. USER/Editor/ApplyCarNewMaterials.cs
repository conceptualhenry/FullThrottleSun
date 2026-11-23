#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class ApplyCarNewMaterials
{
    const string Folder = "Assets/0. USER/Art&Models/High Matters/Free American Sedans/Art/Materials/Car_New";
    const string OutFolder = Folder + "/Generated";

    [MenuItem("Tools/Apply Car_New Materials To Car (1)")]
    public static void Run()
    {
        if (!AssetDatabase.IsValidFolder(OutFolder))
            AssetDatabase.CreateFolder(Folder, "Generated");

        Material main = MakeLit(
            "Car_New_MainTextures",
            Load("MainTextures_BaseColor.jpg"),
            Load("Main_Normal.jpg"),
            Load("Main_Metallic.jpg"),
            0.55f,
            false);

        Material glass = MakeLit(
            "Car_New_Glass",
            Load("Glass_BaseColor.jpg"),
            Load("Glass_Normal.jpg"),
            Load("Glass_Metallic.jpg"),
            0.92f,
            true);

        Material wheels = MakeLit(
            "Car_New_Wheels",
            Load("Wheels_BaseColor.jpg"),
            Load("Wheels_Normal.jpg"),
            Load("Wheels_Metallic.jpg"),
            0.35f,
            false);

        Material leather = MakeLit(
            "Car_New_Leather",
            Load("Leather_Base_01_basecolor.jpg"),
            Load("Leather_Base_01_normal.jpg"),
            Load("Leather_Base_01_metallic.jpg"),
            0.28f,
            false);

        Texture2D leatherAo = Load("Leather_Base_01_ambientocclusion.jpg");
        if (leatherAo != null)
        {
            leather.SetTexture("_OcclusionMap", leatherAo);
            leather.SetFloat("_OcclusionStrength", 1f);
            EditorUtility.SetDirty(leather);
        }

        AssetDatabase.SaveAssets();

        GameObject car1 = GameObject.Find("Car (1)");
        if (car1 == null)
        {
            Debug.LogError("Car (1) not found in active scene.");
            return;
        }

        int replaced = 0;
        Renderer[] rends = car1.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rends.Length; i++)
        {
            Material[] mats = rends[i].sharedMaterials;
            bool changed = false;
            for (int m = 0; m < mats.Length; m++)
            {
                if (mats[m] == null)
                    continue;
                string n = mats[m].name;
                Material next = null;
                if (n == "MainTextures")
                    next = main;
                else if (n == "Glass")
                    next = glass;
                else if (n == "Wheels")
                    next = wheels;
                else if (n.IndexOf("Leather", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    next = leather;

                if (next != null)
                {
                    mats[m] = next;
                    changed = true;
                    replaced++;
                    Debug.Log($"[Car_New] {rends[i].name}[{m}] {n} -> {next.name}");
                }
            }

            if (changed)
            {
                rends[i].sharedMaterials = mats;
                EditorUtility.SetDirty(rends[i]);
            }
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log($"[Car_New] Done. replacedSlots={replaced}");
    }

    static Texture2D Load(string file)
    {
        return AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/" + file);
    }

    static Material MakeLit(
        string name,
        Texture2D albedo,
        Texture2D normal,
        Texture2D metallic,
        float smoothness,
        bool transparent)
    {
        string path = OutFolder + "/" + name + ".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            mat.shader = shader;
        }

        mat.SetColor("_BaseColor", Color.white);
        if (albedo != null)
            mat.SetTexture("_BaseMap", albedo);
        if (normal != null)
        {
            mat.SetTexture("_BumpMap", normal);
            mat.EnableKeyword("_NORMALMAP");
            mat.SetFloat("_BumpScale", 1f);
        }

        if (metallic != null)
        {
            mat.SetTexture("_MetallicGlossMap", metallic);
            mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            mat.SetFloat("_Metallic", 1f);
        }
        else
        {
            mat.SetFloat("_Metallic", 0.05f);
        }

        mat.SetFloat("_Smoothness", smoothness);

        if (transparent)
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = 3000;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_SURFACE_TYPE_OPAQUE");
        }
        else
        {
            mat.SetFloat("_Surface", 0f);
            mat.SetOverrideTag("RenderType", "Opaque");
            mat.SetInt("_ZWrite", 1);
            mat.renderQueue = -1;
            mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }

        EditorUtility.SetDirty(mat);
        return mat;
    }
}
#endif
