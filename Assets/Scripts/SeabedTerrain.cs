using UnityEngine;

/// <summary>
/// 生成海床网格：沙丘状起伏地形（分层正弦叠加）。
/// 模式与 OceanSurface 一致：编辑模式构建并保存为 Assets/Meshes/Seabed.asset。
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class SeabedTerrain : MonoBehaviour
{
    [Tooltip("每边的分段数（200 约 4 万顶点）")]
    [Range(8, 400)]
    public int resolution = 200;

    [Tooltip("网格边长（米）")]
    public float size = 2400f;

    [Tooltip("沙丘起伏高度（米）")]
    public float duneHeight = 2.5f;

    [Tooltip("碗状边缘上翘高度（米），与水面封死防止水下看到天空盒")]
    public float rimHeight = 17f;

    private Mesh _mesh;

    private void OnEnable()
    {
        Rebuild();
    }

    private void OnValidate()
    {
        Rebuild();
    }

    private void Rebuild()
    {
        int res = Mathf.Clamp(resolution, 8, 400);
        Mesh mesh = GetOrCreateMesh();

        int side = res + 1;
        Vector3[] vertices = new Vector3[side * side];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[res * res * 6];

        float half = size * 0.5f;
        float step = size / res;

        for (int z = 0; z < side; z++)
        {
            for (int x = 0; x < side; x++)
            {
                int i = z * side + x;
                float wx = -half + x * step;
                float wz = -half + z * step;
                vertices[i] = new Vector3(wx, DuneHeight(wx, wz), wz);
                uvs[i] = new Vector2((float)x / res, (float)z / res);
            }
        }

        int ti = 0;
        for (int z = 0; z < res; z++)
        {
            for (int x = 0; x < res; x++)
            {
                int i = z * side + x;
                triangles[ti++] = i;
                triangles[ti++] = i + side;
                triangles[ti++] = i + 1;
                triangles[ti++] = i + 1;
                triangles[ti++] = i + side;
                triangles[ti++] = i + side + 1;
            }
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != mesh)
        {
            mf.sharedMesh = mesh;
        }

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(mesh);
#endif
    }

    /// <summary>分层正弦叠加出的沙丘高度 + 碗状边缘上翘。</summary>
    private float DuneHeight(float x, float z)
    {
        float h = 0f;
        h += Mathf.Sin(x * 0.021f + Mathf.Sin(z * 0.013f) * 1.7f) * 1.0f;
        h += Mathf.Sin(z * 0.032f + Mathf.Sin(x * 0.017f) * 1.3f) * 0.6f;
        h += Mathf.Sin((x + z) * 0.058f) * 0.25f;
        h *= duneHeight;

        // 边缘 78%~100% 半径处上翘成碗状，与水面封闭，防止从水下看到天空盒缝隙
        float r = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
        float half = size * 0.5f;
        float rimT = Mathf.Clamp01((r - half * 0.78f) / (half * 0.22f));
        h += rimT * rimT * rimHeight;

        return h;
    }

    /// <summary>获取世界坐标 (x, z) 处的海床世界高度（含碗状边缘）。</summary>
    public float GetWorldSeabedHeight(float x, float z)
    {
        return transform.position.y + DuneHeight(x, z);
    }

    private Mesh GetOrCreateMesh()
    {
#if UNITY_EDITOR
        if (_mesh == null && !Application.isPlaying)
        {
            const string parent = "Assets";
            const string folder = "Assets/Meshes";
            const string path = "Assets/Meshes/Seabed.asset";

            if (!UnityEditor.AssetDatabase.IsValidFolder(folder))
            {
                UnityEditor.AssetDatabase.CreateFolder(parent, "Meshes");
            }

            _mesh = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (_mesh == null)
            {
                _mesh = new Mesh { name = "Seabed" };
                _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                UnityEditor.AssetDatabase.CreateAsset(_mesh, path);
            }
        }
#endif

        if (_mesh == null)
        {
            _mesh = new Mesh { name = "Seabed" };
            _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        }

        return _mesh;
    }
}