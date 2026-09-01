using UnityEngine;
using UnityEngine.SceneManagement; // シーン遷移に必須

public class TitleManager : MonoBehaviour
{
    [Header("遷移先のシーン名")]
    [SerializeField] private string gameSceneName = "GameScene"; // 実際に遊ぶメインのシーン名

    /// <summary>
    /// ゲームスタートボタンを押した時の処理
    /// </summary>
    public void OnClickStartButton()
    {
        // SE（効果音）を鳴らす場合
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySE(SEType.DecideButton);
        }

        // 指定したシーンへ遷移
        SceneManager.LoadScene(gameSceneName);
    }

    /// <summary>
    /// ゲームをやめるボタンを押した時の処理
    /// </summary>
    public void OnClickQuitButton()
    {
        // SE（効果音）を鳴らす場合
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySE(SEType.DecideButton);
        }

        Debug.Log("ゲームを終了します"); // エディタ上での確認用ログ

#if UNITY_EDITOR
        // Unityエディタ上で実行中の場合は、プレイモードを停止する
        UnityEditor.EditorApplication.isPlaying = false;
#else
        // ビルドされたゲームアプリ（PC/スマホ等）の場合は、アプリを終了する
        Application.Quit();
#endif
    }
}