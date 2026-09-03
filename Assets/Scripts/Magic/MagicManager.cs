using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

public enum MagicType
{
    None,
    Fire,
    Water,
    Wind
}

public class MagicManager : MonoBehaviour
{
    public static MagicManager Instance { get; private set; }

    [Header("Fire Magic UI")]
    [SerializeField] private Button fireMagicButton;
    [SerializeField] private TextMeshProUGUI fireCountText;
    [SerializeField] private int fireMagicCount = 3;

    [Header("Water Magic UI")]
    [SerializeField] private Button waterMagicButton;
    [SerializeField] private TextMeshProUGUI waterCountText;
    [SerializeField] private int waterMagicCount = 3;

    [Header("Wind Magic UI")]
    [SerializeField] private Button windMagicButton;
    [SerializeField] private TextMeshProUGUI windCountText;
    [SerializeField] private int windMagicCount = 3;

    // 現在選択されている魔法
    public MagicType ActiveMagic { get; private set; } = MagicType.None;

    // ショットや魔法の演出中は選択を変更できないようにするためのロック。
    // 演出中に選択を切り替えられると、消費処理と表示がずれてしまうため。
    private bool selectionLocked;

    [Header("操作ロック")]
    [Tooltip("ショット球。演出中は魔法ボタンを押せないようにするため、操作可能かどうかを参照します。")]
    [SerializeField] private ShotBall shotBall;

    /// <summary>魔法の選択操作をロック／解除します。外部から明示的に制御したい場合に使います。</summary>
    public void SetSelectionLocked(bool locked)
    {
        selectionLocked = locked;
        UpdateButtonInteractable();
    }

    /// <summary>
    /// ショット球が操作可能かどうか。演出中や手数切れのときは false。
    /// shotBall が未設定の場合はロックしません。
    /// </summary>
    private bool IsShotBallOperable()
    {
        if (shotBall == null) return true;
        return shotBall.IsOperable;
    }

    /// <summary>
    /// ショット球の状態にあわせてロックを自動更新します。
    /// 演出の途中で抜ける経路があっても取り残されないよう、毎フレーム状態を見ます。
    /// </summary>
    private void RefreshSelectionLock()
    {
        bool shouldLock = !IsShotBallOperable();
        if (shouldLock == selectionLocked) return;

        selectionLocked = shouldLock;
        UpdateButtonInteractable();
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // UIボタンのイベント登録
        if (fireMagicButton != null) fireMagicButton.onClick.AddListener(() => ToggleMagic(MagicType.Fire));
        if (waterMagicButton != null) waterMagicButton.onClick.AddListener(() => ToggleMagic(MagicType.Water));
        if (windMagicButton != null) windMagicButton.onClick.AddListener(() => ToggleMagic(MagicType.Wind));

        UpdateUI();
    }

    private void Update()
    {
        // ショット球が操作可能になるまで、魔法の選択をロックする。
        RefreshSelectionLock();

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (selectionLocked) return;  // 演出中はキーでの切り替えも受け付けない // キーボードが接続されていない場合はスキップ

        // Qキーで炎魔法の選択・解除
        if (keyboard.qKey.wasPressedThisFrame)
        {
            ToggleMagic(MagicType.Fire);
        }
        // Wキーで水魔法の選択・解除
        else if (keyboard.wKey.wasPressedThisFrame)
        {
            ToggleMagic(MagicType.Water);
        }
        // Eキーで風魔法の選択・解除
        else if (keyboard.eKey.wasPressedThisFrame)
        {
            ToggleMagic(MagicType.Wind);
        }
    }

    /// <summary>
    /// 魔法の選択切り替え（トグル処理）
    /// </summary>
    public void ToggleMagic(MagicType type)
    {
        // 演出中の選択変更は、消費処理とのずれ（回数が減らないまま効果だけ出る）の原因になるため受け付けない。
        if (selectionLocked) return;

        int remaining = GetMagicCount(type);
        if (remaining <= 0) return; // 残り回数0なら選択不可

        if (ActiveMagic == type)
        {
            // すでに選択中なら解除（オフにする時はSEを鳴らさない）
            ActiveMagic = MagicType.None;
        }
        else
        {
            // 別の魔法を選択（オフ -> オン）
            ActiveMagic = type;

            // オンにした時だけ対応するSEを鳴らす
            switch (type)
            {
                case MagicType.Fire:
                    SoundManager.Instance?.PlaySE(SEType.FrameMagic);
                    break;
                case MagicType.Water:
                    SoundManager.Instance?.PlaySE(SEType.WaterMagic);
                    break;
                case MagicType.Wind:
                    SoundManager.Instance?.PlaySE(SEType.WindMagic);
                    break;
            }
        }
    }

    /// <summary>
    /// 魔法の消費処理（発動時）
    /// </summary>
    public bool ConsumeMagic(MagicType type)
    {
        // [変更] 以前は ActiveMagic != type で弾いていたため、演出中に選択を切り替えられると
        // 回数が減らないまま効果だけ発生していた。消費は「実際に使った魔法の種類」だけで判断する。
        switch (type)
        {
            case MagicType.Fire:
                if (fireMagicCount > 0)
                {
                    fireMagicCount--;
                    if (ActiveMagic == MagicType.Fire) ActiveMagic = MagicType.None;
                    UpdateUI();
                    return true;
                }
                break;

            case MagicType.Water:
                if (waterMagicCount > 0)
                {
                    waterMagicCount--;
                    if (ActiveMagic == MagicType.Water) ActiveMagic = MagicType.None;
                    UpdateUI();
                    return true;
                }
                break;

            case MagicType.Wind:
                if (windMagicCount > 0)
                {
                    windMagicCount--;
                    if (ActiveMagic == MagicType.Wind) ActiveMagic = MagicType.None;
                    UpdateUI();
                    return true;
                }
                break;
        }

        return false;
    }

    /// <summary>
    /// 指定属性の魔法回数を増やし、既存の回数表示を即時更新します。
    /// </summary>
    public bool AddMagic(MagicType type, int amount = 1)
    {
        if (amount <= 0) return false;

        switch (type)
        {
            case MagicType.Fire:
                fireMagicCount += amount;
                break;
            case MagicType.Water:
                waterMagicCount += amount;
                break;
            case MagicType.Wind:
                windMagicCount += amount;
                break;
            default:
                return false;
        }

        UpdateUI();
        return true;
    }

    public int GetMagicCount(MagicType type)
    {
        return type switch
        {
            MagicType.Fire => fireMagicCount,
            MagicType.Water => waterMagicCount,
            MagicType.Wind => windMagicCount,
            _ => 0,
        };
    }

    /// <summary>残り回数とロック状態に応じて、魔法ボタンの押下可否を更新します。</summary>
    private void UpdateButtonInteractable()
    {
        if (fireMagicButton != null) fireMagicButton.interactable = !selectionLocked && fireMagicCount > 0;
        if (waterMagicButton != null) waterMagicButton.interactable = !selectionLocked && waterMagicCount > 0;
        if (windMagicButton != null) windMagicButton.interactable = !selectionLocked && windMagicCount > 0;
    }

    private void UpdateUI()
    {
        if (fireCountText != null) fireCountText.text = fireMagicCount.ToString();
        if (waterCountText != null) waterCountText.text = waterMagicCount.ToString();
        if (windCountText != null) windCountText.text = windMagicCount.ToString();

        UpdateButtonInteractable();
    }
}