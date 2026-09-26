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
    [Tooltip("左から順に6個以上登録。先頭6個を栞の表示枠として再利用します。On ClickはこのManagerが管理します。")]
    [SerializeField] private List<Button> bookmarkButtons;

    [Tooltip("最初の栞ページの章数。0なら自動（通常4個、0章ありなら5個）。Tも1個として数えます。")]
    [SerializeField, Range(0, 5)] private int firstBookmarkPageSize = 0;
    [SerializeField] private string previousBookmarkLabel = "<";
    [SerializeField] private string nextBookmarkLabel = ">";

    private const int BookmarkSlotCount = 6;
    private const int LaterBookmarkPageSize = 4;
    private readonly List<List<int>> bookmarkPages = new List<List<int>>();
    private int currentBookmarkPage;
    private bool bookmarksReady;

    [Header("Opened Book UI (Left Page)")]
    [SerializeField] private RectTransform leftPageRect;
    [SerializeField] private Text leftPageTitleText; // ★TMP_TextからレガシーTextに変更
    [SerializeField] private Button leftPlayButton;
    [SerializeField] private StarRatingView leftStarRating;
    [SerializeField] private Image leftStageImage;
    [SerializeField] private Text leftFastestClearText;

    [Header("Opened Book UI (Right Page)")]
    [SerializeField] private RectTransform rightPageRect;
    [SerializeField] private Text rightPageTitleText; // ★TMP_TextからレガシーTextに変更
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
            if (book == null || book.stages == null) continue;
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
        bookmarkPages.Clear();
        bookmarksReady = false;
        currentBookmarkPage = 0;
        if (bookmarkButtons == null || bookmarkButtons.Count < BookmarkSlotCount)
        {
            Debug.LogError("[StageSelect] Bookmark Buttonsに左から順に6個のButtonを登録してください。", this);
            return;
        }
        var unique = new HashSet<Button>();
        for (int i = 0; i < BookmarkSlotCount; i++)
        {
            if (bookmarkButtons[i] == null || !unique.Add(bookmarkButtons[i]))
            {
                Debug.LogError("[StageSelect] 栞の先頭6枠に未設定または重複があります。", this);
                return;
            }
        }
        // 空の章は栞に出さない。本の実インデックスを保持する。
        var chapterIndices = new List<int>();
        bool hasZeroChapter = false;
        for (int i = 0; i < booksData.Count; i++)
        {
            var book = booksData[i];
            if (book == null || book.stages == null || book.stages.Count == 0) continue;
            chapterIndices.Add(i);
            if (GetBookmarkLabel(i) == "0") hasZeroChapter = true;
        }
        int firstSize = firstBookmarkPageSize == 0
            ? (hasZeroChapter ? 5 : 4) : Mathf.Clamp(firstBookmarkPageSize, 1, 5);
        for (int offset = 0; offset < chapterIndices.Count;)
        {
            int size = bookmarkPages.Count == 0 ? firstSize : LaterBookmarkPageSize;
            int count = Mathf.Min(size, chapterIndices.Count - offset);
            bookmarkPages.Add(chapterIndices.GetRange(offset, count));
            offset += count;
        }
        // Inspectorに残った旧JumpToChapterイベントも二重発火させない。
        for (int i = 0; i < bookmarkButtons.Count; i++)
        {
            var button = bookmarkButtons[i];
            if (button == null) continue;
            if (i >= BookmarkSlotCount && unique.Contains(button)) continue;
            button.onClick = new Button.ButtonClickedEvent();
            button.gameObject.SetActive(false);
        }
        bookmarksReady = true;
        RefreshBookmarks();
    }

    private string GetBookmarkLabel(int bookIndex)
    {
        var book = booksData[bookIndex];
        string title = book.bookTitle ?? "";
        if (title.Contains("チュートリアル")
            || title.IndexOf("Tutorial", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return "T";
        foreach (var stage in book.stages)
        {
            if (stage == null) continue;
            string scene = stage.sceneToLoad ?? "";
            if (scene.IndexOf("Tutorial", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return "T";
            var match = Regex.Match(scene, @"^(\d+)-\d+$");
            if (match.Success) return match.Groups[1].Value;
        }
        return string.IsNullOrWhiteSpace(title) ? (bookIndex + 1).ToString() : title;
    }

    private void RefreshBookmarks()
    {
        if (!bookmarksReady) return;
        for (int i = 0; i < BookmarkSlotCount; i++)
        {
            bookmarkButtons[i].onClick = new Button.ButtonClickedEvent();
            bookmarkButtons[i].gameObject.SetActive(false);
        }
        if (bookmarkPages.Count == 0) return;
        currentBookmarkPage = Mathf.Clamp(currentBookmarkPage, 0, bookmarkPages.Count - 1);
        int slot = 0;
        if (currentBookmarkPage > 0)
            BindBookmark(slot++, previousBookmarkLabel, () => ChangeBookmarkPage(-1));
        foreach (int index in bookmarkPages[currentBookmarkPage])
        {
            int bookIndex = index;
            BindBookmark(slot++, GetBookmarkLabel(bookIndex), () => JumpToChapter(bookIndex));
        }
        if (currentBookmarkPage + 1 < bookmarkPages.Count)
            BindBookmark(slot, nextBookmarkLabel, () => ChangeBookmarkPage(1));
    }

    private void BindBookmark(int slot, string label, UnityEngine.Events.UnityAction action)
    {
        var button = bookmarkButtons[slot];
        var tmp = button.GetComponentInChildren<TMP_Text>(true);
        var legacy = button.GetComponentInChildren<Text>(true);
        if (tmp != null) tmp.text = label;
        else if (legacy != null) legacy.text = label;
        else Debug.LogWarning("[StageSelect] 栞にTMP_TextまたはTextを追加してください: " + button.name, button);
        button.interactable = true;
        button.onClick.AddListener(action);
        button.gameObject.SetActive(true);
    }

    private void ChangeBookmarkPage(int direction)
    {
        if (!bookmarksReady || isAnimating) return;
        int target = currentBookmarkPage + direction;
        if (target < 0 || target >= bookmarkPages.Count) return;
        currentBookmarkPage = target;
        RefreshBookmarks(); // 開いている本のページは変えない。
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySE(SEType.DecideButton);
    }

    private void SyncBookmarksToChapter(int bookIndex)
    {
        if (!bookmarksReady) return;
        for (int i = 0; i < bookmarkPages.Count; i++)
        {
            if (!bookmarkPages[i].Contains(bookIndex)) continue;
            if (currentBookmarkPage != i)
            {
                currentBookmarkPage = i;
                RefreshBookmarks();
            }
            return;
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
        SyncBookmarksToChapter(currentPair.BookIndex);

        UpdateSinglePage(currentPair.LeftStageIndex, currentPair.Book, leftPageTitleText, leftPlayButton, leftStarRating, leftStageImage, leftFastestClearText);
        UpdateSinglePage(currentPair.RightStageIndex, currentPair.Book, rightPageTitleText, rightPlayButton, rightStarRating, rightStageImage, rightFastestClearText);

        if (prevBookPageButton != null) prevBookPageButton.interactable = (currentPairIndex > 0);
        if (nextBookPageButton != null) nextBookPageButton.interactable = (currentPairIndex < allPagePairs.Count - 1);
    }

    private void UpdateSinglePage(int stageIndex, BookData book, Text titleText, Button playBtn, StarRatingView starRating, Image stageImage, Text fastestClearText)
    {
        bool hasStage = book != null && stageIndex >= 0 && stageIndex < book.stages.Count;

        if (hasStage)
        {
            StageData stage = book.stages[stageIndex];

            if (titleText != null) { titleText.gameObject.SetActive(true); titleText.text = stage.stageName; }

            if (playBtn != null)
            {
                playBtn.gameObject.SetActive(true);
                playBtn.interactable = EndingFlow.CanSelectStage(stage.sceneToLoad, stage.isUnlocked);
                playBtn.onClick.RemoveAllListeners();
                playBtn.onClick.AddListener(() => OnSelectStage(stage));
            }

            if (starRating != null)
            {
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
        if (stage == null || !EndingFlow.CanSelectStage(stage.sceneToLoad, stage.isUnlocked)) return;
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