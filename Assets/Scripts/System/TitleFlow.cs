using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// タイトル画面から次のシーンへ進むときの振り分けです。
/// チュートリアル未完了なら1回だけチュートリアルへ、完了済みならステージ選択へ進みます。
///
/// タイトルの「ゲーム開始」ボタンの OnClick に StartGame() を割り当ててください。
/// </summary>
public class TitleFlow : MonoBehaviour
{
    [Header("Scene Names")]
    [Tooltip("チュートリアルの最初のシーン名。")]
    [SerializeField] private string tutorialSceneName = "TutorialA";

    [Tooltip("ステージ選択のシーン名。")]
    [SerializeField] private string stageSelectSceneName = "StageSelect";

    [Header("Debug")]
    [Tooltip("チェックを入れると、完了済みでも毎回チュートリアルへ進みます。動作確認用。")]
    [SerializeField] private bool alwaysShowTutorial = false;

    /// <summary>
    /// ゲームを開始します。チュートリアルの完了状況に応じて遷移先を決めます。
    /// </summary>
    public void StartGame()
    {
        Time.timeScale = 1f;

        if (alwaysShowTutorial || !TutorialProgress.IsCompleted)
        {
            SceneManager.LoadScene(tutorialSceneName);
            return;
        }

        SceneManager.LoadScene(stageSelectSceneName);
    }

    /// <summary>ステージ選択へ直接進みます。チュートリアルを飛ばすボタン用。</summary>
    public void GoToStageSelect()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(stageSelectSceneName);
    }

    /// <summary>
    /// チュートリアルの完了記録を消します。動作確認用のボタンなどに割り当ててください。
    /// </summary>
    public void ResetTutorialProgress()
    {
        TutorialProgress.ResetProgress();
    }
}
