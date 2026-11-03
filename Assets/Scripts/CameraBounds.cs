using UnityEngine;

/// <summary>
/// 相机活动范围限制：Y 方向夹在海床上方与最大高度之间（仅 Play 模式运行时生效）。
/// </summary>
public class CameraBounds : MonoBehaviour
{
    [Tooltip("海床引用；留空则自动查找场景中的 SeabedTerrain")]
    public SeabedTerrain seabed;

    [Tooltip("相机与海床的最小间隔（米），防止穿进沙地")]
    public float seabedClearance = 0.8f;

    [Tooltip("最低世界高度（米），硬下限")]
    public float minY = -15f;

    [Tooltip("允许的最大世界高度（米）；设为 0 可严格限制在海面以下")]
    public float maxY = 20f;

    private void LateUpdate()
    {
        if (seabed == null)
        {
            seabed = FindFirstObjectByType<SeabedTerrain>();
            if (seabed == null)
            {
                return;
            }
        }

        Vector3 p = transform.position;
        // 硬下限与"海床+间隙"取较高者：沟里最低到 minY，沙丘上方自动抬高防穿沙
        float floor = Mathf.Max(minY, seabed.GetWorldSeabedHeight(p.x, p.z) + seabedClearance);
        p.y = Mathf.Clamp(p.y, floor, maxY);
        transform.position = p;
    }
}