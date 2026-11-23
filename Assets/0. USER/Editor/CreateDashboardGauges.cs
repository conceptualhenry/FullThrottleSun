#if UNITY_EDITOR
using FullThrottleSun.Vehicle;
using UnityEditor;
using UnityEngine;

public static class CreateDashboardGauges
{
    [MenuItem("Tools/Create Dashboard Gauges On Car")]
    public static void Run()
    {
        var car = GameObject.Find("Car");
        if (car == null)
        {
            Debug.LogError("Car not found");
            return;
        }

        var existing = car.transform.Find("DashboardGauges");
        if (existing != null)
            Object.DestroyImmediate(existing.gameObject);

        var root = new GameObject("DashboardGauges");
        root.transform.SetParent(car.transform, false);
        root.transform.localPosition = new Vector3(-0.35f, 0.95f, 0.78f);
        root.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null)
            lit = Shader.Find("Standard");

        var dialMat = MakeMat(lit, new Color(0.12f, 0.12f, 0.14f), 0.25f);
        var rimMat = MakeMat(lit, new Color(0.55f, 0.55f, 0.58f), 0.7f);
        var needleMat = MakeMat(lit, new Color(0.85f, 0.12f, 0.1f), 0.4f);

        Transform speedPivot = MakeGauge(root.transform, "Speedometer", new Vector3(0.09f, 0f, 0f), dialMat, rimMat, needleMat);
        Transform rpmPivot = MakeGauge(root.transform, "Tachometer", new Vector3(-0.09f, 0f, 0f), dialMat, rimMat, needleMat);

        var dash = root.AddComponent<DashboardGauges>();
        var so = new SerializedObject(dash);
        so.FindProperty("car").objectReferenceValue = car.GetComponent<TireFrictionCar>();
        so.FindProperty("speedNeedle").objectReferenceValue = speedPivot;
        so.FindProperty("rpmNeedle").objectReferenceValue = rpmPivot;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(root);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("[DashboardGauges] Created under Car.");
    }

    static Material MakeMat(Shader shader, Color c, float smooth)
    {
        var m = new Material(shader) { name = "GaugeMat", hideFlags = HideFlags.DontSave };
        if (m.HasProperty("_BaseColor"))
            m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color"))
            m.SetColor("_Color", c);
        if (m.HasProperty("_Smoothness"))
            m.SetFloat("_Smoothness", smooth);
        return m;
    }

    static Transform MakeGauge(Transform parent, string name, Vector3 localPos, Material dialMat, Material rimMat, Material needleMat)
    {
        var gauge = new GameObject(name);
        gauge.transform.SetParent(parent, false);
        gauge.transform.localPosition = localPos;

        var dial = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        dial.name = "Dial";
        dial.transform.SetParent(gauge.transform, false);
        dial.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        dial.transform.localScale = new Vector3(0.12f, 0.004f, 0.12f);
        Object.DestroyImmediate(dial.GetComponent<Collider>());
        dial.GetComponent<MeshRenderer>().sharedMaterial = dialMat;

        var rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rim.name = "Rim";
        rim.transform.SetParent(gauge.transform, false);
        rim.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        rim.transform.localPosition = new Vector3(0f, 0f, -0.001f);
        rim.transform.localScale = new Vector3(0.128f, 0.003f, 0.128f);
        Object.DestroyImmediate(rim.GetComponent<Collider>());
        rim.GetComponent<MeshRenderer>().sharedMaterial = rimMat;

        var pivot = new GameObject("NeedlePivot");
        pivot.transform.SetParent(gauge.transform, false);
        pivot.transform.localPosition = new Vector3(0f, 0f, -0.006f);

        var needle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        needle.name = "Needle";
        needle.transform.SetParent(pivot.transform, false);
        needle.transform.localPosition = new Vector3(0f, 0.028f, 0f);
        needle.transform.localScale = new Vector3(0.004f, 0.055f, 0.002f);
        Object.DestroyImmediate(needle.GetComponent<Collider>());
        needle.GetComponent<MeshRenderer>().sharedMaterial = needleMat;

        return pivot.transform;
    }
}
#endif
