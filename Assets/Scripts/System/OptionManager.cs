using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class OptionManager : MonoBehaviour
{
    public static OptionManager Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    [Header("UI References")]
    [SerializeField] private GameObject optionPanel;
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider seSlider;

    [Header("Scene Names")]
    [SerializeField] private string titleSceneName = "TitleScene";

    private bool isPaused = false;

    /// <summary>オプション画面を開いているか。他のスクリプトから操作を止める判定に使えます。</summary>
    public bool IsPaused => isPaused;

    void Start()
    {
        // 起動時はオプションパネルを非表示にしておく
        if (optionPanel != null)
        {
            optionPanel.SetActive(false);
        }
    }

    private void Update()
    {
        // ESCキーで開閉する。
        // Time.timeScale = 0 でも入力の取得は影響を受けないため、閉じる操作も問題なく効く。
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.escapeKey.wasPressedThisFrame)
        {
            ToggleOption();
        }
    }

    /// <summary>オプション画面を開いていれば閉じ、閉じていれば開きます。</summary>
    public void ToggleOption()
    {
        if (isPaused) ResumeGame();
        else OpenOption();
    }

    // 右上の灰色のオプションボタンを押した時
    public void OpenOption()
    {
        isPaused = true;
        if (optionPanel != null) optionPanel.SetActive(true);
        Time.timeScale = 0f; // ゲーム内の物理演算・時間を一時停止

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySE(SEType.DecideButton);
        }
    }

    // 「ゲームに戻る」ボタンを押した時
    public void ResumeGame()
    {
        isPaused = false;
        if (optionPanel != null) optionPanel.SetActive(false);
        Time.timeScale = 1f; // ゲームの時間を再開

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySE(SEType.DecideButton);
        }
    }

    // 「リトライ」ボタンを押した時
    public void RetryGame()
    {
        Time.timeScale = 1f; // シーン再読み込み前に時間を必ず戻す
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        SoundManager.Instance.PlaySE(SEType.DecideButton);
    }

    // 「タイトルに戻る」ボタンを押した時
    public void GoToTitle()
    {
        Time.timeScale = 1f; // シーン移動前に時間を必ず戻す
        SceneManager.LoadScene(titleSceneName);
        SoundManager.Instance.PlaySE(SEType.DecideButton);
    }

    // BGM音量変更時（SliderのOn Value Changedで呼び出し）
    public void OnBgmVolumeChanged(float value)
    {
        // TODO: SoundManager等のBGM音量変更処理を記述
        // 例: AudioManager.Instance.SetBGMVolume(value);
    }

    // SE音量変更時（SliderのOn Value Changedで呼び出し）
    public void OnSeVolumeChanged(float value)
    {
        // TODO: SoundManager等のSE音量変更処理を記述
        // 例: AudioManager.Instance.SetSEVolume(value);
    }
}