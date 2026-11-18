using UnityEngine;

namespace FullThrottleSun.Vehicle
{
    /// <summary>
    /// Builds a real 3×3×3 cubie Rubik's cube under <c>Cubies</c> (not sticker quads).
    /// Physics / grab stay on the root; this only creates the visual pieces.
    /// </summary>
    [ExecuteAlways]
    public class RubiksCubeBuilder : MonoBehaviour
    {
        [SerializeField] float size = 0.055f;
        [SerializeField] float gap = 0.0015f;
        [SerializeField] bool rebuildOnValidate;

        public float Size => size;
        public float CubieSize => (size - gap * 2f) / 3f;

        static readonly Color Black = new Color(0.07f, 0.07f, 0.07f);
        static readonly Color White = new Color(0.95f, 0.95f, 0.95f);
        static readonly Color Yellow = new Color(0.95f, 0.85f, 0.1f);
        static readonly Color Red = new Color(0.85f, 0.1f, 0.12f);
        static readonly Color Orange = new Color(0.95f, 0.45f, 0.08f);
        static readonly Color Blue = new Color(0.12f, 0.35f, 0.85f);
        static readonly Color Green = new Color(0.12f, 0.7f, 0.25f);

        void OnEnable()
        {
            if (transform.Find("Cubies") == null)
                Rebuild();
        }

        void OnValidate()
        {
            if (rebuildOnValidate && isActiveAndEnabled)
                Rebuild();
        }

        [ContextMenu("Rebuild Cubies")]
        public void Rebuild()
        {
            // Remove legacy sticker visual if present.
            DestroyChild("Visual");
            DestroyChild("Cubies");
            DestroyChild("TurnPivot");

            var root = new GameObject("Cubies");
            root.transform.SetParent(transform, false);

            float cubie = CubieSize;
            float step = cubie + gap;

            for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
            for (int z = -1; z <= 1; z++)
            {
                if (x == 0 && y == 0 && z == 0)
                    continue; // invisible core

                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = $"Cubie_{x}_{y}_{z}";
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = new Vector3(x, y, z) * step;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one * cubie;
                StripCollider(go);
                SetBodyColor(go, Black);
                AttachStickers(go.transform, cubie, x, y, z);

                var piece = go.AddComponent<RubiksCubie>();
                piece.SetCoord(new Vector3Int(x, y, z));
            }
        }

        void AttachStickers(Transform cubie, float cubieSize, int x, int y, int z)
        {
            // Cubie already has localScale = cubieSize, so sticker transforms are in
            // unit-cube space (1 = full cubie). Using world meters here would shrink
            // stickers by cubieSize again and make them invisible (all-black cube).
            const float face = 0.51f;
            const float sticker = 0.86f;
            const float depth = 0.06f;

            if (y == 1) MakeSticker(cubie, "U", White, Vector3.up * face, Quaternion.Euler(90f, 0f, 0f), sticker, depth);
            if (y == -1) MakeSticker(cubie, "D", Yellow, Vector3.down * face, Quaternion.Euler(-90f, 0f, 0f), sticker, depth);
            if (z == 1) MakeSticker(cubie, "F", Red, Vector3.forward * face, Quaternion.identity, sticker, depth);
            if (z == -1) MakeSticker(cubie, "B", Orange, Vector3.back * face, Quaternion.Euler(0f, 180f, 0f), sticker, depth);
            if (x == -1) MakeSticker(cubie, "L", Blue, Vector3.left * face, Quaternion.Euler(0f, -90f, 0f), sticker, depth);
            if (x == 1) MakeSticker(cubie, "R", Green, Vector3.right * face, Quaternion.Euler(0f, 90f, 0f), sticker, depth);
        }

        static void MakeSticker(Transform parent, string name, Color color, Vector3 localPos, Quaternion localRot, float sticker, float depth)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = new Vector3(sticker, sticker, depth);
            StripCollider(go);
            SetStickerColor(go, color);
        }

        void DestroyChild(string childName)
        {
            // Destroy every match — Find() only returns the first, and repeated
            // Rebuilds used to stack duplicate Cubies roots.
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform t = transform.GetChild(i);
                if (t == null || t.name != childName)
                    continue;
                if (Application.isPlaying)
                    Destroy(t.gameObject);
                else
                    DestroyImmediate(t.gameObject);
            }
        }

        static void StripCollider(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col == null)
                return;
            if (Application.isPlaying)
                Destroy(col);
            else
                DestroyImmediate(col);
        }

        static void SetBodyColor(GameObject go, Color color)
        {
            ApplyColor(go, color, lit: true);
        }

        static void SetStickerColor(GameObject go, Color color)
        {
            // Unlit so sticker colors stay readable in the dark cockpit.
            ApplyColor(go, color, lit: false);
        }

        static void ApplyColor(GameObject go, Color color, bool lit)
        {
            var rend = go.GetComponent<Renderer>();
            if (rend == null)
                return;

            Shader shader = null;
            if (!lit)
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            var mat = new Material(shader) { name = lit ? "RubikBody" : "RubikSticker" };
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", color);
            if (mat.HasProperty("_Smoothness"))
                mat.SetFloat("_Smoothness", 0.35f);
            rend.sharedMaterial = mat;
        }
    }

    /// <summary>Tracks a cubie's logical grid coordinate (-1..1).</summary>
    public class RubiksCubie : MonoBehaviour
    {
        public Vector3Int Coord { get; private set; }

        public void SetCoord(Vector3Int coord) => Coord = coord;
    }
}
