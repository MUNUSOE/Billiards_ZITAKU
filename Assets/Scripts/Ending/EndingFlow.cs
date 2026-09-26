using UnityEngine;
using UnityEngine.SceneManagement;

public static class EndingFlow
{
    // アセット名/配置を固定し、ステージ選択を経由せず開始しても同じ設定を参照する。
    public static EndingSettings Settings => Resources.Load<EndingSettings>("EndingSettings");

    public static bool TryGetSettings(out EndingSettings settings)
    {
        settings = Settings;
        if (settings == null)
        {
            Debug.LogError("[Ending] Assets/Resources/EndingSettings.asset を作成してください。");
            return false;
        }
        if (!settings.Validate(out string error))
        {
            Debug.LogError("[Ending] " + error);
            return false;
        }
        return true;
    }

    public static bool CanEnterScene(string sceneName)
    {
        if (!TryGetSettings(out var settings)) return false;
        return !settings.IsExtra(sceneName) || EndingProgress.TrueSeen;
    }

    public static EndingKind RecordClear(StageInfo info, int movesUsed)
    {
        if (info == null || !TryGetSettings(out var settings)) return EndingKind.None;
        var entry = settings.FindScene(info.gameObject.scene.name);
        if (entry == null) return EndingKind.None; // チュートリアル・エクストラは対象外。
        if (entry.Id != info.StageId || entry.parMoves != info.ParMoves)
        {
            Debug.LogError("[Ending] EndingSettings と StageInfo のID/ParMovesが不一致です: " + entry.sceneName);
            return EndingKind.None;
        }
        EndingProgress.RegisterClear(entry.Id, movesUsed, entry.parMoves);
        var ending = EndingProgress.Evaluate(settings, entry.Id, movesUsed);
        EndingProgress.Queue(ending);
        return ending;
    }

    public static bool TryResumePendingEnding()
    {
        EndingKind kind = EndingProgress.PendingEnding;
        if (kind != EndingKind.Normal && kind != EndingKind.True) return false;
        if (!TryGetSettings(out var settings)) return false;
        string scene = settings.SceneFor(kind);
        if (!Application.CanStreamedLevelBeLoaded(scene))
        {
            Debug.LogError("[Ending] Build Profileにエンディングを登録してください: " + scene);
            return false; // Pending は維持。修正後に再開できる。
        }
        Time.timeScale = 1f;
        SceneManager.LoadScene(scene);
        return true;
    }
}
