using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem; // 追加

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

    private void Update()
    {
        // Keyboard.current を使用して Enter キー（またはテンキーの Enter）の押下を判定
        if (Keyboard.current != null)
        {
            if (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame)
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

    /// <summary>
    /// 全てのプレイ記録（クリア状況、自己ベスト、星、チュートリアル進捗など）をリセットします。
    /// （旧：チュートリアルの完了記録を消す処理）
    /// </summary>
    public void ResetAllPlayData()
    {
        // PlayerPrefs に保存されている全てのセーブデータを消去します
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();

        Debug.Log("【完了】全てのプレイ履歴をリセットしました！");

        // 古い状態が残らないよう、タイトル画面を再読み込みして完全に初期状態に戻します
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}