using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 程序化海草丛与海葵：合并网格 + 顶点色，摆动由 Custom/Plant shader 按 uv.y 加权（根部不动，尖端大摆）。
/// </summary>
[ExecuteAlways]
public class PlantSpawner : MonoBehaviour
{
    public int weedClusters = 30;
    public int anemoneCount = 5;
    public float area = 55f;
    public int seed = 555;

    private static readonly Color[] AnemonePalette =
    {
        new Color(0.75f, 0.30f, 0.50f),  // 粉
        new Color(0.50f, 0.30f, 0.80f),  // 紫
        new Color(0.30f, 0.60f, 0.35f),  // 绿
    };

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
        // 先确认 shader 可用，再清空子物体，避免缺 shader 时清空后无法重建
        Shader shader = Shader.Find("Custom/Plant");
        if (shader == null)
        {
            Debug.LogError("找不到 Custom/Plant shader");
            return;
        }
        Material mat = new Material(shader);
        mat.SetFloat("_SwayAmp", 0.16f);     // 高挑海草用更明显的摆动
        mat.SetFloat("_SwaySpeed", 0.9f);    // 缓慢水流感

        ClearChildren();

        System.Random rng = new System.Random(seed);
        SeabedTerrain seabed = FindFirstObjectByType<SeabedTerrain>();

        Transform weedGroup = EnsureGroup("海草");
        Transform anemoneGroup = EnsureGroup("海葵");

        for (int i = 0; i < weedClusters; i++)
        {
            int index = i;
            Spawn(go =>
            {
                MeshFilter mf = go.AddComponent<MeshFilter>();
                go.AddComponent<MeshRenderer>().sharedMaterial = mat;
                List<ReefSpawner.Obstacle> bones = new List<ReefSpawner.Obstacle>();
                mf.sharedMesh = BuildWeedClusterMesh(rng, bones);
                go.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                return bones;
            }, weedGroup, "海草", index, rng, seabed);
        }

        for (int i = 0; i < anemoneCount; i++)
        {
            int index = i;
            Spawn(go =>
            {
                MeshFilter mf = go.AddComponent<MeshFilter>();
                go.AddComponent<MeshRenderer>().sharedMaterial = mat;
                mf.sharedMesh = BuildAnemoneMesh(rng);
                go.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                return null;   // 海葵是小丑鱼宿主，不注册障碍
            }, anemoneGroup, "海葵", index, rng, seabed);
        }
    }

    private void Spawn(Func<GameObject, List<ReefSpawner.Obstacle>> build, Transform group, string prefix, int index, System.Random rng, SeabedTerrain seabed)
    {
        GameObject go = new GameObject($"{prefix}_{index}");
        go.transform.SetParent(group, false);
        List<ReefSpawner.Obstacle> bones = build(go);
        Vector3 spot = PickSpot(rng, seabed);
        go.transform.position = spot;
        if (bones != null)
        {
            for (int k = 0; k < bones.Count; k++)   // 每片叶的胶囊从局部坐标转世界坐标注册
            {
                ReefSpawner.Obstacle o = bones[k];
                ReefSpawner.Obstacles.Add(new ReefSpawner.Obstacle
                {
                    a = go.transform.TransformPoint(o.a),
                    b = go.transform.TransformPoint(o.b),
                    radius = o.radius,
                });
            }
        }
    }

    /// <summary>创建全新的命名子分组。不复用已有分组：Play 模式下旧分组可能已被 Destroy 标记（帧末销毁），挂进去的新物体会被连带销毁。</summary>
    private Transform EnsureGroup(string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        return go.transform;
    }

    // ---------- 网格构建 ----------

    /// <summary>水草丛：高挑细长带状叶片（参考海藻林），带 S 形静态弯曲，uv.y = 高度分数（供 shader 摆动）。</summary>
    private static Mesh BuildWeedClusterMesh(System.Random rng, List<ReefSpawner.Obstacle> bones)
    {
        List<Vector3> verts = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<Color> cols = new List<Color>();
        List<int> tris = new List<int>();

        Color root = new Color(0.035f, 0.14f, 0.06f);   // 深绿根部
        Color mid = new Color(0.10f, 0.32f, 0.12f);
        Color tip = new Color(0.40f, 0.62f, 0.24f);     // 黄绿叶尖
        int blades = 9 + rng.Next(7);      // 9~15 片，茂密成丛
        const int seg = 7;

        for (int b = 0; b < blades; b++)
        {
            Vector2 o = RandomInCircle(rng, 0.8f);
            float yaw = (float)rng.NextDouble() * Mathf.PI * 2f;
            float height = 1.2f + (float)rng.NextDouble() * 2.2f;   // 1.2~3.4m 高挑海藻
            float width = 0.06f + (float)rng.NextDouble() * 0.05f;  // 细长带状
            float bendDir = (float)rng.NextDouble() * Mathf.PI * 2f;
            float bendFreq = 1.6f + (float)rng.NextDouble() * 1.2f;
            float bendAmp = 0.1f + (float)rng.NextDouble() * 0.25f; // 静态 S 弯
            Vector3 dir = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw));
            Vector3 side = new Vector3(-dir.z, 0f, dir.x);
            Vector3 bend = new Vector3(Mathf.Cos(bendDir), 0f, Mathf.Sin(bendDir));

            int start = verts.Count;
            for (int s = 0; s <= seg; s++)
            {
                float yF = (float)s / seg;
                float w = width * Mathf.Sin(Mathf.Clamp01(yF * 1.25f) * Mathf.PI * 0.5f) * (1f - yF * 0.55f) * 0.5f; // 根窄-中宽-尖细
                Vector3 p = new Vector3(o.x, yF * height, o.y) + bend * (Mathf.Sin(yF * bendFreq * Mathf.PI) * bendAmp * yF);
                verts.Add(p - side * w);
                uvs.Add(new Vector2(0f, yF));
                verts.Add(p + side * w);
                uvs.Add(new Vector2(1f, yF));
                Color c = yF < 0.5f ? Color.Lerp(root, mid, yF * 2f) : Color.Lerp(mid, tip, (yF - 0.5f) * 2f);
                cols.Add(c);
                cols.Add(c);
            }
            for (int s = 0; s < seg; s++)
            {
                int i0 = start + s * 2;
                tris.Add(i0); tris.Add(i0 + 1); tris.Add(i0 + 2);
                tris.Add(i0 + 1); tris.Add(i0 + 3); tris.Add(i0 + 2);
            }

            // 每片叶的根→梢轴线注册为胶囊障碍（局部坐标，由调用处转世界）：鱼能钻进丛内，只避开实际叶片
            bones.Add(new ReefSpawner.Obstacle
            {
                a = new Vector3(o.x, 0.05f, o.y),
                b = new Vector3(o.x, height, o.y) + bend * (Mathf.Sin(bendFreq * Mathf.PI) * bendAmp),
                radius = 0.09f,
            });
        }

        return FinishMesh(verts, uvs, cols, tris, "Seaweed");
    }

    /// <summary>海葵：压扁半球底盘 + 放射状细触手，底盘 uv.y=0 不摆动。</summary>
    private static Mesh BuildAnemoneMesh(System.Random rng)
    {
        List<Vector3> verts = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<Color> cols = new List<Color>();
        List<int> tris = new List<int>();

        Color body = AnemonePalette[rng.Next(AnemonePalette.Length)];
        Color tip = Color.Lerp(body, new Color(1f, 0.9f, 0.85f), 0.5f);

        // 底盘：压扁球体（uv.y=0 → 不参与摆动）
        Mesh sphere = PrimitiveMeshes.UnitSphere;
        Vector3[] sv = sphere.vertices;
        int bodyStart = verts.Count;
        for (int i = 0; i < sv.Length; i++)
        {
            verts.Add(new Vector3(sv[i].x * 0.34f, sv[i].y * 0.2f + 0.12f, sv[i].z * 0.34f));
            uvs.Add(Vector2.zero);
            cols.Add(body);
        }
        foreach (int t in sphere.triangles)
        {
            tris.Add(t + bodyStart);
        }

        // 触手：放射状细管
        int tentacles = 40 + rng.Next(20);
        for (int i = 0; i < tentacles; i++)
        {
            Vector2 o = RandomInCircle(rng, 0.26f);
            Vector3 a = new Vector3(o.x, 0.2f, o.y);
            Vector3 d = UpJitter(rng, Vector3.up, 28f);
            float len = 0.3f + (float)rng.NextDouble() * 0.3f;
            AddTube(verts, uvs, cols, tris, a, a + d * len, 0.028f, 0.008f, body, tip);
        }

        return FinishMesh(verts, uvs, cols, tris, "Anemone");
    }

    private static void AddTube(List<Vector3> verts, List<Vector2> uvs, List<Color> cols, List<int> tris,
                                Vector3 a, Vector3 b, float r0, float r1, Color baseColor, Color tipColor)
    {
        const int sides = 6;
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
            uvs.Add(new Vector2(ang / (Mathf.PI * 2f), 0f));
            cols.Add(baseColor);
            verts.Add(b + ringDir * r1);
            uvs.Add(new Vector2(ang / (Mathf.PI * 2f), 1f));
            cols.Add(tipColor);
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

    private static Mesh FinishMesh(List<Vector3> verts, List<Vector2> uvs, List<Color> cols, List<int> tris, string name)
    {
        Mesh m = new Mesh { name = name };
        m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.vertices = verts.ToArray();
        m.uv = uvs.ToArray();
        m.colors = cols.ToArray();
        m.triangles = tris.ToArray();
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    // ---------- 工具 ----------

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

    private Vector3 PickSpot(System.Random rng, SeabedTerrain seabed)
    {
        float half = area * 0.5f;
        float x = -half + (float)rng.NextDouble() * area;
        float z = -half + (float)rng.NextDouble() * area;
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
}