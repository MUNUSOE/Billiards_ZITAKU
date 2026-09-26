using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class TitleManager : MonoBehaviour
{
    [Header("Scene Names")]
    [Tooltip("チュートリアルの最初のシーン名")]
    [SerializeField] private string tutorialSceneName = "TutorialA";

    [Tooltip("ステージ選択のシーン名")]
    [SerializeField] private string stageSelectSceneName = "StageSelect";

    [Header("Debug")]
    [Tooltip("チェックを入れると、完了済みでも毎回チュートリアルへ進みます。デバッグ用。")]
    [SerializeField] private bool alwaysShowTutorial = false;

    [Header("Reset UI")]
    [Tooltip("「本当にリセットしますか？」の確認ウィンドウパネル")]
    [SerializeField] private GameObject resetConfirmWindow;

    [Tooltip("「リセットしました」の完了ウィンドウパネル")]
    [SerializeField] private GameObject resetCompleteWindow;

    private void Start()
    {
        // 起動時はウィンドウを非表示にしておく
        if (resetConfirmWindow != null) resetConfirmWindow.SetActive(false);
        if (resetCompleteWindow != null) resetCompleteWindow.SetActive(false);
    }

    private void Update()
    {
        // Keyboard.current を使用して Enter キー（またはテンキーの Enter）の押下を判定
        if (Keyboard.current != null)
        {
            // 確認・完了ウィンドウが開いている間は、Enterでのスタート判定を無効にする
            bool isWindowOpen = (resetConfirmWindow != null && resetConfirmWindow.activeSelf) ||
                                (resetCompleteWindow != null && resetCompleteWindow.activeSelf);

            if (!isWindowOpen && (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame))
            {
                OnClickStartButton();
            }
        }
    }

    /// <summary>
    /// ゲームスタートボタンを押した時の処理
    /// </summary>
    public void OnClickStartButton()
    {
        if (EndingFlow.TryResumePendingEnding()) return;
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySE(SEType.DecideButton);
        }

        // 念のためゲームの時間を通常速度に戻す
        Time.timeScale = 1f;

        // チュートリアル未完了（またはデバッグON）ならチュートリアルへ、それ以外はステージ選択へ
        if (alwaysShowTutorial || !TutorialProgress.IsCompleted)
        {
            SceneManager.LoadScene(tutorialSceneName);
        }
        else
        {
            SceneManager.LoadScene(stageSelectSceneName);
        }
    }

    /// <summary>
    /// ステージ選択へ直接進む（チュートリアルをスキップするボタン等用）
    /// </summary>
    public void GoToStageSelect()
    {
        if (EndingFlow.TryResumePendingEnding()) return;
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySE(SEType.DecideButton);
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(stageSelectSceneName);
    }

    /// <summary>
    /// ゲームをやめるボタンを押した時の処理
    /// </summary>
    public void OnClickQuitButton()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySE(SEType.DecideButton);
        }

        Debug.Log("ゲームを終了します");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ====================================================
    // ▼ リセット関連の処理 ▼
    // ====================================================

    /// <summary>
    /// タイトル画面の「リセットボタン」を押した時の処理。
    /// 確認ウィンドウを表示します。
    /// </summary>
    public void OnClickResetButton()
    {
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySE(SEType.DecideButton);

        if (resetConfirmWindow != null) resetConfirmWindow.SetActive(true);
    }

    /// <summary>
    /// 確認ウィンドウで「キャンセル（いいえ）」を押した時の処理。
    /// </summary>
    public void CancelReset()
    {
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySE(SEType.DecideButton);

        if (resetConfirmWindow != null) resetConfirmWindow.SetActive(false);
    }

    /// <summary>
    /// 確認ウィンドウで「実行（はい）」を押した時の処理。
    /// 全てのプレイ記録をリセットし、完了ウィンドウを表示します。
    /// </summary>
    public void ExecuteReset()
    {
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySE(SEType.DecideButton);

        // PlayerPrefs に保存されている全てのセーブデータを消去します
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();

        Debug.Log("【完了】全てのプレイ履歴をリセットしました！");

        if (resetConfirmWindow != null) resetConfirmWindow.SetActive(false);
        if (resetCompleteWindow != null) resetCompleteWindow.SetActive(true);
    }

    /// <summary>
    /// 完了ウィンドウで「OK」を押した時の処理。
    /// 古い状態が残らないよう、タイトル画面を再読み込みします。
    /// </summary>
    public void CloseCompleteWindow()
    {
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySE(SEType.DecideButton);

        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}