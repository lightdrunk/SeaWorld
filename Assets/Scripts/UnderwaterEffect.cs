using UnityEngine;

/// <summary>
/// 水下特效开关：相机没入浪面以下时显示全屏水下滤镜并启用雾效，出水后自动恢复。
/// 挂在 Main Camera 下的全屏 Quad（水下特效）上，自动查找「海面」的水材质读取浪高。
/// </summary>
[ExecuteAlways]
public class UnderwaterEffect : MonoBehaviour
{
    [Tooltip("水下雾浓度")]
    public float fogDensity = 0.055f;
    [Tooltip("水下雾颜色")]
    public Color fogColor = new Color(0.02f, 0.13f, 0.20f, 1f);

    private Renderer _fxRenderer;
    private Camera _camera;
    private Material _waterMat;
    private bool _submerged;
    private bool _prevFogOn;
    private Color _prevFogColor;
    private FogMode _prevFogMode;
    private float _prevFogDensity;

    private void OnDisable()
    {
        RestoreFog();
        _submerged = false;
    }

    private void LateUpdate()
    {
        if (_fxRenderer == null) _fxRenderer = GetComponent<Renderer>();
        if (_camera == null) _camera = GetComponentInParent<Camera>();
        if (_fxRenderer == null || _camera == null) return;

        Material waterMat = FindWaterMaterial();
        if (waterMat == null || !waterMat.HasProperty("_WaveScale")) return;

        Transform camT = _camera.transform;
        float waterH = WaterMath.WaveHeight(waterMat, new Vector2(camT.position.x, camT.position.z), Time.time);
        bool under = camT.position.y < waterH;

        // 每帧同步滤镜显隐（初始状态也对齐）
        _fxRenderer.enabled = under;

        if (under != _submerged)
        {
            _submerged = under;
            if (under) ApplyFog(); else RestoreFog();
        }
    }

    private Material FindWaterMaterial()
    {
        if (_waterMat != null && _waterMat.HasProperty("_WaveScale")) return _waterMat;

        GameObject sea = GameObject.Find("海面");
        if (sea != null && sea.TryGetComponent(out Renderer r) && r.sharedMaterial != null)
        {
            _waterMat = r.sharedMaterial;
        }
        return _waterMat;
    }

    private void ApplyFog()
    {
        _prevFogOn = RenderSettings.fog;
        _prevFogColor = RenderSettings.fogColor;
        _prevFogMode = RenderSettings.fogMode;
        _prevFogDensity = RenderSettings.fogDensity;
        RenderSettings.fog = true;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = fogDensity;
    }

    private void RestoreFog()
    {
        RenderSettings.fog = _prevFogOn;
        RenderSettings.fogColor = _prevFogColor;
        RenderSettings.fogMode = _prevFogMode;
        RenderSettings.fogDensity = _prevFogDensity;
    }
}