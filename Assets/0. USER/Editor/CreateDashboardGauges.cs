#if UNITY_EDITOR
using FullThrottleSun.Vehicle;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds editable dashboard gauges under Car. Materials are real assets so
/// color/position changes in the Scene view survive Play Mode and reloads.
/// </summary>
public static class CreateDashboardGauges
{
    const string MatFolder = "Assets/0. USER/Materials/Dashboard";

    [MenuItem("Tools/Dashboard Gauges/Create Or Repair On Car")]
    public static void CreateOrRepair()
    {
        var car = GameObject.Find("Car");
        if (car == null)
        {
            Debug.LogError("[DashboardGauges] Car not found.");
            return;
        }

        EnsureMaterialAssets();
        var dialMat = AssetDatabase.LoadAssetAtPath<Material>(MatFolder + "/Gauge_Dial.mat");
        var needleMat = AssetDatabase.LoadAssetAtPath<Material>(MatFolder + "/Gauge_Needle.mat");

        Transform rootTf = car.transform.Find("DashboardGauges");
        GameObject root;
        bool created = false;
        if (rootTf == null)
        {
            root = new GameObject("DashboardGauges");
            root.transform.SetParent(car.transform, false);
            root.transform.localPosition = new Vector3(-0.35f, 0.95f, 0.78f);
            root.transform.localRotation = Quaternion.identity;
            created = true;
        }
        else
        {
            root = rootTf.gameObject;
        }

        Transform speedPivot = EnsureGauge(root.transform, "Speedometer", new Vector3(0.09f, 0f, 0f), dialMat, needleMat, created);
        Transform rpmPivot = EnsureGauge(root.transform, "Tachometer", new Vector3(-0.09f, 0f, 0f), dialMat, needleMat, created);
        TextMesh gearLabel = EnsureGearLabel(root.transform, created);

        var dash = root.GetComponent<DashboardGauges>();
        if (dash == null)
            dash = root.AddComponent<DashboardGauges>();

        var so = new SerializedObject(dash);
        so.FindProperty("car").objectReferenceValue = car.GetComponent<TireFrictionCar>();
        so.FindProperty("speedNeedle").objectReferenceValue = speedPivot;
        so.FindProperty("rpmNeedle").objectReferenceValue = rpmPivot;
        so.FindProperty("gearLabel").objectReferenceValue = gearLabel;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(root);
        EditorUtility.SetDirty(dash);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Selection.activeGameObject = root;
        Debug.Log(created
            ? "[DashboardGauges] Created under Car. Drag Speedometer / Tachometer in Scene view; edit materials under Assets/0. USER/Materials/Dashboard."
            : "[DashboardGauges] Repaired materials + refs. Existing positions kept.");
    }

    [MenuItem("Tools/Dashboard Gauges/Force Recreate (resets positions)")]
    public static void ForceRecreate()
    {
        var car = GameObject.Find("Car");
        if (car == null)
        {
            Debug.LogError("[DashboardGauges] Car not found.");
            return;
        }

        var existing = car.transform.Find("DashboardGauges");
        if (existing != null)
            Object.DestroyImmediate(existing.gameObject);

        CreateOrRepair();
    }

    static void EnsureMaterialAssets()
    {
        if (!AssetDatabase.IsValidFolder("Assets/0. USER/Materials"))
            AssetDatabase.CreateFolder("Assets/0. USER", "Materials");
        if (!AssetDatabase.IsValidFolder(MatFolder))
            AssetDatabase.CreateFolder("Assets/0. USER/Materials", "Dashboard");

        EnsureMat("Gauge_Dial.mat", new Color(0.12f, 0.12f, 0.14f), 0.25f);
        EnsureMat("Gauge_Needle.mat", new Color(0.85f, 0.12f, 0.1f), 0.4f);
        AssetDatabase.SaveAssets();
    }

    static void EnsureMat(string fileName, Color color, float smooth)
    {
        string path = MatFolder + "/" + fileName;
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null)
            return;

        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null)
            lit = Shader.Find("Standard");

        mat = new Material(lit) { name = System.IO.Path.GetFileNameWithoutExtension(fileName) };
        ApplyColor(mat, color, smooth);
        AssetDatabase.CreateAsset(mat, path);
    }

    static void ApplyColor(Material m, Color c, float smooth)
    {
        if (m.HasProperty("_BaseColor"))
            m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color"))
            m.SetColor("_Color", c);
        if (m.HasProperty("_Smoothness"))
            m.SetFloat("_Smoothness", smooth);
    }

    /// <summary>
    /// Creates a gauge only when missing. When <paramref name="forceDefaults"/> is false,
    /// keeps local transform of an existing gauge and only reassigns materials.
    /// </summary>
    static Transform EnsureGauge(
        Transform parent,
        string name,
        Vector3 defaultLocalPos,
        Material dialMat,
        Material needleMat,
        bool forceDefaults)
    {
        Transform gaugeTf = parent.Find(name);
        GameObject gauge;
        if (gaugeTf == null)
        {
            gauge = new GameObject(name);
            gauge.transform.SetParent(parent, false);
            gauge.transform.localPosition = defaultLocalPos;
            gauge.transform.localRotation = Quaternion.identity;
            gauge.transform.localScale = Vector3.one;
        }
        else
        {
            gauge = gaugeTf.gameObject;
            if (forceDefaults)
                gauge.transform.localPosition = defaultLocalPos;
        }

        // Drop leftover Rim cylinders from older builds (two solid discs looked stacked).
        Transform oldRim = gauge.transform.Find("Rim");
        if (oldRim != null)
            Object.DestroyImmediate(oldRim.gameObject);

        EnsurePrimitive(gauge.transform, "Dial", PrimitiveType.Cylinder,
            new Vector3(0f, 0f, 0f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.12f, 0.004f, 0.12f), dialMat);

        Transform pivotTf = gauge.transform.Find("NeedlePivot");
        if (pivotTf == null)
        {
            var pivotGo = new GameObject("NeedlePivot");
            pivotTf = pivotGo.transform;
            pivotTf.SetParent(gauge.transform, false);
            pivotTf.localPosition = new Vector3(0f, 0f, -0.006f);
            pivotTf.localRotation = Quaternion.identity;
            pivotTf.localScale = Vector3.one;
        }

        EnsurePrimitive(pivotTf, "Needle", PrimitiveType.Cube,
            new Vector3(0f, 0.028f, 0f), Quaternion.identity, new Vector3(0.004f, 0.055f, 0.002f), needleMat);

        return pivotTf;
    }

    static TextMesh EnsureGearLabel(Transform parent, bool forceDefaults)
    {
        Transform tf = parent.Find("GearLabel");
        GameObject go;
        if (tf == null)
        {
            go = new GameObject("GearLabel");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0.2f, 0f, -0.01f);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
        }
        else
        {
            go = tf.gameObject;
            if (forceDefaults)
                go.transform.localPosition = new Vector3(0.2f, 0f, -0.01f);
        }

        var text = go.GetComponent<TextMesh>();
        if (text == null)
            text = go.AddComponent<TextMesh>();

        text.text = "D";
        text.fontSize = 64;
        text.characterSize = 0.012f;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.color = new Color(0.35f, 0.95f, 0.45f);
        text.fontStyle = FontStyle.Bold;
        EditorUtility.SetDirty(text);
        return text;
    }

    static void EnsurePrimitive(
        Transform parent,
        string name,
        PrimitiveType type,
        Vector3 localPos,
        Quaternion localRot,
        Vector3 localScale,
        Material mat)
    {
        Transform tf = parent.Find(name);
        GameObject go;
        if (tf == null)
        {
            go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = localScale;
            var col = go.GetComponent<Collider>();
            if (col != null)
                Object.DestroyImmediate(col);
        }
        else
        {
            go = tf.gameObject;
        }

        var renderer = go.GetComponent<MeshRenderer>();
        if (renderer != null && mat != null)
        {
            renderer.sharedMaterial = mat;
            EditorUtility.SetDirty(renderer);
        }
    }
}
#endif
