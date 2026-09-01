using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class StageData
{
    public string stageId;       // 例: "1-1"
    public string stageName;     // 例: "1-1"
    public string sceneToLoad;   // 移動先のシーン名 (例: "Stage_1_1")
    public bool isUnlocked = true;
    [Range(0, 3)] public int starCount = 0; // クリア時の星の数（0〜3）
}

[System.Serializable]
public class BookData
{
    public int bookId;           // 本の番号 (1, 2...)
    public string bookTitle;     // 本のタイトル (例: "1の本")
    public Sprite bookCover;     // 表紙画像
    public List<StageData> stages = new List<StageData>();
}