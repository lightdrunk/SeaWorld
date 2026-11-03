using UnityEngine;

/// <summary>
/// Water.shader 波浪函数的 C# 镜像：保证漂浮、水下特效与着色器使用同一套波形。
/// </summary>
public static class WaterMath
{
    /// <summary>采样某点（世界 XZ）在 t 时刻的浪面高度。</summary>
    public static float WaveHeight(Material mat, Vector2 p, float t)
    {
        float ws = mat.GetFloat("_WaveScale");
        float speed = mat.GetFloat("_WaveSpeed");
        float amp = mat.GetFloat("_WaveHeight");

        float h = 0f;
        h += Mathf.Sin(Vector2.Dot(p, Normalize(new Vector2(1f, 0.35f))) * 0.25f * ws + t * 1.1f * speed) * 1.00f;
        h += Mathf.Sin(Vector2.Dot(p, Normalize(new Vector2(-0.6f, 1f))) * 0.45f * ws + t * 1.7f * speed) * 0.60f;
        h += Mathf.Sin(Vector2.Dot(p, Normalize(new Vector2(0.3f, -0.9f))) * 0.90f * ws + t * 2.3f * speed) * 0.40f;
        return h * amp;
    }

    /// <summary>由几何高度梯度求浪面法线（世界空间）。</summary>
    public static Vector3 WaveNormal(Material mat, Vector2 p, float t)
    {
        const float eps = 0.5f;
        float h = WaveHeight(mat, p, t);
        float hx = WaveHeight(mat, p + Vector2.right * eps, t);
        float hz = WaveHeight(mat, p + Vector2.up * eps, t);
        return new Vector3(-(hx - h) / eps, 1f, -(hz - h) / eps).normalized;
    }

    private static Vector2 Normalize(Vector2 v)
    {
        float len = Mathf.Sqrt(v.x * v.x + v.y * v.y);
        return len > 0.0001f ? new Vector2(v.x / len, v.y / len) : v;
    }
}