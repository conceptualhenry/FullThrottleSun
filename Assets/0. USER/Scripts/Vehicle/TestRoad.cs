using System.Collections.Generic;
using UnityEngine;

namespace FullThrottleSun.Vehicle
{
    /// <summary>
    /// Builds a closed-loop road through the given control points (Catmull-Rom), in edit mode and at runtime.
    /// Sections along the loop can use another material and grip, through a GroundSurface on that piece.
    /// Also places lane markings, roadside posts and scattered blocks as visual reference.
    /// </summary>
    [ExecuteAlways]
    public class TestRoad : MonoBehaviour
    {
        [System.Serializable]
        public class Section
        {
            public string name = "Section";
            [Tooltip("Start and end along the loop, 0 to 1 from the first control point.")]
            [Range(0f, 1f)] public float start;
            [Range(0f, 1f)] public float end = 0.1f;
            public Material material;
            [Min(0f)] public float gripMultiplier = 1f;
            [Min(0f)] public float rollingResistanceMultiplier = 1f;
        }

        const string GeneratedName = "Generated";
        const float MarkingLift = 0.01f;

        [Tooltip("Control points on the XZ plane, in this object's local space. The road loops back to the first one.")]
        [SerializeField] Vector2[] points =
        {
            new Vector2(0f, -30f), new Vector2(0f, 30f), new Vector2(0f, 90f), new Vector2(20f, 125f),
            new Vector2(60f, 135f), new Vector2(95f, 115f), new Vector2(105f, 75f), new Vector2(85f, 45f),
            new Vector2(95f, 10f), new Vector2(110f, -25f), new Vector2(95f, -60f), new Vector2(55f, -75f),
            new Vector2(20f, -65f),
        };
        [Min(1f)] [SerializeField] float width = 10f;
        [Tooltip("Height above this object, keeps the road from z-fighting with the ground below.")]
        [SerializeField] float height = 0.02f;
        [Range(2, 100)] [SerializeField] int samplesPerSegment = 24;
        [SerializeField] Material asphalt;
        [SerializeField] Section[] sections = new Section[0];

        [Header("Lane markings")]
        [SerializeField] bool markings = true;
        [SerializeField] Material markingMaterial;
        [Min(0.05f)] [SerializeField] float lineWidth = 0.2f;
        [Tooltip("Distance of the edge lines from the road edge (m).")]
        [Min(0f)] [SerializeField] float edgeLineInset = 0.4f;
        [Min(0.5f)] [SerializeField] float dashLength = 3f;
        [Min(0.5f)] [SerializeField] float dashGap = 4f;

        [Header("Roadside posts")]
        [SerializeField] bool posts = true;
        [SerializeField] Material postMaterialA;
        [SerializeField] Material postMaterialB;
        [Min(1f)] [SerializeField] float postSpacing = 10f;
        [Tooltip("Distance of the posts outside the road edge (m).")]
        [Min(0f)] [SerializeField] float postOffset = 1.5f;
        [Min(0.1f)] [SerializeField] float postHeight = 1.2f;
        [SerializeField] bool postColliders;

        [Header("Scenery blocks")]
        [SerializeField] bool scenery = true;
        [SerializeField] Material[] sceneryMaterials = new Material[0];
        [Min(0)] [SerializeField] int sceneryCount = 70;
        [Tooltip("How far past the road's bounding box blocks can appear (m).")]
        [Min(0f)] [SerializeField] float sceneryMargin = 60f;
        [Tooltip("Minimum gap between a block and the road edge (m).")]
        [Min(0f)] [SerializeField] float sceneryClearance = 8f;
        [SerializeField] Vector2 blockSize = new Vector2(2f, 7f);
        [SerializeField] Vector2 blockHeight = new Vector2(2f, 14f);
        [SerializeField] int scenerySeed = 1;

        readonly List<Mesh> meshes = new List<Mesh>();

        void OnEnable() => Rebuild();

        void OnDisable() => ClearMeshes();

#if UNITY_EDITOR
        void OnValidate()
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null && isActiveAndEnabled)
                    Rebuild();
            };
        }
#endif

        [ContextMenu("Rebuild")]
        public void Rebuild()
        {
            if (points == null || points.Length < 3)
                return;

            Transform root = transform.Find(GeneratedName);
            if (root == null)
            {
                root = new GameObject(GeneratedName).transform;
                root.SetParent(transform, false);
            }
            for (int i = root.childCount - 1; i >= 0; i--)
                DestroyImmediate(root.GetChild(i).gameObject);
            ClearMeshes();

            List<Vector3> centre = SampleLoop();
            float[] distances = CumulativeDistances(centre);

            BuildRoad(root, centre);
            if (markings)
                BuildMarkings(CreateGroup(root, "Markings"), centre, distances);
            if (posts)
                BuildPosts(CreateGroup(root, "Posts"), centre, distances);
            if (scenery)
                BuildScenery(CreateGroup(root, "Scenery"), centre);
        }

        void BuildRoad(Transform root, List<Vector3> centre)
        {
            int count = centre.Count;
            var cuts = new SortedSet<int> { 0, count };
            foreach (var section in sections)
            {
                cuts.Add(ToIndex(section.start, count));
                cuts.Add(ToIndex(section.end, count));
            }

            int pieceStart = -1;
            foreach (int cut in cuts)
            {
                if (pieceStart >= 0 && cut > pieceStart)
                {
                    Section section = FindSection((pieceStart + cut) * 0.5f / count);
                    string pieceName = section != null ? section.name : "Asphalt";
                    Mesh mesh = BuildStrip(pieceName, centre, pieceStart, cut, 0f, width, 0f);
                    Material material = section != null && section.material != null ? section.material : asphalt;
                    GameObject piece = CreateMeshObject(root, pieceName, mesh, material);
                    piece.AddComponent<MeshCollider>().sharedMesh = mesh;

                    if (section != null)
                    {
                        var surface = piece.AddComponent<GroundSurface>();
                        surface.gripMultiplier = section.gripMultiplier;
                        surface.rollingResistanceMultiplier = section.rollingResistanceMultiplier;
                    }
                }
                pieceStart = cut;
            }
        }

        void BuildMarkings(Transform group, List<Vector3> centre, float[] distances)
        {
            int count = centre.Count;
            float edge = width * 0.5f - edgeLineInset;
            CreateMeshObject(group, "EdgeLeft", BuildStrip("EdgeLeft", centre, 0, count, -edge, lineWidth, MarkingLift), markingMaterial);
            CreateMeshObject(group, "EdgeRight", BuildStrip("EdgeRight", centre, 0, count, edge, lineWidth, MarkingLift), markingMaterial);

            float total = distances[count];
            for (float d = 0f; d + dashLength <= total; d += dashLength + dashGap)
            {
                int from = IndexAtDistance(distances, d);
                int to = Mathf.Max(from + 1, IndexAtDistance(distances, d + dashLength));
                CreateMeshObject(group, "Dash", BuildStrip("Dash", centre, from, to, 0f, lineWidth, MarkingLift), markingMaterial);
            }
        }

        void BuildPosts(Transform group, List<Vector3> centre, float[] distances)
        {
            int count = centre.Count;
            float total = distances[count];
            int postCount = Mathf.Max(1, Mathf.RoundToInt(total / postSpacing));
            float lateral = width * 0.5f + postOffset;

            for (int p = 0; p < postCount; p++)
            {
                float d = total * p / postCount;
                int i = IndexAtDistance(distances, d) % count;
                Vector3 right = RightAt(centre, i);
                Material material = p % 2 == 0 ? postMaterialA : postMaterialB;

                for (int side = -1; side <= 1; side += 2)
                {
                    var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    post.name = "Post";
                    post.transform.SetParent(group, false);
                    post.transform.localPosition = centre[i] + right * (lateral * side) + Vector3.up * (postHeight * 0.5f);
                    post.transform.localScale = new Vector3(0.2f, postHeight * 0.5f, 0.2f);
                    post.isStatic = gameObject.isStatic;
                    post.GetComponent<MeshRenderer>().sharedMaterial = material;
                    if (!postColliders)
                        DestroyImmediate(post.GetComponent<Collider>());
                }
            }
        }

        void BuildScenery(Transform group, List<Vector3> centre)
        {
            Vector3 min = centre[0], max = centre[0];
            foreach (var c in centre)
            {
                min = Vector3.Min(min, c);
                max = Vector3.Max(max, c);
            }
            min -= new Vector3(sceneryMargin, 0f, sceneryMargin);
            max += new Vector3(sceneryMargin, 0f, sceneryMargin);

            var random = new System.Random(scenerySeed);
            float Range(float a, float b) => a + (float)random.NextDouble() * (b - a);

            int placed = 0;
            for (int attempt = 0; attempt < sceneryCount * 20 && placed < sceneryCount; attempt++)
            {
                float size = Range(blockSize.x, blockSize.y);
                float depth = Range(blockSize.x, blockSize.y);
                float tall = Range(blockHeight.x, blockHeight.y);
                var position = new Vector3(Range(min.x, max.x), 0f, Range(min.z, max.z));

                float keepOut = width * 0.5f + sceneryClearance + Mathf.Max(size, depth) * 0.75f;
                if (DistanceToRoad(centre, position) < keepOut)
                    continue;

                var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                block.name = "Block";
                block.transform.SetParent(group, false);
                block.transform.localPosition = position + Vector3.up * (tall * 0.5f);
                block.transform.localRotation = Quaternion.Euler(0f, Range(0f, 360f), 0f);
                block.transform.localScale = new Vector3(size, tall, depth);
                block.isStatic = gameObject.isStatic;
                if (sceneryMaterials.Length > 0)
                    block.GetComponent<MeshRenderer>().sharedMaterial = sceneryMaterials[random.Next(sceneryMaterials.Length)];
                placed++;
            }
        }

        List<Vector3> SampleLoop()
        {
            var result = new List<Vector3>(points.Length * samplesPerSegment);
            int n = points.Length;
            for (int i = 0; i < n; i++)
            {
                Vector2 p0 = points[(i - 1 + n) % n];
                Vector2 p1 = points[i];
                Vector2 p2 = points[(i + 1) % n];
                Vector2 p3 = points[(i + 2) % n];
                for (int s = 0; s < samplesPerSegment; s++)
                {
                    Vector2 p = CatmullRom(p0, p1, p2, p3, (float)s / samplesPerSegment);
                    result.Add(new Vector3(p.x, height, p.y));
                }
            }
            return result;
        }

        /// <summary>distances[i] is the length along the loop up to sample i; distances[count] is the full loop.</summary>
        static float[] CumulativeDistances(List<Vector3> centre)
        {
            int count = centre.Count;
            var distances = new float[count + 1];
            for (int i = 1; i <= count; i++)
                distances[i] = distances[i - 1] + Vector3.Distance(centre[i - 1], centre[i % count]);
            return distances;
        }

        static int IndexAtDistance(float[] distances, float d)
        {
            int index = System.Array.BinarySearch(distances, d);
            return index >= 0 ? index : Mathf.Min(~index, distances.Length - 1);
        }

        static Vector3 RightAt(List<Vector3> centre, int i)
        {
            int count = centre.Count;
            Vector3 tangent = centre[(i + 1) % count] - centre[(i - 1 + count) % count];
            return Vector3.Cross(Vector3.up, tangent).normalized;
        }

        static float DistanceToRoad(List<Vector3> centre, Vector3 position)
        {
            float best = float.MaxValue;
            int count = centre.Count;
            for (int i = 0; i < count; i++)
            {
                Vector3 a = centre[i];
                Vector3 b = centre[(i + 1) % count];
                Vector3 ab = new Vector3(b.x - a.x, 0f, b.z - a.z);
                Vector3 ap = new Vector3(position.x - a.x, 0f, position.z - a.z);
                float t = Mathf.Clamp01(Vector3.Dot(ap, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
                best = Mathf.Min(best, (ap - ab * t).magnitude);
            }
            return best;
        }

        /// <summary>Flat strip following the centreline from sample `from` to `to`, shifted sideways by `lateral`.</summary>
        Mesh BuildStrip(string meshName, List<Vector3> centre, int from, int to, float lateral, float stripWidth, float lift)
        {
            int count = centre.Count;
            int rows = to - from + 1;
            var vertices = new Vector3[rows * 2];
            var uvs = new Vector2[rows * 2];
            var triangles = new int[(rows - 1) * 6];
            float distance = 0f;

            for (int r = 0; r < rows; r++)
            {
                int i = (from + r) % count;
                Vector3 right = RightAt(centre, i);
                Vector3 c = centre[i] + right * lateral + Vector3.up * lift;
                if (r > 0)
                    distance += Vector3.Distance(centre[i], centre[(i - 1 + count) % count]);

                vertices[r * 2] = c - right * (stripWidth * 0.5f);
                vertices[r * 2 + 1] = c + right * (stripWidth * 0.5f);
                uvs[r * 2] = new Vector2(0f, distance / stripWidth);
                uvs[r * 2 + 1] = new Vector2(1f, distance / stripWidth);

                if (r == rows - 1)
                    continue;
                int v = r * 2;
                int t = r * 6;
                triangles[t] = v;
                triangles[t + 1] = v + 2;
                triangles[t + 2] = v + 1;
                triangles[t + 3] = v + 1;
                triangles[t + 4] = v + 2;
                triangles[t + 5] = v + 3;
            }

            var mesh = new Mesh { name = meshName, hideFlags = HideFlags.DontSave };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            meshes.Add(mesh);
            return mesh;
        }

        GameObject CreateMeshObject(Transform parent, string objectName, Mesh mesh, Material material)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            go.isStatic = gameObject.isStatic;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        static Transform CreateGroup(Transform root, string groupName)
        {
            var group = new GameObject(groupName).transform;
            group.SetParent(root, false);
            return group;
        }

        Section FindSection(float t)
        {
            foreach (var section in sections)
                if (t >= section.start && t < section.end)
                    return section;
            return null;
        }

        static int ToIndex(float t, int count) => Mathf.Clamp(Mathf.RoundToInt(t * count), 0, count);

        void ClearMeshes()
        {
            foreach (var mesh in meshes)
                if (mesh != null)
                    DestroyImmediate(mesh);
            meshes.Clear();
        }

        static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t3);
        }
    }
}
