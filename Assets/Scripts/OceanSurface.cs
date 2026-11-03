using UnityEngine;

/// <summary>
/// 生成高细分海面网格并赋给 MeshFilter。
/// Unity 默认 Plane 只有 10x10 顶点，顶点位移看不出波浪，故需要重新生成网格。
/// 编辑模式下网格会存成 Assets/Meshes/OceanGrid.asset，避免把大网格塞进场景文件。
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class OceanSurface : MonoBehaviour
{
    [Tooltip("每边的分段数，越高越平滑（300 约 9 万顶点）")]
    [Range(8, 512)]
    public int resolution = 300;

    [Tooltip("网格边长（米）")]
    public float size = 3000f;

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
        int res = Mathf.Clamp(resolution, 8, 512);
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
                vertices[i] = new Vector3(-half + x * step, 0f, -half + z * step);
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

    private Mesh GetOrCreateMesh()
    {
#if UNITY_EDITOR
        if (_mesh == null && !Application.isPlaying)
        {
            const string parent = "Assets";
            const string folder = "Assets/Meshes";
            const string path = "Assets/Meshes/OceanGrid.asset";

            if (!UnityEditor.AssetDatabase.IsValidFolder(folder))
            {
                UnityEditor.AssetDatabase.CreateFolder(parent, "Meshes");
            }

            _mesh = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (_mesh == null)
            {
                _mesh = new Mesh { name = "OceanGrid" };
                _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                UnityEditor.AssetDatabase.CreateAsset(_mesh, path);
            }
        }
#endif

        if (_mesh == null)
        {
            _mesh = new Mesh { name = "OceanGrid" };
            _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        }

        return _mesh;
    }
}