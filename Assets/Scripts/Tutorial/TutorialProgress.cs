using UnityEngine;

/// <summary>
/// チュートリアルを見たかどうかを保存します。
/// 初回プレイ時だけチュートリアルへ誘導するために使います。
/// </summary>
public static class TutorialProgress
{
    private const string CompletedKey = "Tutorial_Completed";

    /// <summary>チュートリアルを完了済みか。</summary>
    public static bool IsCompleted => PlayerPrefs.GetInt(CompletedKey, 0) == 1;

    /// <summary>チュートリアルを完了済みとして記録します。</summary>
    public static void MarkCompleted()
    {
        PlayerPrefs.SetInt(CompletedKey, 1);
        PlayerPrefs.Save();
        Debug.Log("[Tutorial] チュートリアルを完了済みとして記録しました。");
    }

    /// <summary>記録を消して、次回もう一度チュートリアルを表示させます。デバッグ用。</summary>
    public static void ResetProgress()
    {
        PlayerPrefs.DeleteKey(CompletedKey);
        PlayerPrefs.Save();
        Debug.Log("[Tutorial] チュートリアルの完了記録を削除しました。");
    }
}
