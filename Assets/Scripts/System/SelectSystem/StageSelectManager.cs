using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

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
    [SerializeField] private TMP_Text leftPageTitleText;
    [SerializeField] private TMP_Text leftPageNumberText;
    [SerializeField] private Button leftPlayButton;
    [SerializeField] private StarRatingView leftStarRating;

    [Header("Opened Book UI (Right Page)")]
    [Tooltip("右ページ全体のRectTransform（CurlPageの位置・サイズ合わせに使う）。")]
    [SerializeField] private RectTransform rightPageRect;
    [SerializeField] private TMP_Text rightPageTitleText;
    [SerializeField] private TMP_Text rightPageNumberText;
    [SerializeField] private Button rightPlayButton;
    [SerializeField] private StarRatingView rightStarRating;

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
        // クリア画面(GameClear.OpenClearUI)が Time.timeScale = 0f にしたまま
        // このシーンへ遷移してくる可能性があるため、メニュー画面としては必ず通常速度に戻す。
        Time.timeScale = 1f;

        InitializeSampleData();
        ShowBookSelection();

        if (closeBookButton != null) closeBookButton.onClick.AddListener(ShowBookSelection);
        if (nextBookPageButton != null) nextBookPageButton.onClick.AddListener(() => OnClickPageChange(true));
        if (prevBookPageButton != null) prevBookPageButton.onClick.AddListener(() => OnClickPageChange(false));
    }

    /// <summary>
    /// テスト用の本・ステージデータ作成（インスペクターで設定する場合は不要）。
    /// 1冊あたり5ステージ（例: 1-1〜1-5）を自動生成する。
    /// </summary>
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

        // Content の下にある子要素（手動配置した本）を取得してデータ・イベントを設定する。
        int childCount = bookGridContainer.childCount;

        for (int i = 0; i < childCount; i++)
        {
            Transform child = bookGridContainer.GetChild(i);

            if (i >= booksData.Count)
            {
                // データがない分は非表示。
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
                    itemView.Button.onClick.RemoveAllListeners(); // 二重登録防止
                    itemView.Button.onClick.AddListener(() => OpenBook(book));
                }
            }
            else
            {
                // BookShelfItemView が付いていない場合のフォールバック（タイトルのみ反映）。
                TMP_Text t = child.GetComponentInChildren<TMP_Text>();
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

        // 左ページ更新
        UpdateSinglePage(leftStageIndex, leftPageTitleText, leftPageNumberText, leftPlayButton, leftStarRating);

        // 右ページ更新
        UpdateSinglePage(rightStageIndex, rightPageTitleText, rightPageNumberText, rightPlayButton, rightStarRating);

        // 矢印ボタンの有効／無効切り替え
        int totalStages = currentBook.stages.Count;
        if (prevBookPageButton != null) prevBookPageButton.interactable = (currentPagePairIndex > 0);
        if (nextBookPageButton != null) nextBookPageButton.interactable = (rightStageIndex < totalStages - 1);
    }

    private void UpdateSinglePage(int stageIndex, TMP_Text titleText, TMP_Text pageNumText, Button playBtn, StarRatingView starRating)
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
        }
        else
        {
            // ステージが存在しないページ（最後の奇数ページ用）。
            if (titleText != null) titleText.gameObject.SetActive(false);
            if (pageNumText != null) pageNumText.gameObject.SetActive(false);
            if (playBtn != null) playBtn.gameObject.SetActive(false);
            if (starRating != null) starRating.gameObject.SetActive(false);
        }
    }

    private void OnClickPageChange(bool isNext)
    {
        Debug.Log($"[StageSelectManager] ページ送りボタン押下 isNext={isNext} pageCurl={(pageCurl != null ? "あり" : "null")} isAnimating={(pageCurl != null && pageCurl.IsAnimating)}");

        if (pageCurl != null && pageCurl.IsAnimating)
        {
            Debug.Log("[StageSelectManager] アニメーション中のため無視");
            return;
        }

        StartCoroutine(PageChangeRoutine(isNext));
    }

    private IEnumerator PageChangeRoutine(bool isNext)
    {
        Debug.Log($"[StageSelectManager] PageChangeRoutine 開始 isNext={isNext} currentPagePairIndex={currentPagePairIndex}");

        if (pageCurl != null)
        {
            // Nextは今の右ページが、Prevは今の左ページがめくれる。
            RectTransform sourcePageRect = isNext ? rightPageRect : leftPageRect;

            // めくれるページに今表示されているステージ（表示中の中身をCurlPageにコピーするため）。
            int outgoingStageIndex = isNext ? (currentPagePairIndex * 2 + 1) : (currentPagePairIndex * 2);
            StageData outgoingStage = (currentBook != null && outgoingStageIndex >= 0 && outgoingStageIndex < currentBook.stages.Count)
                ? currentBook.stages[outgoingStageIndex]
                : null;

            yield return StartCoroutine(pageCurl.PlayCurlAnimation(isNext, sourcePageRect, outgoingStage, outgoingStageIndex + 1, () =>
            {
                // ページが反転した瞬間（半分の位置）にデータを更新する。
                if (isNext) currentPagePairIndex++;
                else currentPagePairIndex--;

                Debug.Log($"[StageSelectManager] onHalfway内でcurrentPagePairIndex更新 → {currentPagePairIndex}");
                UpdatePageUI();
                Debug.Log("[StageSelectManager] UpdatePageUI 完了");
            }));
        }
        else
        {
            // アニメーション演出がない場合は即座に切り替える。
            if (isNext) currentPagePairIndex++;
            else currentPagePairIndex--;

            UpdatePageUI();
        }

        Debug.Log($"[StageSelectManager] PageChangeRoutine 終了 currentPagePairIndex={currentPagePairIndex}");
    }

    private void OnSelectStage(StageData stage)
    {
        if (stage == null) return;

        Debug.Log($"ステージ読み込み: {stage.stageId}");
        if (!string.IsNullOrEmpty(stage.sceneToLoad))
        {
            SceneManager.LoadScene(stage.sceneToLoad);
        }
    }
}