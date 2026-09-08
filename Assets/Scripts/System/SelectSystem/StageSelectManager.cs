using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// ステージセレクト全体を管理する。
/// 本棚（本の一覧）→本を開く（見開き2ステージ表示、5ステージ/冊）→ステージ選択、の流れ。
/// 右ボタンでページを進める（例: 1-1,1-2 のページ → 1-3,1-4 のページ）、
/// 左ボタンで戻る。
/// </summary>
public class StageSelectManager : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject bookSelectionPanel;
    [SerializeField] private GameObject bookViewPanel;

    [Header("Book List UI")]
    [Tooltip("本棚に手動配置した本ボタンの親。子には BookShelfItemView を付けておく。")]
    [SerializeField] private Transform bookGridContainer;

    [Header("Opened Book UI (Left Page)")]
    [Tooltip("左ページ全体のRectTransform（CurlPageの位置・サイズ合わせに使う）。")]
    [SerializeField] private RectTransform leftPageRect;
    [SerializeField] private Text leftPageTitleText;
    [SerializeField] private Text leftPageNumberText;
    [SerializeField] private Button leftPlayButton;
    [SerializeField] private StarRatingView leftStarRating;
    [Tooltip("左ページの「最速クリア」テキスト")]
    [SerializeField] private Text leftFastestClearText;

    [Header("Opened Book UI (Right Page)")]
    [Tooltip("右ページ全体のRectTransform（CurlPageの位置・サイズ合わせに使う）。")]
    [SerializeField] private RectTransform rightPageRect;
    [SerializeField] private Text rightPageTitleText;
    [SerializeField] private Text rightPageNumberText;
    [SerializeField] private Button rightPlayButton;
    [SerializeField] private StarRatingView rightStarRating;
    [Tooltip("右ページの「最速クリア」テキスト")]
    [SerializeField] private Text rightFastestClearText;

    [Header("Book Controls")]
    [SerializeField] private Button closeBookButton;
    [SerializeField] private Button nextBookPageButton; // 右ページ送り(->)
    [SerializeField] private Button prevBookPageButton; // 左ページ戻り(<-)

    [Header("Animation & Data")]
    [SerializeField] private BookPageCurl pageCurl;
    [SerializeField] private List<BookData> booksData = new List<BookData>();

    private BookData currentBook;
    private int currentPagePairIndex = 0; // 見開きペアインデックス (0 = 1&2, 1 = 3&4 ...)

    private void Start()
    {
        Time.timeScale = 1f;

        InitializeSampleData();
        ShowBookSelection();

        if (closeBookButton != null) closeBookButton.onClick.AddListener(ShowBookSelection);
        if (nextBookPageButton != null) nextBookPageButton.onClick.AddListener(() => OnClickPageChange(true));
        if (prevBookPageButton != null) prevBookPageButton.onClick.AddListener(() => OnClickPageChange(false));
    }

    private void InitializeSampleData()
    {
        if (booksData.Count > 0) return;

        for (int b = 1; b <= 12; b++)
        {
            BookData book = new BookData
            {
                bookId = b,
                bookTitle = $"{b} の本"
            };

            for (int s = 1; s <= 5; s++)
            {
                book.stages.Add(new StageData
                {
                    stageId = $"{b}-{s}",
                    stageName = $"{b}-{s}",
                    sceneToLoad = $"{b}-{s}",
                    isUnlocked = true,
                    parMoves = 3,
                    starCount = 0,
                });
            }
            booksData.Add(book);
        }
    }

    // -------------------------------------------------------------
    // 本棚（本の選択画面）
    // -------------------------------------------------------------
    public void ShowBookSelection()
    {
        if (bookSelectionPanel != null) bookSelectionPanel.SetActive(true);
        if (bookViewPanel != null) bookViewPanel.SetActive(false);

        if (bookGridContainer == null) return;

        int childCount = bookGridContainer.childCount;

        for (int i = 0; i < childCount; i++)
        {
            Transform child = bookGridContainer.GetChild(i);

            if (i >= booksData.Count)
            {
                child.gameObject.SetActive(false);
                continue;
            }

            child.gameObject.SetActive(true);
            BookData book = booksData[i];

            BookShelfItemView itemView = child.GetComponent<BookShelfItemView>();
            if (itemView != null)
            {
                itemView.SetData(book);

                if (itemView.Button != null)
                {
                    itemView.Button.onClick.RemoveAllListeners();
                    itemView.Button.onClick.AddListener(() => OpenBook(book));
                }
            }
            else
            {
                Text t = child.GetComponentInChildren<Text>();
                if (t != null) t.text = book.bookTitle;

                Button btn = child.GetComponent<Button>();
                if (btn == null) btn = child.GetComponentInChildren<Button>();

                if (btn != null)
                {
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() => OpenBook(book));
                }
            }
        }
    }

    // -------------------------------------------------------------
    // 見開き本画面
    // -------------------------------------------------------------
    public void OpenBook(BookData book)
    {
        if (book == null) return;

        currentBook = book;
        currentPagePairIndex = 0;

        if (bookSelectionPanel != null) bookSelectionPanel.SetActive(false);
        if (bookViewPanel != null) bookViewPanel.SetActive(true);

        UpdatePageUI();
    }

    private void UpdatePageUI()
    {
        if (currentBook == null) return;

        int leftStageIndex = currentPagePairIndex * 2;
        int rightStageIndex = leftStageIndex + 1;

        UpdateSinglePage(leftStageIndex, leftPageTitleText, leftPageNumberText, leftPlayButton, leftStarRating, leftFastestClearText);
        UpdateSinglePage(rightStageIndex, rightPageTitleText, rightPageNumberText, rightPlayButton, rightStarRating, rightFastestClearText);

        int totalStages = currentBook.stages.Count;
        if (prevBookPageButton != null) prevBookPageButton.interactable = (currentPagePairIndex > 0);
        if (nextBookPageButton != null) nextBookPageButton.interactable = (rightStageIndex < totalStages - 1);
    }

    private void UpdateSinglePage(int stageIndex, Text titleText, Text pageNumText, Button playBtn, StarRatingView starRating, Text fastestClearText)
    {
        bool hasStage = currentBook != null && stageIndex >= 0 && stageIndex < currentBook.stages.Count;

        if (hasStage)
        {
            StageData stage = currentBook.stages[stageIndex];

            if (titleText != null)
            {
                titleText.gameObject.SetActive(true);
                titleText.text = stage.stageName;
            }

            if (pageNumText != null)
            {
                pageNumText.gameObject.SetActive(true);
                pageNumText.text = $"- {stageIndex + 1} -";
            }

            if (playBtn != null)
            {
                playBtn.gameObject.SetActive(true);
                playBtn.interactable = stage.isUnlocked;
                playBtn.onClick.RemoveAllListeners();
                playBtn.onClick.AddListener(() => OnSelectStage(stage));
            }

            if (starRating != null)
            {
                starRating.gameObject.SetActive(true);
                starRating.SetStarCount(stage.starCount);
            }

            if (fastestClearText != null)
            {
                bool isFastest = StageResult.IsFastestAchieved(stage.stageId, stage.parMoves);
                fastestClearText.gameObject.SetActive(isFastest);
            }
        }
        else
        {
            if (titleText != null) titleText.gameObject.SetActive(false);
            if (pageNumText != null) pageNumText.gameObject.SetActive(false);
            if (playBtn != null) playBtn.gameObject.SetActive(false);
            if (starRating != null) starRating.gameObject.SetActive(false);
            if (fastestClearText != null) fastestClearText.gameObject.SetActive(false);
        }
    }

    private void OnClickPageChange(bool isNext)
    {
        if (pageCurl != null && pageCurl.IsAnimating) return;
        StartCoroutine(PageChangeRoutine(isNext));
    }

    private IEnumerator PageChangeRoutine(bool isNext)
    {
        if (pageCurl != null)
        {
            RectTransform sourcePageRect = isNext ? rightPageRect : leftPageRect;
            int outgoingStageIndex = isNext ? (currentPagePairIndex * 2 + 1) : (currentPagePairIndex * 2);
            StageData outgoingStage = (currentBook != null && outgoingStageIndex >= 0 && outgoingStageIndex < currentBook.stages.Count)
                ? currentBook.stages[outgoingStageIndex]
                : null;

            yield return StartCoroutine(pageCurl.PlayCurlAnimation(isNext, sourcePageRect, outgoingStage, outgoingStageIndex + 1, () =>
            {
                if (isNext) currentPagePairIndex++;
                else currentPagePairIndex--;

                UpdatePageUI();
            }));
        }
        else
        {
            if (isNext) currentPagePairIndex++;
            else currentPagePairIndex--;
            UpdatePageUI();
        }
    }

    private void OnSelectStage(StageData stage)
    {
        if (stage == null) return;

        if (!string.IsNullOrEmpty(stage.sceneToLoad))
        {
            SceneManager.LoadScene(stage.sceneToLoad);
        }
    }
}