using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 程序化生成礁石与珊瑚：礁石 = 噪声变形球；珊瑚 = 递归分支锥管 + 顶点色。
/// 编辑模式生成并保存进场景，材质使用 Custom/Reef（带焦散与雾）。
/// </summary>
[ExecuteAlways]
public class ReefSpawner : MonoBehaviour
{
    public int rockCount = 24;
    public int coralCount = 110;
    [Tooltip("散布半径（米）")]
    public float area = 60f;
    public int seed = 1234;

    static readonly Color[] CoralPalette =
    {
        new Color(1f, 0.45f, 0.15f),   // 橙
        new Color(0.95f, 0.25f, 0.55f),// 品红
        new Color(1f, 0.38f, 0.5f),    // 粉
        new Color(0.6f, 0.3f, 0.95f),  // 紫
        new Color(0.2f, 0.8f, 0.7f),   // 青
        new Color(1f, 0.75f, 0.2f),    // 黄
        new Color(0.35f, 0.75f, 0.4f), // 绿
    };

    private readonly List<Vector3> _rockSpots = new List<Vector3>();
    private readonly List<Vector3> _coralSpots = new List<Vector3>();

    /// <summary>程序化障碍（世界坐标胶囊线段 + 半径）：供鱼群做碰撞回避，每次 Rebuild 重建。
    /// 珊瑚=每根枝条一条胶囊，鱼可钻进丛内只避开实际枝条；礁石=单点球。</summary>
    public struct Obstacle
    {
        public Vector3 a;       // 胶囊线段起点
        public Vector3 b;       // 终点（球形障碍时 a == b）
        public float radius;
    }

    public static readonly List<Obstacle> Obstacles = new List<Obstacle>();

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
        Material mat = EnsureMaterial();
        if (mat == null)
        {
            return;
        }

        ClearChildren();

        System.Random rng = new System.Random(seed);
        SeabedTerrain seabed = FindFirstObjectByType<SeabedTerrain>();
        _rockSpots.Clear();
        _coralSpots.Clear();
        Obstacles.Clear();

        Transform rockGroup = EnsureGroup("礁石");
        Transform coralGroup = EnsureGroup("珊瑚");

        // 礁石成簇分布：3~4 个簇中心，八成礁石长在簇内，形成礁脊
        int clusters = 3 + rng.Next(2);
        List<Vector3> clusterCenters = new List<Vector3>();
        for (int c = 0; c < clusters; c++)
        {
            clusterCenters.Add(PickSpot(rng, seabed, 0.72f));
        }
        // 珊瑚花园：礁簇核心即花园，再补 2 个纯花园中心——珊瑚八成密集种在花园里，成片不成"一颗颗"
        for (int c = 0; c < 2; c++)
        {
            clusterCenters.Add(PickSpot(rng, seabed, 0.8f));
        }
        for (int i = 0; i < rockCount; i++)
        {
            Vector3? center = clusterCenters.Count > 0 && rng.NextDouble() < 0.8
                ? clusterCenters[rng.Next(clusterCenters.Count)]
                : (Vector3?)null;
            SpawnRock(rng, seabed, mat, rockGroup, center);
        }
        for (int i = 0; i < coralCount; i++)
        {
            SpawnCoral(rng, seabed, mat, coralGroup, i, clusterCenters);
        }
    }

    private void SpawnRock(System.Random rng, SeabedTerrain seabed, Material mat, Transform group, Vector3? center)
    {
        Vector3 spot;
        if (center.HasValue)
        {
            Vector2 o = RandomInCircle(rng, 2.6f);
            spot = new Vector3(center.Value.x + o.x, 0f, center.Value.z + o.y);
            if (seabed != null)
            {
                spot.y = seabed.GetWorldSeabedHeight(spot.x, spot.z);
            }
        }
        else
        {
            spot = PickSpot(rng, seabed, 0.9f);
        }
        GameObject go = NewChild(group, $"礁石_{_rockSpots.Count}");
        MeshFilter mf = go.AddComponent<MeshFilter>();
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mf.sharedMesh = BuildRockMesh(rng);
        mr.sharedMaterial = mat;

        float s = 1.2f + (float)rng.NextDouble() * 2f;   // 更大的礁石块
        go.transform.localScale = new Vector3(s * (0.8f + (float)rng.NextDouble() * 0.6f),
                                              s * (0.5f + (float)rng.NextDouble() * 0.4f),
                                              s * (0.8f + (float)rng.NextDouble() * 0.6f));
        go.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
        go.transform.position = spot + Vector3.down * (0.15f * s);
        _rockSpots.Add(go.transform.position);
        Bounds rb = mf.sharedMesh.bounds;   // 礁石：单球障碍，半径按网格实际尺寸算
        Obstacles.Add(new Obstacle { a = go.transform.position, b = go.transform.position, radius = Mathf.Max(rb.extents.x, rb.extents.z) * Mathf.Max(go.transform.localScale.x, go.transform.localScale.z) });
    }

    private void SpawnCoral(System.Random rng, SeabedTerrain seabed, Material mat, Transform group, int index, List<Vector3> gardens)
    {
        Vector3 spot = Vector3.zero;
        bool placed = false;
        // 珊瑚成片：八成密集种在花园内（半径 2.4m、最小间距 0.85m），两成零星散布点缀过渡带
        for (int attempt = 0; attempt < 9 && !placed; attempt++)
        {
            if (gardens.Count > 0 && rng.NextDouble() < 0.8)
            {
                Vector3 garden = gardens[rng.Next(gardens.Count)];
                Vector2 o = RandomInCircle(rng, 2.4f);
                spot = new Vector3(garden.x + o.x, 0f, garden.z + o.y);
                if (seabed != null)
                {
                    spot.y = seabed.GetWorldSeabedHeight(spot.x, spot.z);
                }
            }
            else
            {
                spot = PickSpot(rng, seabed, 1f);
            }
            placed = attempt >= 8 || _coralSpots.TrueForAll(p => (p - spot).sqrMagnitude > 0.72f);  // 最小间距 0.85m，花园内挤成一丛
        }
        _coralSpots.Add(spot);

        GameObject go = NewChild(group, $"珊瑚_{index}");
        MeshFilter mf = go.AddComponent<MeshFilter>();
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        List<Obstacle> bones = new List<Obstacle>();
        mf.sharedMesh = BuildCoralMesh(rng, bones);
        mr.sharedMaterial = mat;

        float s = 0.55f + (float)rng.NextDouble() * 0.55f;   // 矮丛：整体更小更矮
        go.transform.localScale = new Vector3(s, s, s);
        go.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
        go.transform.position = spot;
        // 每根枝条（或团块球）注册为独立胶囊障碍：鱼可钻进丛内，只避开实际枝条
        for (int k = 0; k < bones.Count; k++)
        {
            Obstacles.Add(new Obstacle
            {
                a = go.transform.TransformPoint(bones[k].a),
                b = go.transform.TransformPoint(bones[k].b),
                radius = bones[k].radius * s,
            });
        }
    }

    // ---------- 网格构建 ----------

    private static Mesh BuildRockMesh(System.Random rng)
    {
        Mesh sphere = PrimitiveMeshes.UnitSphere;
        Vector3[] src = sphere.vertices;
        Vector3[] verts = new Vector3[src.Length];
        Color[] colors = new Color[src.Length];
        float f1 = 2.2f + (float)rng.NextDouble() * 1.6f;
        float f2 = 5.5f;
        float off = (float)rng.NextDouble() * 100f;

        for (int i = 0; i < src.Length; i++)
        {
            Vector3 dir = src[i].normalized;
            float n = VNoise3(dir * f1 + Vector3.one * off) * 0.7f + VNoise3(dir * f2 + Vector3.one * off * 1.7f) * 0.3f;
            float r = 0.5f * (0.72f + 0.55f * n);
            verts[i] = dir * r;
            float g = 0.2f + 0.26f * n;                              // 深灰褐
            Color c = new Color(g * 1.08f, g * 0.95f, g * 0.78f);
            float algae = Mathf.Clamp01(-verts[i].y * 3f);           // 底部染藻绿
            colors[i] = Color.Lerp(c, new Color(0.16f, 0.28f, 0.16f), algae * 0.7f);
        }

        Mesh m = new Mesh { name = "Rock" };
        m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.vertices = verts;
        m.colors = colors;
        m.uv = sphere.uv;
        m.triangles = sphere.triangles;
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    private static Mesh BuildCoralMesh(System.Random rng, List<Obstacle> bones)
    {
        if (rng.NextDouble() < 0.55)
        {
            return BuildBranchingCoral(rng, bones);   // 分支珊瑚：每根枝条一条胶囊骨骼
        }
        Mesh m = BuildBoulderCoral(rng);
        Bounds b = m.bounds;                          // 团块珊瑚：单球障碍
        bones.Add(new Obstacle { a = b.center, b = b.center, radius = Mathf.Max(b.extents.x, b.extents.z) });
        return m;
    }

    /// <summary>分支珊瑚：矮丛状手指珊瑚——4~6 根主干、短节、宽角度展开，形成低矮茂密的灌木丛。</summary>
    private static Mesh BuildBranchingCoral(System.Random rng, List<Obstacle> bones)
    {
        List<Vector3> verts = new List<Vector3>();
        List<Color> cols = new List<Color>();
        List<int> tris = new List<int>();

        Color baseColor = CoralPalette[rng.Next(CoralPalette.Length)];
        int trunks = 4 + rng.Next(3);
        for (int i = 0; i < trunks; i++)
        {
            // 主干向四周张开（60°~120°锥角内），高度低
            Vector3 d = UpJitter(rng, Vector3.up, 55f);
            Branch(rng, verts, cols, tris, Vector3.zero, d, 0.3f, 0.055f, 0, baseColor, bones);
        }

        Mesh m = new Mesh { name = "CoralBranch" };
        m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.vertices = verts.ToArray();
        m.colors = cols.ToArray();
        m.triangles = tris.ToArray();
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    /// <summary>团块珊瑚：噪声变形球，体表深色斑点纹（脑纹/圆球珊瑚）。</summary>
    private static Mesh BuildBoulderCoral(System.Random rng)
    {
        Mesh sphere = PrimitiveMeshes.UnitSphere;
        Vector3[] src = sphere.vertices;
        Vector3[] verts = new Vector3[src.Length];
        Color[] colors = new Color[src.Length];
        float off = (float)rng.NextDouble() * 100f;
        Color baseColor = CoralPalette[rng.Next(CoralPalette.Length)];
        Color spot = baseColor * 0.45f;

        for (int i = 0; i < src.Length; i++)
        {
            Vector3 dir = src[i].normalized;
            float n = VNoise3(dir * 3.2f + Vector3.one * off) * 0.65f + VNoise3(dir * 8.5f + Vector3.one * off * 1.7f) * 0.35f;
            float r = 0.5f * (0.78f + 0.4f * n);
            verts[i] = new Vector3(dir.x * r, dir.y * r * 0.72f, dir.z * r);   // 压扁圆团
            float cell = VNoise3(dir * 7f + Vector3.one * off * 2.3f);
            colors[i] = cell > 0.58f ? spot : Color.Lerp(baseColor * 0.85f, Color.Lerp(baseColor, new Color(1f, 0.9f, 0.75f), 0.3f), n);
        }

        Mesh m = new Mesh { name = "CoralBoulder" };
        m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.vertices = verts;
        m.colors = colors;
        m.uv = sphere.uv;
        m.triangles = sphere.triangles;
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    private static void Branch(System.Random rng, List<Vector3> verts, List<Color> cols, List<int> tris,
                               Vector3 pos, Vector3 dir, float len, float radius, int depth, Color color, List<Obstacle> bones)
    {
        Vector3 end = pos + dir * len;
        float tipR = Mathf.Max(0.012f, radius * 0.55f);
        AddTube(verts, cols, tris, pos, end, radius, tipR, color);
        bones.Add(new Obstacle { a = pos, b = end, radius = radius });   // 管子轴线段 = 胶囊障碍骨骼

        if (depth >= 4 || radius < 0.02f)
        {
            return;
        }

        int children = 2 + (rng.NextDouble() < 0.5 ? 1 : 0);
        for (int i = 0; i < children; i++)
        {
            Vector3 d = UpJitter(rng, dir, 34f + (float)rng.NextDouble() * 30f);
            Color c = Color.Lerp(color, CoralPalette[rng.Next(CoralPalette.Length)], 0.12f);
            Branch(rng, verts, cols, tris, end, d, len * (0.72f + (float)rng.NextDouble() * 0.2f),
                   radius * 0.64f, depth + 1, c, bones);
        }
    }

    private static void AddTube(List<Vector3> verts, List<Color> cols, List<int> tris,
                                Vector3 a, Vector3 b, float r0, float r1, Color color)
    {
        const int sides = 7;
        Vector3 axis = (b - a).normalized;
        Vector3 side = Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > 0.9f ? Vector3.right : Vector3.up;
        Vector3 u = Vector3.Cross(axis, side).normalized;
        Vector3 v = Vector3.Cross(axis, u).normalized;

        int start = verts.Count;
        for (int s = 0; s <= sides; s++)
        {
            float ang = s / (float)sides * Mathf.PI * 2f;
            Vector3 ringDir = u * Mathf.Cos(ang) + v * Mathf.Sin(ang);
            verts.Add(a + ringDir * r0);
            cols.Add(color);
            verts.Add(b + ringDir * r1);
            cols.Add(Color.Lerp(color, new Color(1f, 0.95f, 0.8f), 0.35f));
        }
        for (int s = 0; s < sides; s++)
        {
            int i0 = start + s * 2;
            tris.Add(i0); tris.Add(i0 + 1); tris.Add(i0 + 2);
            tris.Add(i0 + 1); tris.Add(i0 + 3); tris.Add(i0 + 2);
        }
    }

    private static Vector3 UpJitter(System.Random rng, Vector3 dir, float degrees)
    {
        float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
        float tilt = (float)rng.NextDouble() * degrees * Mathf.Deg2Rad;
        Vector3 side = Mathf.Abs(Vector3.Dot(dir, Vector3.up)) > 0.9f ? Vector3.right : Vector3.up;
        Vector3 u = Vector3.Cross(dir, side).normalized;
        Vector3 v = Vector3.Cross(dir, u).normalized;
        return (dir * Mathf.Cos(tilt) + (u * Mathf.Cos(ang) + v * Mathf.Sin(ang)) * Mathf.Sin(tilt)).normalized;
    }

    // ---------- 工具 ----------

    private static GameObject NewChild(Transform parent, string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    /// <summary>创建全新的命名子分组。不复用已有分组：Play 模式下旧分组可能已被 Destroy 标记（帧末销毁），挂进去的新物体会被连带销毁。</summary>
    private Transform EnsureGroup(string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        return go.transform;
    }

    private void ClearChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            if (Application.isPlaying)
            {
                Destroy(child);
            }
            else
            {
                DestroyImmediate(child);
            }
        }
    }

    private Vector3 PickSpot(System.Random rng, SeabedTerrain seabed, float inset)
    {
        float half = area * 0.5f * inset;
        float x = -half + (float)rng.NextDouble() * half * 2f;
        float z = -half + (float)rng.NextDouble() * half * 2f;
        Vector3 spot = new Vector3(x, 0f, z);
        if (seabed != null)
        {
            spot.y = seabed.GetWorldSeabedHeight(x, z);
        }
        return spot;
    }

    private static Vector2 RandomInCircle(System.Random rng, float radius)
    {
        float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
        float r = radius * Mathf.Sqrt((float)rng.NextDouble());
        return new Vector2(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r);
    }

    private static Material EnsureMaterial()
    {
        Shader shader = Shader.Find("Custom/Reef");
        if (shader == null)
        {
            Debug.LogError("找不到 Custom/Reef shader");
            return null;
        }
        return new Material(shader);
    }

    private static float VNoise3(Vector3 p)
    {
        Vector3 i = new Vector3(Mathf.Floor(p.x), Mathf.Floor(p.y), Mathf.Floor(p.z));
        Vector3 f = p - i;
        f = Vector3.Scale(f, f);                                    // f^2
        f = Vector3.Scale(f, Vector3.one * 3f - f * 2f);            // f^2 * (3 - 2f)
        float a = H3(i);
        float b = H3(i + Vector3.right);
        float c = H3(i + Vector3.up);
        float d = H3(i + Vector3.right + Vector3.up);
        float e = H3(i + Vector3.forward);
        float g = H3(i + Vector3.right + Vector3.forward);
        float h = H3(i + Vector3.up + Vector3.forward);
        float k = H3(i + Vector3.one);
        return Mathf.Lerp(Mathf.Lerp(Mathf.Lerp(a, b, f.x), Mathf.Lerp(c, d, f.x), f.y),
                          Mathf.Lerp(Mathf.Lerp(e, g, f.x), Mathf.Lerp(h, k, f.x), f.y), f.z);
    }

    private static float H3(Vector3 p)
    {
        return Frac(Mathf.Sin(p.x * 127.1f + p.y * 311.7f + p.z * 74.7f) * 43758.5453f);
    }

    private static float Frac(float v)
    {
        return v - Mathf.Floor(v);
    }
}