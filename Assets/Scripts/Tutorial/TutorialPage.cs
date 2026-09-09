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

    /// <summary>魔法を含むすべての操作を許可。通常プレイと同じ状態。</summary>
    AllMoveAccept,

    /// <summary>方向と威力は固定したまま、打ち出しだけ許可。</summary>
    FixedShotOnly,

    /// <summary>魔法の選択だけ許可。方向・威力・打ち出しはできません。</summary>
    SelectMagicOnly,
}

/// <summary>
/// 次のページへ進む条件です。複数を組み合わせて指定できます（Inspector ではチェックボックス形式）。
/// SceneChange は単独の条件ではなく、「進むときに次のシーンへ遷移する」という指定です。
/// </summary>
[System.Flags]
public enum TutorialAdvanceMode
{
    None = 0,

    /// <summary>左クリックで次へ。テキスト送り用。</summary>
    LeftClick = 1 << 0,

    /// <summary>スペースキーで次へ。「Space to Next」の操作説明ページ用。</summary>
    SpaceKey = 1 << 1,

    /// <summary>実際に球を打ち出し、演出が終わったら自動で次へ。</summary>
    Shot = 1 << 2,

    /// <summary>選択肢ボタンを押したら次へ。</summary>
    Choice = 1 << 3,

    /// <summary>指定した魔法が選択されたら次へ。魔法ボタン（キーボード・UIどちらでも）を押させるページ用。</summary>
    MagicSelected = 1 << 5,

    /// <summary>進むとき、次のページではなく次のシーンへ遷移する。他の条件と組み合わせて使います。</summary>
    SceneChange = 1 << 4,
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
    [Tooltip("次へ進む条件。複数選択できます。SceneChange を含めると、進むときにシーン遷移します。")]
    public TutorialAdvanceMode advanceMode = TutorialAdvanceMode.LeftClick;

    [Header("方向・威力の指定")]
    [Tooltip("方向を指定する。Input Mode が方向変更を許可している場合は「初期値」として設定され、禁止している場合はその方向に固定されます。SelectMagicOnly など操作を止めるモードでも指定できます。")]
    public bool useForcedDirection = false;

    [Tooltip("指定する方向。8方向のいずれか（例: 左向きなら X=-1, Z=0）。")]
    public Vector3 forcedDirection = new Vector3(-1f, 0f, 0f);

    [Tooltip("威力を指定する。Input Mode が威力変更を許可している場合は「初期値」として設定され、禁止している場合はその値に固定されます。")]
    public bool useForcedPower = false;

    [Tooltip("指定する威力レベル。0=弱(1マス) 1=中(2マス) 2=強(3マス)。")]
    [Range(0, 2)]
    public int forcedPowerLevel = 0;

    [Tooltip("使用する魔法を指定する。プレイヤーに押させたい魔法、または最初から選択させておきたい魔法を指定します。")]
    public bool useForcedMagic = false;

    [Tooltip("指定する魔法。None なら魔法なしの状態にします。")]
    public MagicType forcedMagicType = MagicType.None;

    [Tooltip("チェックすると、指定した魔法だけを押せる状態にします。他の魔法のボタン・キーは反応しません。")]
    public bool allowOnlyForcedMagic = false;

    [Tooltip("チェックすると、ページに入った時点で指定した魔法を選択済みにします。プレイヤーに押させる場合は外してください。")]
    public bool selectForcedMagicOnEnter = false;

    [Header("待機（Advance Mode が Shot のとき）")]
    [Tooltip("ショットの処理が終わってから次のページへ進むまでの待機秒数。\n魔法の発動エフェクトは球の移動より長く残るため、その表示を見せ切りたい場合に設定します。")]
    [Min(0f)]
    public float advanceDelayAfterShot = 0f;

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