using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// ヘルプ画面の表示・非表示、およびメニュー項目の切り替えを管理するクラス
/// </summary>
public class HelpManager : MonoBehaviour
{
    // OptionManagerと同じように、外部から簡単にアクセスできるようにする
    public static HelpManager Instance { get; private set; }

    [System.Serializable]
    public class CategoryData
    {
        [Tooltip("このカテゴリを選択・解除するためのボタン")]
        public Button categoryButton;

        [Tooltip("このカテゴリが選択されたときに表示する、ボタン群をまとめた親オブジェクト")]
        public GameObject menuContainer;
    }

    [Header("UI References")]
    [Tooltip("ヘルプ画面全体のコンテナ（暗い背景＋ヘルプウィンドウ）")]
    [SerializeField] private GameObject helpContainer;

    [Tooltip("ヘルプを開くボタン（ヒントアイコン/電球）")]
    [SerializeField] private Button openButton;

    [Tooltip("ヘルプを閉じるボタン（✖アイコン）")]
    [SerializeField] private Button closeButton;

    [Header("Content Switching")]
    [Tooltip("すべてのメニューボタン（カテゴリ問わず）を順番に登録します")]
    [SerializeField] private Button[] menuButtons;

    [Tooltip("右側に表示するコンテンツパネル配列（menuButtonsとインデックスを合わせる）")]
    [SerializeField] private GameObject[] contentPanels;

    [Header("Category Filtering")]
    [Tooltip("カテゴリボタンと、それに紐づくメニューコンテナの設定")]
    [SerializeField] private List<CategoryData> categories;

    [Header("Highlight Frame")]
    [Tooltip("選択中のボタンを示す黄色い枠のUI (RectTransform)")]
    [SerializeField] private RectTransform highlightFrame;

    // 現在選択中のカテゴリを記憶しておく変数
    private CategoryData currentActiveCategory = null;

    // ヘルプ画面が開いているかどうかを外部に教えるフラグ
    private bool isHelpOpen = false;
    public bool IsHelpOpen => isHelpOpen;

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
        if (helpContainer != null) helpContainer.SetActive(false);

        if (openButton != null) openButton.gameObject.SetActive(true);
        if (closeButton != null) closeButton.gameObject.SetActive(false);

        if (openButton != null) openButton.onClick.AddListener(OpenHelp);
        if (closeButton != null) closeButton.onClick.AddListener(CloseHelp);

        // メニューボタンのクリックイベント
        for (int i = 0; i < menuButtons.Length; i++)
        {
            int index = i;
            if (menuButtons[i] != null)
            {
                menuButtons[i].onClick.AddListener(() => SwitchContent(index));
            }
        }

        // カテゴリボタンのイベント
        if (categories != null)
        {
            foreach (var category in categories)
            {
                if (category.categoryButton != null)
                {
                    CategoryData cat = category;
                    category.categoryButton.onClick.AddListener(() => ToggleCategory(cat));
                }
            }
        }
    }

    public void OpenHelp()
    {
        isHelpOpen = true; // フラグをON

        if (helpContainer != null) helpContainer.SetActive(true);

        if (openButton != null) openButton.gameObject.SetActive(false);
        if (closeButton != null) closeButton.gameObject.SetActive(true);

        ResetCategoryFilter();
        ClearContentAndHighlight();

        // ゲーム内のアニメーション等も止める
        Time.timeScale = 0f;
    }

    public void CloseHelp()
    {
        isHelpOpen = false; // フラグをOFF

        if (helpContainer != null) helpContainer.SetActive(false);

        if (openButton != null) openButton.gameObject.SetActive(true);
        if (closeButton != null) closeButton.gameObject.SetActive(false);

        // ゲームのポーズを解除する
        Time.timeScale = 1f;
    }

    private void SwitchContent(int index)
    {
        if (contentPanels == null) return;

        for (int i = 0; i < contentPanels.Length; i++)
        {
            if (contentPanels[i] != null)
            {
                contentPanels[i].SetActive(i == index);
            }
        }

        if (highlightFrame != null && menuButtons != null && index >= 0 && index < menuButtons.Length)
        {
            Button selectedBtn = menuButtons[index];
            if (selectedBtn != null)
            {
                highlightFrame.gameObject.SetActive(true);
                highlightFrame.SetParent(selectedBtn.transform.parent, false);

                RectTransform btnRect = selectedBtn.GetComponent<RectTransform>();
                highlightFrame.position = btnRect.position;
                highlightFrame.sizeDelta = btnRect.sizeDelta;

                highlightFrame.SetAsLastSibling();
            }
        }
    }

    private void ToggleCategory(CategoryData category)
    {
        if (currentActiveCategory == category)
        {
            ResetCategoryFilter();
        }
        else
        {
            currentActiveCategory = category;
            ApplyCategoryFilter();
        }

        ClearContentAndHighlight();
    }

    private void ResetCategoryFilter()
    {
        currentActiveCategory = null;
        if (categories == null) return;

        foreach (var cat in categories)
        {
            if (cat != null && cat.menuContainer != null)
            {
                cat.menuContainer.SetActive(true);
            }
        }
    }

    private void ApplyCategoryFilter()
    {
        if (categories == null) return;

        foreach (var cat in categories)
        {
            if (cat != null && cat.menuContainer != null)
            {
                cat.menuContainer.SetActive(cat == currentActiveCategory);
            }
        }
    }

    private void ClearContentAndHighlight()
    {
        if (contentPanels != null)
        {
            for (int i = 0; i < contentPanels.Length; i++)
            {
                if (contentPanels[i] != null) contentPanels[i].SetActive(false);
            }
        }
        if (highlightFrame != null) highlightFrame.gameObject.SetActive(false);
    }
}