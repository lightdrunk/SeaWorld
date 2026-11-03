using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 气泡：从海床升起、左右摆动、到水面重置（Play 模式动画；编辑模式静态分布）。
/// </summary>
[ExecuteAlways]
public class BubbleSpawner : MonoBehaviour
{
    public int bubbleCount = 40;
    public float area = 50f;
    public int seed = 88;

    [Tooltip("气泡最高点（世界 Y）")]
    public float topY = -0.6f;

    private struct Bubble
    {
        public Transform t;
        public Vector3 basePos;
        public float speed;
        public float wobble;
        public float phase;
        public float scale;
        public float progress;
    }

    private readonly List<Bubble> _bubbles = new List<Bubble>();

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
        Shader shader = Shader.Find("Custom/Bubble");
        if (shader == null)
        {
            Debug.LogError("找不到 Custom/Bubble shader");
            return;
        }
        Material mat = new Material(shader);
        Mesh sphere = PrimitiveMeshes.UnitSphere;

        ClearChildren();
        _bubbles.Clear();

        System.Random rng = new System.Random(seed);
        SeabedTerrain seabed = FindFirstObjectByType<SeabedTerrain>();

        for (int i = 0; i < bubbleCount; i++)
        {
            GameObject go = new GameObject($"气泡_{i}");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = sphere;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;

            Vector2 o = RandomInCircle(rng, area * 0.5f);
            float baseY = seabed != null ? seabed.GetWorldSeabedHeight(o.x, o.y) : -15f;
            float scale = 0.05f + (float)rng.NextDouble() * 0.13f;
            go.transform.localScale = new Vector3(scale, scale, scale);

            Bubble b = new Bubble
            {
                t = go.transform,
                basePos = new Vector3(o.x, baseY, o.y),
                speed = 0.6f + (float)rng.NextDouble() * 0.8f,
                wobble = 0.15f + (float)rng.NextDouble() * 0.25f,
                phase = (float)rng.NextDouble() * Mathf.PI * 2f,
                scale = scale,
                progress = (float)rng.NextDouble(),
            };
            _bubbles.Add(b);
            Apply(b, 0f);
        }
    }

    private void Apply(Bubble b, float time)
    {
        float y = Mathf.Lerp(b.basePos.y, topY, b.progress);
        float sway = Mathf.Sin(time * 2f + b.phase) * b.wobble * b.progress;
        b.t.position = new Vector3(
            b.basePos.x + sway,
            y,
            b.basePos.z + Mathf.Cos(time * 1.7f + b.phase) * b.wobble * 0.6f * b.progress);
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        float dt = Time.deltaTime;
        float time = Time.time;
        for (int i = 0; i < _bubbles.Count; i++)
        {
            Bubble b = _bubbles[i];
            float span = Mathf.Max(0.5f, topY - b.basePos.y);
            b.progress += b.speed * dt / span;
            if (b.progress >= 1f)
            {
                b.progress = 0f;
            }
            _bubbles[i] = b;
            Apply(b, time);
        }
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

    private static Vector2 RandomInCircle(System.Random rng, float radius)
    {
        float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
        float r = radius * Mathf.Sqrt((float)rng.NextDouble());
        return new Vector2(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r);
    }
}