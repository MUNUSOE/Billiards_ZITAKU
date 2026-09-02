using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum BGMType
{
    MainTheme,
    Stage,
    Clear,
    GameOver,
}

public enum SEType
{
    DecideButton,
    BallHit,
    Pocket,
    WeakArrow,
    StrongArrow,
    FrameMagic,
    WaterMagic,
    WindMagic,
    FrameTile,
    GetPotion,
    FrameBallHit,
    WallHit,
    UseFrame,
    UseWater,
    UseWind,
    FireExtinguishing,
    DestroyBox,
}

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [System.Serializable]
    public struct BGMData
    {
        public BGMType type;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume;
    }

    [System.Serializable]
    public struct SEData
    {
        public SEType type;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume;
    }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource seSource;
    [SerializeField] private AudioSource loopSeSource;

    [Header("Audio Data")]
    [SerializeField] private List<BGMData> bgmList = new List<BGMData>();
    [SerializeField] private List<SEData> seList = new List<SEData>();

    [Header("Sound Settings (0.0 - 1.0)")]
    [Range(0f, 1f)][SerializeField] private float userBGMVolume = 1.0f;
    [Range(0f, 1f)][SerializeField] private float userSEVolume = 1.0f;

    [Header("Master Volume Boost")]
    [Tooltip("全体音量を一律で持ち上げる倍率。通常は1.0")]
    [Range(1f, 3f)][SerializeField] private float masterVolumeBoost = 1.0f;

    private Dictionary<BGMType, BGMData> bgmDict = new Dictionary<BGMType, BGMData>();
    private Dictionary<SEType, SEData> seDict = new Dictionary<SEType, SEData>();

    private float currentBGMIndividualVolume = 1.0f;

    public float UserBGMVolume => userBGMVolume;
    public float UserSEVolume => userSEVolume;
    public float BGMVolume => userBGMVolume;
    public float SEVolume => userSEVolume;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitDictionaries();
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    private void OnEnable()
    {
        // シーン読み込みイベントに登録
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        // イベント解除
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        // ★ 最初（起動時）のシーン用にBGM判定・再生を手動で実行
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private void InitDictionaries()
    {
        bgmDict.Clear();
        foreach (var data in bgmList)
        {
            if (data.clip != null && !bgmDict.ContainsKey(data.type))
            {
                bgmDict.Add(data.type, data);
            }
        }

        seDict.Clear();
        foreach (var data in seList)
        {
            if (data.clip != null && !seDict.ContainsKey(data.type))
            {
                seDict.Add(data.type, data);
            }
        }
    }

    // --- シーン切り替え時の自動BGM判定 ---

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // シーン名に応じて再生するBGMを分岐
        if (scene.name == "Title" || scene.name == "StageSelect")
        {
            PlayBGM(BGMType.MainTheme);
        }
        else
        {
            // Title, StageSelect 以外のシーン（各ステージなど）
            PlayBGM(BGMType.Stage);
        }
    }

    // --- BGM再生機能 ---

    public void PlayBGM(BGMType type, bool loop = true)
    {
        if (bgmSource == null) return;

        if (bgmDict.TryGetValue(type, out var data))
        {
            // 既に同じ曲が再生中なら演奏を止めずにそのまま維持
            if (bgmSource.clip == data.clip && bgmSource.isPlaying) return;

            bgmSource.clip = data.clip;
            bgmSource.loop = loop;
            currentBGMIndividualVolume = data.volume;

            ApplyVolumes();
            bgmSource.Play();
        }
        else
        {
            Debug.LogWarning($"[SoundManager] BGM {type} が見つからないかClipが未設定です。");
        }
    }

    public void StopBGM()
    {
        if (bgmSource != null)
        {
            bgmSource.Stop();
        }
    }

    // --- SE再生機能（単発） ---

    public void PlaySE(SEType type)
    {
        if (seSource == null) return;

        if (seDict.TryGetValue(type, out var data))
        {
            float volumeScale = data.volume * userSEVolume * masterVolumeBoost;
            seSource.PlayOneShot(data.clip, volumeScale);
        }
    }

    // --- SE再生機能（ループ専用） ---

    public void PlayLoopSE(SEType type)
    {
        if (loopSeSource == null) return;

        if (seDict.TryGetValue(type, out var data))
        {
            if (loopSeSource.clip == data.clip && loopSeSource.isPlaying) return;

            loopSeSource.clip = data.clip;
            loopSeSource.loop = true;

            float finalVolume = Mathf.Clamp01(data.volume * userSEVolume * masterVolumeBoost);
            loopSeSource.volume = finalVolume;

            loopSeSource.Play();
        }
    }

    public void StopLoopSE()
    {
        if (loopSeSource != null && loopSeSource.isPlaying)
        {
            loopSeSource.Stop();
            loopSeSource.clip = null;
        }
    }

    // --- 音量調整機能 ---

    public void SetBGMVolume(float volume)
    {
        userBGMVolume = Mathf.Clamp01(volume);
        ApplyVolumes();
    }

    public void SetSEVolume(float volume)
    {
        userSEVolume = Mathf.Clamp01(volume);
        ApplyVolumes();
    }

    private void ApplyVolumes()
    {
        if (bgmSource != null)
        {
            bgmSource.volume = Mathf.Clamp01(currentBGMIndividualVolume * userBGMVolume * masterVolumeBoost);
        }

        if (seSource != null)
        {
            seSource.volume = Mathf.Clamp01(userSEVolume * masterVolumeBoost);
        }

        if (loopSeSource != null)
        {
            loopSeSource.volume = Mathf.Clamp01(userSEVolume * masterVolumeBoost);
        }
    }
}