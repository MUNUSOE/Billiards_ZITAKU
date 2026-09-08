using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// チュートリアルの進行を管理します。
/// Inspector に並べた TutorialPage を順番に表示し、ページごとに操作の許可範囲を切り替えます。
///
/// このスクリプトはチュートリアル専用です。通常のステージには置きません。
/// 球の移動や運動量の計算は既存の BallPath / ShotBall をそのまま使います。
/// </summary>
public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    [Header("Pages")]
    [Tooltip("チュートリアルのページを順番に並べます。")]
    [SerializeField] private List<TutorialPage> pages = new List<TutorialPage>();

    [Header("References")]
    [Tooltip("チュートリアルで操作するショット球。")]
    [SerializeField] private ShotBall shotBall;

    [Header("Settings")]
    [Tooltip("ページが切り替わった直後、この秒数だけ入力を受け付けません。クリックの二重反応を防ぎます。")]
    [SerializeField] private float inputCooldown = 0.15f;

    [Tooltip("最後のページを終えたときにチュートリアル完了として記録します。")]
    [SerializeField] private bool markCompletedAtEnd = true;

    private int currentIndex = -1;
    private float cooldownTimer;
    private bool lastChoiceWasCorrect;
    private bool waitingForShotToFinish;

    /// <summary>直前の選択肢が正解だったか。結果表示ページで参照します。</summary>
    public bool LastChoiceWasCorrect => lastChoiceWasCorrect;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;

        // チュートリアルのシーンを抜けるときは、必ず操作制限を解除する。
        TutorialInputGate.Clear();
    }

    private void Start()
    {
        // すべてのページの表示物をいったん隠す。
        foreach (TutorialPage page in pages)
        {
            if (page == null) continue;
            if (page.displayObject != null) page.displayObject.SetActive(false);
            if (page.correctResultObject != null) page.correctResultObject.SetActive(false);
            if (page.wrongResultObject != null) page.wrongResultObject.SetActive(false);
        }

        GoToPage(0);
    }

    private void Update()
    {
        if (cooldownTimer > 0f)
        {
            cooldownTimer -= Time.unscaledDeltaTime;
            return;
        }

        TutorialPage page = GetCurrentPage();
        if (page == null) return;

        switch (page.advanceMode)
        {
            case TutorialAdvanceMode.LeftClick:
                if (IsLeftClickPressed()) Advance();
                break;

            case TutorialAdvanceMode.SpaceKey:
                if (IsSpacePressed()) Advance();
                break;

            case TutorialAdvanceMode.Shot:
                HandleShotAdvance();
                break;

            case TutorialAdvanceMode.Choice:
                // ボタンのクリック待ち。OnChoiceSelected から進みます。
                break;

            case TutorialAdvanceMode.SceneChange:
                if (IsLeftClickPressed()) LoadNextScene(page);
                break;
        }
    }

    /// <summary>
    /// 打ち出しページの進行です。
    /// 打ち出しを検知したあと、球が止まって操作可能になるまで待ってから次へ進みます。
    /// </summary>
    private void HandleShotAdvance()
    {
        if (!waitingForShotToFinish)
        {
            if (TutorialInputGate.ConsumeShotFired())
            {
                waitingForShotToFinish = true;
            }
            return;
        }

        // 球がすべて止まったら次のページへ。
        if (shotBall != null && !shotBall.IsOperable) return;

        waitingForShotToFinish = false;
        Advance();
    }

    /// <summary>次のページへ進みます。最後まで進んだ場合は終了処理を行います。</summary>
    public void Advance()
    {
        GoToPage(currentIndex + 1);
    }

    /// <summary>指定した番号のページを表示します。</summary>
    public void GoToPage(int index)
    {
        // 今のページの表示物を隠す。
        TutorialPage previous = GetCurrentPage();
        if (previous != null)
        {
            if (previous.displayObject != null) previous.displayObject.SetActive(false);
            if (previous.correctResultObject != null) previous.correctResultObject.SetActive(false);
            if (previous.wrongResultObject != null) previous.wrongResultObject.SetActive(false);
            UnbindChoiceButtons(previous);
        }

        currentIndex = index;

        if (currentIndex < 0 || currentIndex >= pages.Count)
        {
            FinishTutorial();
            return;
        }

        TutorialPage page = pages[currentIndex];
        if (page == null)
        {
            Advance();
            return;
        }

        if (page.displayObject != null) page.displayObject.SetActive(true);

        // 選択結果の表示があれば、直前の選択に応じて出し分ける。
        if (page.correctResultObject != null) page.correctResultObject.SetActive(lastChoiceWasCorrect);
        if (page.wrongResultObject != null) page.wrongResultObject.SetActive(!lastChoiceWasCorrect);

        ApplyInputMode(page);
        BindChoiceButtons(page);

        waitingForShotToFinish = false;
        cooldownTimer = inputCooldown;
    }

    /// <summary>ページの設定にあわせて操作の許可範囲を切り替えます。</summary>
    private void ApplyInputMode(TutorialPage page)
    {
        switch (page.inputMode)
        {
            case TutorialInputMode.None:
                TutorialInputGate.Apply(false, false, false, false, false, Vector3.zero, -1);
                break;

            case TutorialInputMode.DirectionOnly:
                TutorialInputGate.Apply(true, false, false, false, false, Vector3.zero, -1);
                break;

            case TutorialInputMode.DirectionAndPower:
                TutorialInputGate.Apply(true, true, false, false, false, Vector3.zero, -1);
                break;

            case TutorialInputMode.AllExceptMagic:
                TutorialInputGate.Apply(true, true, true, false, false, Vector3.zero, -1);
                break;

            case TutorialInputMode.FixedShotOnly:
                TutorialInputGate.Apply(false, false, true, false,
                    true, page.forcedDirection.normalized, page.forcedPowerLevel);
                break;
        }
    }

    private void BindChoiceButtons(TutorialPage page)
    {
        if (page.advanceMode != TutorialAdvanceMode.Choice) return;
        if (page.choiceButtons == null) return;

        for (int i = 0; i < page.choiceButtons.Length; i++)
        {
            if (page.choiceButtons[i] == null) continue;

            int choiceIndex = i; // クロージャ対策でローカルに退避
            page.choiceButtons[i].onClick.RemoveAllListeners();
            page.choiceButtons[i].onClick.AddListener(() => OnChoiceSelected(choiceIndex));
        }
    }

    private void UnbindChoiceButtons(TutorialPage page)
    {
        if (page.choiceButtons == null) return;

        foreach (var button in page.choiceButtons)
        {
            if (button != null) button.onClick.RemoveAllListeners();
        }
    }

    /// <summary>選択肢が押されたときの処理です。正誤を記録して次のページへ進みます。</summary>
    public void OnChoiceSelected(int choiceIndex)
    {
        TutorialPage page = GetCurrentPage();
        if (page == null) return;

        lastChoiceWasCorrect = (choiceIndex == page.correctChoiceIndex);
        Debug.Log($"[Tutorial] 選択肢 {choiceIndex} が選ばれました。正解={lastChoiceWasCorrect}");

        Advance();
    }

    private void LoadNextScene(TutorialPage page)
    {
        if (string.IsNullOrEmpty(page.nextSceneName))
        {
            Debug.LogWarning("[Tutorial] 遷移先のシーン名が設定されていません。");
            return;
        }

        TutorialInputGate.Clear();
        Time.timeScale = 1f;
        SceneManager.LoadScene(page.nextSceneName);
    }

    private void FinishTutorial()
    {
        TutorialInputGate.Clear();

        if (markCompletedAtEnd)
        {
            TutorialProgress.MarkCompleted();
        }

        Debug.Log("[Tutorial] チュートリアルの全ページが終了しました。");
    }

    private TutorialPage GetCurrentPage()
    {
        if (currentIndex < 0 || currentIndex >= pages.Count) return null;
        return pages[currentIndex];
    }

    private static bool IsLeftClickPressed()
    {
        Mouse mouse = Mouse.current;
        return mouse != null && mouse.leftButton.wasPressedThisFrame;
    }

    private static bool IsSpacePressed()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
    }
}
