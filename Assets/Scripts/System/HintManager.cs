using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// ヒント機能のメニュー開閉と、画面上部へのヒントテキスト表示を管理します。
/// </summary>
public class HintManager : MonoBehaviour
{
    public static HintManager Instance { get; private set; }

    [Header("UI References")]
    [Tooltip("ヒントメニュー全体（ヒント①②③のボタンが含まれる背景）")]
    [SerializeField] private GameObject hintMenuPanel;

    [Tooltip("ヒントを開くボタン（電球アイコン）")]
    [SerializeField] private Button openButton;

    [Tooltip("ヒントを閉じるボタン（✖アイコン）")]
    [SerializeField] private Button closeButton;

    [Header("Hint Display (画面上部)")]
    [Tooltip("画面上部に表示するヒントテキストの背景パネル")]
    [SerializeField] private GameObject hintDisplayPanel;

    [Tooltip("画面上部に表示するヒントテキスト")]
    [SerializeField] private Text hintDisplayText;

    [Header("Hint Data")]
    [Tooltip("メニュー内の各ヒントボタン（ヒント①、ヒント②...）")]
    [SerializeField] private List<Button> hintButtons;

    [Tooltip("各ボタンを押したときに表示するテキスト（上のリストと順番を合わせてください）")]
    [SerializeField, TextArea(2, 5)] private List<string> hintTexts;

    // ヒントメニューが開いているかどうか（ShotBallの操作ロック判定用）
    public bool IsHintOpen { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        // 初期状態のセット
        if (hintMenuPanel != null) hintMenuPanel.SetActive(false);
        if (hintDisplayPanel != null) hintDisplayPanel.SetActive(false);

        if (openButton != null)
        {
            openButton.gameObject.SetActive(true);
            openButton.onClick.AddListener(OpenHintMenu);
        }

        if (closeButton != null)
        {
            closeButton.gameObject.SetActive(false);
            closeButton.onClick.AddListener(CloseHintMenu);
        }

        // 各ヒントボタンにクリック時のイベントを割り当て
        for (int i = 0; i < hintButtons.Count; i++)
        {
            int index = i; // クロージャ対策
            if (hintButtons[i] != null)
            {
                hintButtons[i].onClick.AddListener(() => OnHintButtonClicked(index));
            }
        }
    }

    public void OpenHintMenu()
    {
        IsHintOpen = true;
        if (hintMenuPanel != null) hintMenuPanel.SetActive(true);
        if (openButton != null) openButton.gameObject.SetActive(false);
        if (closeButton != null) closeButton.gameObject.SetActive(true);

        // メニューを開いている間はゲームをポーズする
        Time.timeScale = 0f;

        // SEを鳴らす場合
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySE(SEType.DecideButton);
    }

    public void CloseHintMenu()
    {
        IsHintOpen = false;
        if (hintMenuPanel != null) hintMenuPanel.SetActive(false);
        if (openButton != null) openButton.gameObject.SetActive(true);
        if (closeButton != null) closeButton.gameObject.SetActive(false);

        // ゲームを再開する
        Time.timeScale = 1f;
    }

    private void OnHintButtonClicked(int index)
    {
        // テキストをセットして画面上部のパネルを表示
        if (index >= 0 && index < hintTexts.Count)
        {
            if (hintDisplayText != null)
            {
                hintDisplayText.text = hintTexts[index];
            }
            if (hintDisplayPanel != null)
            {
                hintDisplayPanel.SetActive(true);
            }
        }

        // SEを鳴らす場合
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySE(SEType.DecideButton);

        // ヒントを選んだらメニューを閉じてゲームを再開する
        CloseHintMenu();
    }

    /// <summary>
    /// 表示中のヒントテキストを隠す。ShotBallからボールが打ち出された瞬間に呼ばれる。
    /// </summary>
    public void HideHintText()
    {
        if (hintDisplayPanel != null)
        {
            hintDisplayPanel.SetActive(false);
        }
    }
}