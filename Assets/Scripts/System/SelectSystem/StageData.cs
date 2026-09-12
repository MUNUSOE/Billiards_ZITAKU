using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class StageData
{
    public string stageId;       // 例: "1-1"
    public string stageName;     // 例: "1-1"
    public string sceneToLoad;   // 移動先のシーン名 (例: "Stage_1_1")
    public bool isUnlocked = true;

    [Tooltip("このステージの最速手。StageInfoのParMovesと同じ値を設定します。星3つ目の条件になります。")]
    public int parMoves = 3;     // 最速クリア判定用の規定手数

    [Tooltip("星2つ目の条件となる手数。この手数以内でクリアすると星2つ。\nparMoves 以上の値（＝緩い条件）を設定します。")]
    public int twoStarMoves = 5;

    [Tooltip("【未使用】星の数はクリア記録から自動計算されます。手動で指定したい場合のみ使います。")]
    [Range(0, 3)] public int starCount = 0;

    [Tooltip("ステージセレクト画面で表示するサムネイル画像")]
    public Sprite stageImage;

    [Tooltip("このステージで星評価のUIを表示するかどうか")]
    public bool showStarRating = true; // ★追加: 星表示のオンオフ用チェックボックス
}

[System.Serializable]
public class BookData
{
    public int bookId;           // 本の番号 (1, 2...)
    public string bookTitle;     // 本のタイトル (例: "1の本")
    public Sprite bookCover;     // 表紙画像
    public List<StageData> stages = new List<StageData>();
}