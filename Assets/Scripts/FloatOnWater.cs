using UnityEngine;

/// <summary>
/// 让物体漂浮在海面上：与 Water.shader 用同一套波浪函数（见 WaterMath），
/// 逐帧采样波高使物体随浪起伏、倾斜；Play 模式下可用 WASD/方向键驾驶。
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Renderer))]
public class FloatOnWater : MonoBehaviour
{
    [Header("漂浮")]
    [Tooltip("编辑模式下也预览漂浮效果")]
    public bool previewInEditMode = true;
    [Tooltip("物体底部相对浪面的抬升（物体高度的一半）")]
    public float floatOffset = 0.5f;
    [Tooltip("随浪倾斜的强度")]
    public float tiltStrength = 1.0f;

    [Header("移动（仅 Play 模式）")]
    public bool enableMove = true;
    public float moveSpeed = 12f;
    public float turnSpeed = 70f;

    private Renderer _cachedRenderer;

    private void LateUpdate()
    {
        if (!Application.isPlaying && !previewInEditMode) return;

        Renderer rend = GetRenderer();
        if (rend == null) return;
        Material mat = rend.sharedMaterial;
        if (mat == null || !mat.HasProperty("_WaveScale")) return;

        // 先水平移动（仅 Play 模式），再按新位置采样波高
        if (Application.isPlaying && enableMove)
        {
            float inputV = Input.GetAxis("Vertical");
            float inputH = Input.GetAxis("Horizontal");
            if (Mathf.Abs(inputV) > 0.001f)
            {
                Vector3 moveDir = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
                transform.position += moveDir * (inputV * moveSpeed * Time.deltaTime);
            }
            if (Mathf.Abs(inputH) > 0.001f)
            {
                transform.Rotate(0f, inputH * turnSpeed * Time.deltaTime, 0f, Space.World);
            }
        }

        Vector2 p = new Vector2(transform.position.x, transform.position.z);
        Vector3 waveNormal = WaterMath.WaveNormal(mat, p, Time.time);

        Vector3 pos = transform.position;
        pos.y = WaterMath.WaveHeight(mat, p, Time.time) + floatOffset;
        transform.position = pos;

        // 随浪倾斜
        Vector3 newUp = Vector3.Slerp(transform.up, waveNormal, Mathf.Clamp01(tiltStrength * 0.1f));
        Vector3 tiltFwd = Vector3.ProjectOnPlane(transform.forward, newUp).normalized;
        if (tiltFwd.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(tiltFwd, newUp);
        }
    }

    private Renderer GetRenderer()
    {
        return _cachedRenderer != null ? _cachedRenderer : (_cachedRenderer = GetComponent<Renderer>());
    }
}