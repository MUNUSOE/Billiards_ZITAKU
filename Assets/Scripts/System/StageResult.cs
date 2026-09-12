using UnityEngine;

/// <summary>
/// ステージごとのクリア記録を保存・読み出しします。
/// 保存先は PlayerPrefs なので、ビルドしたアプリでもそのPCに記録が残ります。
///
/// 記録する内容:
///   - クリア済みかどうか
///   - 自己ベスト手数（クリア時に使った手数の最小値）
///
/// 「最速手達成」は、自己ベスト手数と各ステージに設定した最速手を比べて判定します。
/// 最速手の値を後から変更しても記録し直す必要がないよう、判定は都度計算する方式にしています。
/// </summary>
public static class StageResult
{
    private const string ClearedKeyFormat = "Stage_{0}_Cleared";
    private const string BestMovesKeyFormat = "Stage_{0}_BestMoves";

    /// <summary>そのステージをクリア済みか。</summary>
    public static bool IsCleared(string stageId)
    {
        if (string.IsNullOrEmpty(stageId)) return false;
        return PlayerPrefs.GetInt(string.Format(ClearedKeyFormat, stageId), 0) == 1;
    }

    /// <summary>
    /// 自己ベスト手数。未クリアの場合は -1 を返します。
    /// </summary>
    public static int GetBestMoves(string stageId)
    {
        if (string.IsNullOrEmpty(stageId)) return -1;
        return PlayerPrefs.GetInt(string.Format(BestMovesKeyFormat, stageId), -1);
    }

    /// <summary>
    /// 最速手を達成しているか。
    /// parMoves はそのステージの最速手（Inspector で設定した値）です。
    /// </summary>
    public static bool IsFastestAchieved(string stageId, int parMoves)
    {
        if (parMoves <= 0) return false;

        int best = GetBestMoves(stageId);
        if (best < 0) return false;

        return best <= parMoves;
    }

    /// <summary>
    /// 獲得している星の数（0〜3）を返します。
    ///
    /// 1つ目: クリア済み
    /// 2つ目: 自己ベストが twoStarMoves 以内
    /// 3つ目: 自己ベストが parMoves 以内（最速手達成）
    ///
    /// 手数は少ないほど良いため、twoStarMoves には parMoves 以上の値（緩い条件）を設定します。
    /// </summary>
    public static int GetStarCount(string stageId, int parMoves, int twoStarMoves)
    {
        if (!IsCleared(stageId)) return 0;

        int best = GetBestMoves(stageId);
        if (best < 0) return 1; // クリア済みだが手数の記録がない場合

        int stars = 1;

        // 設定ミス（2つ目の条件が最速手より厳しい）でも破綻しないよう、緩い方を採用する。
        int twoStarThreshold = Mathf.Max(twoStarMoves, parMoves);
        if (best <= twoStarThreshold) stars = 2;

        if (parMoves > 0 && best <= parMoves) stars = 3;

        return stars;
    }

    /// <summary>
    /// クリア結果を記録します。自己ベストはより少ない手数のときだけ更新されます。
    /// </summary>
    /// <returns>今回の記録で自己ベストが更新された場合 true。</returns>
    public static bool RecordClear(string stageId, int movesUsed)
    {
        if (string.IsNullOrEmpty(stageId)) return false;

        PlayerPrefs.SetInt(string.Format(ClearedKeyFormat, stageId), 1);

        int best = GetBestMoves(stageId);
        bool updated = best < 0 || movesUsed < best;

        if (updated)
        {
            PlayerPrefs.SetInt(string.Format(BestMovesKeyFormat, stageId), movesUsed);
        }

        PlayerPrefs.Save();

        Debug.Log($"[StageResult] {stageId} をクリア。使用手数={movesUsed} 自己ベスト={(updated ? movesUsed : best)} 更新={updated}");
        return updated;
    }

    /// <summary>そのステージの記録を消します。デバッグ用。</summary>
    public static void ClearRecord(string stageId)
    {
        if (string.IsNullOrEmpty(stageId)) return;

        PlayerPrefs.DeleteKey(string.Format(ClearedKeyFormat, stageId));
        PlayerPrefs.DeleteKey(string.Format(BestMovesKeyFormat, stageId));
        PlayerPrefs.Save();
    }
}