using UnityEngine;

/// <summary>
/// 双通道音频播放器（自 AutomobileLifting 项目移植，适配海底场景）：
/// - 索引 0：开机自动循环播放的水下背景音（专用循环源，永不中断）
/// - 索引 1 起：外部触发的一次性音效（PlayByIndex / PlayRandom / UnityEvent 绑定），与背景音互不打断
/// 建议挂在主相机上（自带 AudioListener）：背景音始终 2D 全局可听，音效空间化可调。
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class UnderwaterAudio : MonoBehaviour
{
    [Header("音频列表")]
    [Tooltip("音频资源数组。索引 0 为循环播放的背景音（自动播放），索引 1 起为外部触发的一次性音效")]
    public AudioClip[] audioClips;

    [Header("播放设置")]
    [Tooltip("音量")]
    [Range(0f, 1f)]
    public float volume = 1f;

    [Tooltip("音效播放中收到新播放指令是否立即切换（false 则忽略新指令）")]
    public bool interruptCurrent = true;

    [Tooltip("音效空间化混合（0=2D 全局，1=3D 随距离衰减）。背景音始终 2D")]
    [Range(0f, 1f)]
    public float sfxSpatialBlend = 0f;

    [Header("调试")]
    public bool enableDebugLog = false;

    private AudioSource _loopSource;    // 索引0：循环背景音
    private AudioSource _oneshotSource; // 索引1起：触发性音效

    /// <summary>当前正在播放的音效</summary>
    public AudioClip CurrentClip => _oneshotSource != null ? _oneshotSource.clip : null;

    /// <summary>音效是否正在播放</summary>
    public bool IsPlaying => _oneshotSource != null && _oneshotSource.isPlaying;

    private void Awake()
    {
        // 复用 RequireComponent 自动添加的 AudioSource 作为音效源
        _oneshotSource = GetComponent<AudioSource>();
        _oneshotSource.playOnAwake = false;
        _oneshotSource.loop = false;
        _oneshotSource.volume = volume;
        _oneshotSource.spatialBlend = sfxSpatialBlend;

        // 额外创建一个专用循环源，保证背景音不被音效打断
        _loopSource = gameObject.AddComponent<AudioSource>();
        _loopSource.playOnAwake = false;
        _loopSource.loop = true;
        _loopSource.volume = volume;
        _loopSource.spatialBlend = 0f;   // 背景音 2D 全局
    }

    private void Start()
    {
        PlayLoop();
    }

    /// <summary>
    /// 播放索引 0 的循环背景音（已在播则忽略，保证一直循环）
    /// </summary>
    public void PlayLoop()
    {
        if (audioClips == null || audioClips.Length == 0 || audioClips[0] == null)
        {
            Log("audioClips[0] 为空，跳过背景音（拖入音频资源后自动生效）");
            return;
        }

        if (_loopSource.clip == audioClips[0] && _loopSource.isPlaying) return;

        _loopSource.clip = audioClips[0];
        _loopSource.volume = volume;
        _loopSource.Play();

        Log($"循环背景音开始播放: {audioClips[0].name}");
    }

    /// <summary>
    /// 按索引播放音频：索引 0 等价于 PlayLoop()，索引 1 起播放一次性音效（UnityEvent 可直接绑定并填索引）
    /// </summary>
    public void PlayByIndex(int index)
    {
        if (audioClips == null || audioClips.Length == 0)
        {
            Debug.LogWarning("[UnderwaterAudio] 音频数组为空，无法播放", this);
            return;
        }

        if (index < 0 || index >= audioClips.Length)
        {
            Debug.LogWarning($"[UnderwaterAudio] 索引 {index} 超出范围（有效范围 0~{audioClips.Length - 1}）", this);
            return;
        }

        if (index == 0)
        {
            PlayLoop();
            return;
        }

        PlayClip(audioClips[index]);
    }

    /// <summary>
    /// 播放指定的一次性音效（不影响循环背景音）
    /// </summary>
    public void PlayClip(AudioClip clip)
    {
        if (clip == null)
        {
            Debug.LogWarning("[UnderwaterAudio] 传入的 AudioClip 为空", this);
            return;
        }

        if (!interruptCurrent && _oneshotSource.isPlaying)
        {
            Log($"正在播放 {_oneshotSource.clip.name}，忽略新播放请求: {clip.name}");
            return;
        }

        _oneshotSource.Stop();
        _oneshotSource.clip = clip;
        _oneshotSource.volume = volume;
        _oneshotSource.spatialBlend = sfxSpatialBlend;
        _oneshotSource.Play();

        Log($"播放音效: {clip.name} (时长 {clip.length:F1} 秒)");
    }

    /// <summary>
    /// 随机播放一个音效（索引 1 起，不会动背景音）
    /// </summary>
    public void PlayRandom()
    {
        if (audioClips == null || audioClips.Length < 2)
        {
            LogWarningOnce("没有可触发的音效（索引 1 起才有）");
            return;
        }

        PlayByIndex(Random.Range(1, audioClips.Length));
    }

    /// <summary>
    /// 停止当前音效（循环背景音不受影响）
    /// </summary>
    public void Stop()
    {
        if (_oneshotSource != null && _oneshotSource.isPlaying)
        {
            _oneshotSource.Stop();
            Log("停止音效");
        }
    }

    private bool _warnedEmpty;
    private void LogWarningOnce(string message)
    {
        if (_warnedEmpty) return;
        _warnedEmpty = true;
        Debug.LogWarning($"[UnderwaterAudio] {message}", this);
    }

    private void Log(string message)
    {
        if (enableDebugLog) Debug.Log($"[UnderwaterAudio] {message}", this);
    }
}
