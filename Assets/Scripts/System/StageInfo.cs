using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ステージ1つ分の情報です。各ステージのシーンに1つ置きます。
/// ステージの識別にはシーン名を使うので、通常は最速手を設定するだけで動きます。
/// </summary>
public class StageInfo : MonoBehaviour
{
    public static StageInfo Instance { get; private set; }

    [Header("Stage Settings")]
    [Tooltip("このステージの最速手。プレイヤーがこの手数以内でクリアすると最速手達成（星3つ）になります。")]
    [Min(1)]
    [SerializeField] private int parMoves = 3;

    [Tooltip("星2つ目の条件となる手数。この手数以内でクリアすると星2つ。\nParMoves 以上の値（＝緩い条件）を設定します。")]
    [Min(1)]
    [SerializeField] private int twoStarMoves = 5;

    [Tooltip("ステージの識別名。空ならシーン名を使います。シーン名と別のIDにしたい場合だけ入力してください。")]
    [SerializeField] private string stageIdOverride = "";

    /// <summary>このステージの識別名。</summary>
    public string StageId =>
        string.IsNullOrEmpty(stageIdOverride) ? SceneManager.GetActiveScene().name : stageIdOverride;

    /// <summary>このステージの最速手。星3つ目の条件。</summary>
    public int ParMoves => parMoves;

    /// <summary>星2つ目の条件となる手数。</summary>
    public int TwoStarMoves => twoStarMoves;

    /// <summary>クリア済みか。</summary>
    public bool IsCleared => StageResult.IsCleared(StageId);

    /// <summary>最速手を達成済みか。</summary>
    public bool IsFastestAchieved => StageResult.IsFastestAchieved(StageId, parMoves);

    /// <summary>自己ベスト手数。未クリアなら -1。</summary>
    public int BestMoves => StageResult.GetBestMoves(StageId);

    /// <summary>獲得している星の数（0〜3）。</summary>
    public int StarCount => StageResult.GetStarCount(StageId, parMoves, twoStarMoves);

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}