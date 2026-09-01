using UnityEngine;
using UnityEngine.UI; // レガシー/標準の UI Text を使用

public class GameManagerUI : MonoBehaviour
{
    [SerializeField] private Text movesText; // 標準 Text コンポーネントをアタッチ

    private void OnEnable()
    {
        RegisterEvent();
    }

    private void Start()
    {
        RegisterEvent();
        if (GameManager.Instance != null)
        {
            UpdateMovesUI(GameManager.Instance.CurrentMoves);
        }
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnMovesChanged -= UpdateMovesUI;
        }
    }

    private void RegisterEvent()
    {
        if (GameManager.Instance != null)
        {
            // 二重登録を防ぐため、一度解除してから登録
            GameManager.Instance.OnMovesChanged -= UpdateMovesUI;
            GameManager.Instance.OnMovesChanged += UpdateMovesUI;
        }
    }

    /// <summary>
    /// UIテキストの描画更新
    /// </summary>
    private void UpdateMovesUI(int remainingMoves)
    {
        if (movesText != null)
        {
            movesText.text = $"残り {remainingMoves} 手";
        }
    }
}