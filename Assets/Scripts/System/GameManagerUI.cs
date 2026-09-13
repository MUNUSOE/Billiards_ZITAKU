using UnityEngine;
using UnityEngine.UI;

public class GameManagerUI : MonoBehaviour
{
    [Header("Moves UI Settings")]
    [Tooltip("手数の画像を並べる親オブジェクト。ここに Horizontal Layout Group を付けておきます。")]
    [SerializeField] private Transform iconsContainer;

    [Tooltip("5手用のアイコン（プレハブ）")]
    [SerializeField] private GameObject fiveMovesIconPrefab;

    [Tooltip("1手用のアイコン（プレハブ）")]
    [SerializeField] private GameObject oneMoveIconPrefab;

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
    /// UIの描画更新（5手用と1手用の画像を計算して並べる）
    /// </summary>
    private void UpdateMovesUI(int remainingMoves)
    {
        if (iconsContainer == null) return;

        // まず、現在表示されているアイコンをすべて削除してリセットする
        foreach (Transform child in iconsContainer)
        {
            Destroy(child.gameObject);
        }

        // マイナスの場合は表示しない
        if (remainingMoves <= 0) return;

        // 5手用アイコンの数と、1手用アイコンの数を計算
        int fiveCount = remainingMoves / 5;
        int oneCount = remainingMoves % 5;

        // 5の画像を生成してコンテナの子にする
        if (fiveMovesIconPrefab != null)
        {
            for (int i = 0; i < fiveCount; i++)
            {
                Instantiate(fiveMovesIconPrefab, iconsContainer);
            }
        }

        // 1の画像を生成してコンテナの子にする
        if (oneMoveIconPrefab != null)
        {
            for (int i = 0; i < oneCount; i++)
            {
                Instantiate(oneMoveIconPrefab, iconsContainer);
            }
        }
    }
}