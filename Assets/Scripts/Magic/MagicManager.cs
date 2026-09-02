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
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return; // キーボードが接続されていない場合はスキップ

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
        if (ActiveMagic != type) return false;

        switch (type)
        {
            case MagicType.Fire:
                if (fireMagicCount > 0)
                {
                    fireMagicCount--;
                    ActiveMagic = MagicType.None;
                    UpdateUI();
                    return true;
                }
                break;

            case MagicType.Water:
                if (waterMagicCount > 0)
                {
                    waterMagicCount--;
                    ActiveMagic = MagicType.None;
                    UpdateUI();
                    return true;
                }
                break;

            case MagicType.Wind:
                if (windMagicCount > 0)
                {
                    windMagicCount--;
                    ActiveMagic = MagicType.None;
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

    private void UpdateUI()
    {
        if (fireCountText != null) fireCountText.text = fireMagicCount.ToString();
        if (waterCountText != null) waterCountText.text = waterMagicCount.ToString();
        if (windCountText != null) windCountText.text = windMagicCount.ToString();
    }
}