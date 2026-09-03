using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 各シーン個別のUIボタンから呼び出し、全シーン共通の倍速状態を切り替え・適用するクラスです。
/// シーン遷移後も前シーンで設定した倍速（1.0x / 2.0x）が維持されます。
/// </summary>
public class SpeedController : MonoBehaviour
{
    // 静的変数（Static）として保持することで、シーンが変わっても設定値が引き継がれます
    private static bool isFastSpeed = false;

    [Header("UI Settings")]
    [Tooltip("速度切り替え用のUI Button")]
    [SerializeField] private Button speedButton;

    [Tooltip("倍速を表示するテキスト (TextMeshPro)")]
    [SerializeField] private TextMeshProUGUI speedTextTMP;

    [Tooltip("倍速を表示するテキスト (通常のUI Text)")]
    [SerializeField] private Text speedText;

    private void Start()
    {
        // ボタンのクリックイベントを登録
        if (speedButton != null)
        {
            speedButton.onClick.RemoveAllListeners();
            speedButton.onClick.AddListener(ToggleSpeed);
        }

        // シーン読み込み時に、前シーンから引き継いだ isFastSpeed の状態を反映
        ApplySpeed();
    }

    /// <summary>
    /// 1倍速と2倍速を交互に切り替え、画面と Time.timeScale に反映します。
    /// </summary>
    public void ToggleSpeed()
    {
        isFastSpeed = !isFastSpeed;
        ApplySpeed();
    }

    /// <summary>
    /// 現在の isFastSpeed 状態を Time.timeScale および このシーンのUI表示に反映します。
    /// </summary>
    private void ApplySpeed()
    {
        // ゲームの進行速度を変更
        Time.timeScale = isFastSpeed ? 2.0f : 1.0f;

        // このシーンのUI表示を更新
        string textValue = isFastSpeed ? "2.0x" : "1.0x";

        if (speedTextTMP != null)
        {
            speedTextTMP.text = textValue;
        }

        if (speedText != null)
        {
            speedText.text = textValue;
        }
    }
}