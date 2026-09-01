using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class OptionManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject optionPanel;
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider seSlider;

    [Header("Scene Names")]
    [SerializeField] private string titleSceneName = "TitleScene";

    private bool isPaused = false;

    void Start()
    {
        // 起動時はオプションパネルを非表示にしておく
        if (optionPanel != null)
        {
            optionPanel.SetActive(false);
        }
    }

    // 右上の灰色のオプションボタンを押した時
    public void OpenOption()
    {
        isPaused = true;
        optionPanel.SetActive(true);
        Time.timeScale = 0f; // ゲーム内の物理演算・時間を一時停止
        SoundManager.Instance.PlaySE(SEType.DecideButton);
    }

    // 「ゲームに戻る」ボタンを押した時
    public void ResumeGame()
    {
        isPaused = false;
        optionPanel.SetActive(false);
        Time.timeScale = 1f; // ゲームの時間を再開
        SoundManager.Instance.PlaySE(SEType.DecideButton);
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