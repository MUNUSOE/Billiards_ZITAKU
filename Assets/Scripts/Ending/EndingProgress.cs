using System;
using System.Text;
using UnityEngine;

// 自己ベストは既存の StageResult を使用。ルート判定に必要な履歴だけを追加保存する。
public static class EndingProgress
{
    private const string Prefix = "MagiArchv_Ending_v1_";
    private static string StageKey(string id, string suffix) =>
        Prefix + "Stage_" + Convert.ToBase64String(Encoding.UTF8.GetBytes(id)) + "_" + suffix;
    public static bool NormalSeen => PlayerPrefs.GetInt(Prefix + "NormalSeen", 0) == 1;
    public static bool TrueSeen => PlayerPrefs.GetInt(Prefix + "TrueSeen", 0) == 1;
    public static bool NormalRouteForced => PlayerPrefs.GetInt(Prefix + "NormalRouteForced", 0) == 1;
    public static EndingKind PendingEnding => (EndingKind)PlayerPrefs.GetInt(Prefix + "Pending", 0);
    public static int Attempts(string id) => PlayerPrefs.GetInt(StageKey(id, "Attempts"), 0);
    public static bool FirstClearFastest(string id) => PlayerPrefs.GetInt(StageKey(id, "FirstFastest"), 0) == 1;

    public static void RegisterEntry(string id)
    {
        int previous = Attempts(id);
        // 導入前の自己ベストだけでは「初回で最短」を証明できないので通常ルートへ。
        bool legacyRecord = previous == 0 && StageResult.IsCleared(id);
        int count = previous < int.MaxValue ? previous + 1 : previous;
        PlayerPrefs.SetInt(StageKey(id, "Attempts"), count);
        if (!NormalSeen && (count >= 2 || legacyRecord))
            PlayerPrefs.SetInt(Prefix + "NormalRouteForced", 1);
        PlayerPrefs.Save();
    }

    public static void RegisterClear(string id, int movesUsed, int parMoves)
    {
        bool fastest = parMoves > 0 && movesUsed <= parMoves;
        string key = StageKey(id, "FirstClearRecorded");
        if (PlayerPrefs.GetInt(key, 0) == 0)
        {
            PlayerPrefs.SetInt(key, 1);
            PlayerPrefs.SetInt(StageKey(id, "FirstFastest"), fastest && Attempts(id) == 1 ? 1 : 0);
        }
        if (!NormalSeen && (!fastest || Attempts(id) != 1))
            PlayerPrefs.SetInt(Prefix + "NormalRouteForced", 1);
        PlayerPrefs.Save();
    }

    // 純粋な分岐関数。判定タイミングは最終ステージのクリアに限定。
    public static EndingKind Decide(bool finalStage, bool currentFinalFastest,
        bool allBestFastest, bool normalSeen, bool normalForced, bool allFirstTryFastest)
    {
        if (!finalStage) return EndingKind.None;
        if (!currentFinalFastest || !allBestFastest) return EndingKind.Normal;
        if (normalSeen) return EndingKind.True;
        return !normalForced && allFirstTryFastest ? EndingKind.True : EndingKind.Normal;
    }

    public static EndingKind Evaluate(EndingSettings settings, string clearedId, int movesUsed)
    {
        var stage = settings.FindStage(clearedId);
        if (stage == null || clearedId != settings.finalStageId) return EndingKind.None;
        bool allBest = true, allFirst = true;
        foreach (var s in settings.storyStages)
        {
            allBest &= StageResult.IsCleared(s.Id) && StageResult.IsFastestAchieved(s.Id, s.parMoves);
            allFirst &= Attempts(s.Id) == 1 && FirstClearFastest(s.Id);
        }
        return Decide(true, movesUsed <= stage.parMoves, allBest, NormalSeen, NormalRouteForced, allFirst);
    }

    public static void Queue(EndingKind kind)
    {
        if (kind == EndingKind.None) return;
        PlayerPrefs.SetInt(Prefix + "Pending", (int)kind);
        PlayerPrefs.Save();
    }

    // シーンへの入場が閲覧条件。空のエンディングシーンでもRuntimeから呼ばれる。
    // すでに付いたもう一方の閲覧記録は保持し、再開予約だけを解除する。
    public static bool RecordEndingEntered(EndingKind kind)
    {
        if (kind != EndingKind.Normal && kind != EndingKind.True) return false;
        string seenKey = Prefix + (kind == EndingKind.True ? "TrueSeen" : "NormalSeen");
        if (PlayerPrefs.GetInt(seenKey, 0) == 1 && !PlayerPrefs.HasKey(Prefix + "Pending"))
            return true;
        PlayerPrefs.SetInt(seenKey, 1);
        PlayerPrefs.DeleteKey(Prefix + "Pending");
        PlayerPrefs.Save();
        return true;
    }

    // 旧コードとの互換用。現在の通常フローはシーン入場時に記録する。
    public static bool CompletePending(EndingKind kind)
    {
        if (kind == EndingKind.None || PendingEnding != kind) return false;
        return RecordEndingEntered(kind);
    }
}
