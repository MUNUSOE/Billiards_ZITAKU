using UnityEngine;
using UnityEngine.UI;

/// <summary>チュートリアル中に許可する操作の組み合わせです。</summary>
public enum TutorialInputMode
{
    /// <summary>操作不可。テキストを読ませるだけのページ。</summary>
    None,

    /// <summary>方向の変更のみ許可。</summary>
    DirectionOnly,

    /// <summary>方向と威力の変更を許可。</summary>
    DirectionAndPower,

    /// <summary>魔法以外のすべて（方向・威力・打ち出し）を許可。</summary>
    AllExceptMagic,

    /// <summary>方向と威力は固定したまま、打ち出しだけ許可。</summary>
    FixedShotOnly,
}

/// <summary>次のページへ進む条件です。</summary>
public enum TutorialAdvanceMode
{
    /// <summary>左クリックで次へ。テキスト送り用。</summary>
    LeftClick,

    /// <summary>スペースキーで次へ。「Space to Next」の操作説明ページ用。</summary>
    SpaceKey,

    /// <summary>実際に球を打ち出したら次へ。</summary>
    Shot,

    /// <summary>選択肢ボタンを押したら次へ。</summary>
    Choice,

    /// <summary>左クリックで次のシーンへ遷移する。</summary>
    SceneChange,
}

/// <summary>
/// チュートリアルの1ページ分の設定です。
/// Inspector で1ページずつ並べて設定します。
/// </summary>
[System.Serializable]
public class TutorialPage
{
    [Tooltip("識別用の名前。Inspector で見分けるためだけに使います。")]
    public string pageName;

    [Header("表示")]
    [Tooltip("このページで表示するオブジェクト（吹き出し・操作説明パネル・動画など）。ページが切り替わると自動で非表示になります。")]
    public GameObject displayObject;

    [Header("操作")]
    public TutorialInputMode inputMode = TutorialInputMode.None;
    public TutorialAdvanceMode advanceMode = TutorialAdvanceMode.LeftClick;

    [Header("固定ショット設定（Input Mode が FixedShotOnly のとき）")]
    [Tooltip("固定する方向。8方向のいずれか（例: 左向きなら X=-1, Z=0）。")]
    public Vector3 forcedDirection = new Vector3(-1f, 0f, 0f);

    [Tooltip("固定する威力レベル。0=弱(1マス) 1=中(2マス) 2=強(3マス)。")]
    [Range(0, 2)]
    public int forcedPowerLevel = 0;

    [Header("選択肢（Advance Mode が Choice のとき）")]
    [Tooltip("選択肢のボタン。並び順が選択肢の番号になります。")]
    public Button[] choiceButtons;

    [Tooltip("正解の選択肢の番号（0から数えます）。")]
    public int correctChoiceIndex = 2;

    [Header("選択結果の表示（前のページの選択に応じて出し分け）")]
    [Tooltip("正解だった場合に表示するオブジェクト。")]
    public GameObject correctResultObject;

    [Tooltip("不正解だった場合に表示するオブジェクト。")]
    public GameObject wrongResultObject;

    [Header("シーン遷移（Advance Mode が SceneChange のとき）")]
    [Tooltip("遷移先のシーン名。")]
    public string nextSceneName;
}
