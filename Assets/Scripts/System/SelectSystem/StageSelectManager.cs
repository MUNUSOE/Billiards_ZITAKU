using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Text.RegularExpressions;

/// <summary>
/// ステージセレクト全体を管理する。
/// 本棚を廃止し、すべての章（本）のステージを連結して1冊の本として扱います。
/// </summary>
public class StageSelectManager : MonoBehaviour
{
    [Header("UI Panels")]
    [Tooltip("不要になった本棚UI（非表示にします）")]
    [SerializeField] private GameObject bookSelectionPanel;
    [Tooltip("見開き本全体の親パネル")]
    [SerializeField] private GameObject bookViewPanel;

    [Header("Bookmarks (Tabs)")]
    [Tooltip("右ページの上などに配置する栞（タブ）ボタンのリスト。")]
    [SerializeField] private List<Button> bookmarkButtons;

    [Header("Opened Book UI (Left Page)")]
    [SerializeField] private RectTransform leftPageRect;
    [SerializeField] private TMP_Text leftPageTitleText;
    [SerializeField] private TMP_Text leftPageNumberText;
    [SerializeField] private Button leftPlayButton;
    [SerializeField] private StarRatingView leftStarRating;
    [SerializeField] private Image leftStageImage;
    [SerializeField] private Text leftFastestClearText;

    [Header("Opened Book UI (Right Page)")]
    [SerializeField] private RectTransform rightPageRect;
    [SerializeField] private TMP_Text rightPageTitleText;
    [SerializeField] private TMP_Text rightPageNumberText;
    [SerializeField] private Button rightPlayButton;
    [SerializeField] private StarRatingView rightStarRating;
    [SerializeField] private Image rightStageImage;
    [SerializeField] private Text rightFastestClearText;

    [Header("Book Controls")]
    [SerializeField] private Button nextBookPageButton;
    [SerializeField] private Button prevBookPageButton;

    [Header("Data")]
    [Tooltip("このリストの中身がそのままゲームに反映されます。")]
    [SerializeField] private List<BookData> booksData = new List<BookData>();

    // ▼ 新しいページ管理システム ▼
    private class PagePair
    {
        public BookData Book;
        public int BookIndex;
        public int LeftStageIndex;
        public int RightStageIndex;
    }

    private List<PagePair> allPagePairs = new List<PagePair>();
    private int currentPairIndex = 0;

    // ▼ アニメーション状態の管理 ▼
    private bool isAnimating = false;
    private float turnDuration = 0.4f; // めくるスピード（秒）

    private void Start()
    {
        Time.timeScale = 1f;

        BuildPagePairs();
        SetupBookmarks();

        // 過去の「本を選んで下さい」UIが残っていれば確実に消す
        if (bookSelectionPanel != null) bookSelectionPanel.SetActive(false);
        if (bookViewPanel != null) bookViewPanel.SetActive(true);

        if (nextBookPageButton != null) nextBookPageButton.onClick.AddListener(() => OnClickPageChange(true));
        if (prevBookPageButton != null) prevBookPageButton.onClick.AddListener(() => OnClickPageChange(false));

        currentPairIndex = 0;
        UpdatePageUI();
    }

    private void BuildPagePairs()
    {
        allPagePairs.Clear();

        for (int b = 0; b < booksData.Count; b++)
        {
            BookData book = booksData[b];
            int stageCount = book.stages.Count;

            for (int s = 0; s < stageCount; s += 2)
            {
                PagePair pair = new PagePair
                {
                    Book = book,
                    BookIndex = b,
                    LeftStageIndex = s,
                    RightStageIndex = (s + 1 < stageCount) ? (s + 1) : -1
                };
                allPagePairs.Add(pair);
            }
        }
    }

    private void SetupBookmarks()
    {
        for (int i = 0; i < bookmarkButtons.Count; i++)
        {
            if (bookmarkButtons[i] != null)
            {
                int bookIndex = i;
                bookmarkButtons[i].onClick.AddListener(() => JumpToChapter(bookIndex));
            }
        }
    }

    public void JumpToChapter(int targetBookIndex)
    {
        if (isAnimating) return;

        for (int i = 0; i < allPagePairs.Count; i++)
        {
            if (allPagePairs[i].BookIndex == targetBookIndex)
            {
                currentPairIndex = i;
                UpdatePageUI();

                if (SoundManager.Instance != null) SoundManager.Instance.PlaySE(SEType.DecideButton);
                break;
            }
        }
    }

    private void UpdatePageUI()
    {
        if (allPagePairs.Count == 0) return;

        PagePair currentPair = allPagePairs[currentPairIndex];

        UpdateSinglePage(currentPair.LeftStageIndex, currentPair.Book, leftPageTitleText, leftPageNumberText, leftPlayButton, leftStarRating, leftStageImage, leftFastestClearText);
        UpdateSinglePage(currentPair.RightStageIndex, currentPair.Book, rightPageTitleText, rightPageNumberText, rightPlayButton, rightStarRating, rightStageImage, rightFastestClearText);

        if (prevBookPageButton != null) prevBookPageButton.interactable = (currentPairIndex > 0);
        if (nextBookPageButton != null) nextBookPageButton.interactable = (currentPairIndex < allPagePairs.Count - 1);
    }

    private void UpdateSinglePage(int stageIndex, BookData book, TMP_Text titleText, TMP_Text pageNumText, Button playBtn, StarRatingView starRating, Image stageImage, Text fastestClearText)
    {
        bool hasStage = book != null && stageIndex >= 0 && stageIndex < book.stages.Count;

        if (hasStage)
        {
            StageData stage = book.stages[stageIndex];

            if (titleText != null) { titleText.gameObject.SetActive(true); titleText.text = stage.stageName; }
            if (pageNumText != null) { pageNumText.gameObject.SetActive(true); pageNumText.text = $"- {stage.stageName} -"; }

            if (playBtn != null)
            {
                playBtn.gameObject.SetActive(true);
                playBtn.interactable = stage.isUnlocked;
                playBtn.onClick.RemoveAllListeners();
                playBtn.onClick.AddListener(() => OnSelectStage(stage));
            }

            if (starRating != null)
            {
                // ★追加: ステージデータで星を表示する設定のときだけ Active にする
                if (stage.showStarRating)
                {
                    starRating.gameObject.SetActive(true);
                    int stars = StageResult.GetStarCount(stage.stageId, stage.parMoves, stage.twoStarMoves);
                    starRating.SetStarCount(stars);
                }
                else
                {
                    starRating.gameObject.SetActive(false);
                }
            }

            if (stageImage != null)
            {
                stageImage.gameObject.SetActive(true);
                stageImage.enabled = true;
                if (stage.stageImage != null) { stageImage.sprite = stage.stageImage; stageImage.color = Color.white; }
                else { stageImage.sprite = null; }
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
            if (stageImage != null) stageImage.gameObject.SetActive(false);
            if (fastestClearText != null) fastestClearText.gameObject.SetActive(false);
        }
    }

    private void OnClickPageChange(bool isNext)
    {
        if (isAnimating) return;
        StartCoroutine(PageChangeRoutine(isNext));
    }

    private void SetPivotPreservingPosition(RectTransform rectTransform, Vector2 newPivot)
    {
        if (rectTransform == null) return;
        Vector2 size = rectTransform.rect.size;
        Vector2 deltaPivot = rectTransform.pivot - newPivot;
        Vector2 deltaPosition = new Vector2(deltaPivot.x * size.x, deltaPivot.y * size.y);
        rectTransform.pivot = newPivot;
        rectTransform.anchoredPosition -= deltaPosition;
    }

    private IEnumerator PageChangeRoutine(bool isNext)
    {
        isAnimating = true;
        float halfDuration = turnDuration / 2f;

        RectTransform outgoingPage = isNext ? rightPageRect : leftPageRect;
        Vector2 originalOutPivot = outgoingPage.pivot;

        SetPivotPreservingPosition(outgoingPage, isNext ? new Vector2(0f, 0.5f) : new Vector2(1f, 0.5f));

        float elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / halfDuration);
            float angle = Mathf.Lerp(0f, isNext ? 90f : -90f, t);
            outgoingPage.localRotation = Quaternion.Euler(0f, angle, 0f);
            yield return null;
        }
        outgoingPage.localRotation = Quaternion.Euler(0f, isNext ? 90f : -90f, 0f);

        if (isNext) currentPairIndex++;
        else currentPairIndex--;
        UpdatePageUI();

        outgoingPage.localRotation = Quaternion.identity;
        SetPivotPreservingPosition(outgoingPage, originalOutPivot);

        RectTransform incomingPage = isNext ? leftPageRect : rightPageRect;
        Vector2 originalInPivot = incomingPage.pivot;

        SetPivotPreservingPosition(incomingPage, isNext ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f));
        incomingPage.localRotation = Quaternion.Euler(0f, isNext ? -90f : 90f, 0f);

        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / halfDuration);
            float angle = Mathf.Lerp(isNext ? -90f : 90f, 0f, t);
            incomingPage.localRotation = Quaternion.Euler(0f, angle, 0f);
            yield return null;
        }
        incomingPage.localRotation = Quaternion.identity;
        SetPivotPreservingPosition(incomingPage, originalInPivot);

        isAnimating = false;
    }

    private void OnSelectStage(StageData stage)
    {
        if (stage == null) return;
        if (!string.IsNullOrEmpty(stage.sceneToLoad))
        {
            SceneManager.LoadScene(stage.sceneToLoad);
        }
    }

    // =========================================================
    // ▼ エディタ機能: Build Profileの登録シーンから自動生成する ▼
    // =========================================================
#if UNITY_EDITOR
    [ContextMenu("★ Build Profileからステージデータを自動生成する")]
    private void GenerateDataFromBuildProfile()
    {
        booksData.Clear();
        Dictionary<int, List<string>> chapterStages = new Dictionary<int, List<string>>();
        string tutorialSceneName = null;

        foreach (var scene in UnityEditor.EditorBuildSettings.scenes)
        {
            if (!scene.enabled) continue; 
            string sceneName = System.IO.Path.GetFileNameWithoutExtension(scene.path);

            if (sceneName.Contains("Tutorial") && sceneName.Contains("A"))
            {
                tutorialSceneName = sceneName;
                continue;
            }

            Match match = Regex.Match(sceneName, @"^(\d+)-(\d+)$");
            if (match.Success)
            {
                int chapterNum = int.Parse(match.Groups[1].Value); 
                if (!chapterStages.ContainsKey(chapterNum)) chapterStages[chapterNum] = new List<string>();
                chapterStages[chapterNum].Add(sceneName);
            }
        }

        int bookIdCounter = 0;
        if (!string.IsNullOrEmpty(tutorialSceneName))
        {
            BookData tutBook = new BookData { bookId = bookIdCounter++, bookTitle = "チュートリアル" };
            // ★ チュートリアルはデフォルトで星非表示 (showStarRating = false) に設定します
            tutBook.stages.Add(new StageData { stageId = tutorialSceneName, stageName = "チュートリアル", sceneToLoad = tutorialSceneName, isUnlocked = true, parMoves = 3, twoStarMoves = 5, starCount = 0, showStarRating = false });
            booksData.Add(tutBook);
        }

        List<int> sortedChapters = new List<int>(chapterStages.Keys);
        sortedChapters.Sort();

        foreach (int chapter in sortedChapters)
        {
            BookData book = new BookData { bookId = bookIdCounter++, bookTitle = $"{chapter} の章" };
            chapterStages[chapter].Sort((a, b) => { return int.Parse(a.Split('-')[1]).CompareTo(int.Parse(b.Split('-')[1])); });

            foreach (string stageName in chapterStages[chapter])
            {
                // ★ 通常ステージは星表示あり (showStarRating = true)
                book.stages.Add(new StageData { stageId = stageName, stageName = stageName, sceneToLoad = stageName, isUnlocked = true, parMoves = 3, twoStarMoves = 5, starCount = 0, showStarRating = true });
            }
            booksData.Add(book);
        }

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"【成功】Build Profileから {booksData.Count} 冊分のデータを抽出・自動生成しました！");
    }
#endif
}