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
        // OFFではストーリー一覧・最終ステージ等が未設定でも通常プレイを可能にする。
        if (!settings.enableStoryMode) return true;
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
        return CanEnterScene(settings, sceneName);
    }

    // UI側と遷移ボタン側で同じ進行制限を使う。
    private static bool CanEnterScene(EndingSettings settings, string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName)) return false;
        if (!settings.enableStoryMode)
            return sceneName != settings.normalEndingScene && sceneName != settings.trueEndingScene;
        if (settings.IsExtra(sceneName)) return EndingProgress.TrueSeen;

        int index = settings.storyStages.FindIndex(s => s.sceneName == sceneName);
        if (index < 0) return true; // チュートリアル、タイトル等は進行制限の対象外。
        if (index == 0) return true;

        // 既存セーブやテストでクリアしたステージは再プレイ可能。
        // 保存IDはStageSelect側の表示データでなく、StageInfoから取り込んだIDを使う。
        return StageResult.IsCleared(settings.storyStages[index].Id)
            || StageResult.IsCleared(settings.storyStages[index - 1].Id);
    }

    public static bool CanSelectStage(string sceneName, bool manuallyUnlocked)
    {
        if (!TryGetSettings(out var settings)) return false;
        if (!CanEnterScene(settings, sceneName)) return false;
        if (!settings.enableStoryMode) return true;
        // 通常ステージは自動解放を優先。旧isUnlockedを個別に変更する必要はない。
        if (settings.FindScene(sceneName) != null) return true;
        return manuallyUnlocked;
    }

    public static EndingKind RecordClear(StageInfo info, int movesUsed)
    {
        if (info == null || !TryGetSettings(out var settings)) return EndingKind.None;
        if (!settings.enableStoryMode) return EndingKind.None;
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
        if (!settings.enableStoryMode) return false; // 保留中のエンディングも再開しない。
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
