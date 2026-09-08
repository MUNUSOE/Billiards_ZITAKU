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

        // 打ち出し待ちは他の条件より先に処理する（演出の完了を待つため）。
        if (HasMode(page, TutorialAdvanceMode.Shot))
        {
            HandleShotAdvance();
            if (waitingForShotToFinish) return;
        }

        // Choice はボタンのクリック待ちなので、ここでは何もしない（OnChoiceSelected から進む）。

        bool advanceRequested = false;

        if (HasMode(page, TutorialAdvanceMode.LeftClick) && IsLeftClickPressed()) advanceRequested = true;
        if (HasMode(page, TutorialAdvanceMode.SpaceKey) && IsSpacePressed()) advanceRequested = true;

        if (advanceRequested) Advance();
    }

    /// <summary>ページに指定の条件が含まれているか。</summary>
    private static bool HasMode(TutorialPage page, TutorialAdvanceMode mode)
    {
        return (page.advanceMode & mode) != 0;
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

                // 打ち直しを防ぐため、演出が終わるまで打ち出しを禁止する。
                TutorialInputGate.Apply(false, false, false, false,
                    TutorialInputGate.UseForcedAim, TutorialInputGate.ForcedDirection,
                    TutorialInputGate.ForcedPowerLevel);
            }
            return;
        }

        // 球がすべて止まる（＝操作可能に戻る）まで待つ。
        if (shotBall != null && !shotBall.IsOperable) return;

        // 演出が終わったので自動で次のページへ進む。
        waitingForShotToFinish = false;
        Advance();
    }

    /// <summary>次のページへ進みます。最後まで進んだ場合は終了処理を行います。</summary>
    public void Advance()
    {
        TutorialPage page = GetCurrentPage();

        // SceneChange が指定されているページは、次のページではなくシーンへ遷移する。
        if (page != null && HasMode(page, TutorialAdvanceMode.SceneChange))
        {
            LoadNextScene(page);
            return;
        }

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
        bool allowDirection = false;
        bool allowPower = false;
        bool allowShot = false;
        const bool allowMagic = false; // チュートリアルでは魔法を扱わない

        switch (page.inputMode)
        {
            case TutorialInputMode.None:
                break;

            case TutorialInputMode.DirectionOnly:
                allowDirection = true;
                break;

            case TutorialInputMode.DirectionAndPower:
                allowDirection = true;
                allowPower = true;
                break;

            case TutorialInputMode.AllExceptMagic:
                allowDirection = true;
                allowPower = true;
                allowShot = true;
                break;

            case TutorialInputMode.FixedShotOnly:
                allowShot = true;
                break;
        }

        // FixedShotOnly は方向・威力の指定を常に使う（従来どおりの挙動）。
        bool useDirection = page.useForcedDirection || page.inputMode == TutorialInputMode.FixedShotOnly;
        bool usePower = page.useForcedPower || page.inputMode == TutorialInputMode.FixedShotOnly;

        // 指定があればショット球へ反映する。
        // 変更が許可されているモードでは「初期値」として働き、そこから操作できます。
        if (shotBall != null)
        {
            if (useDirection) shotBall.SetAimDirection(page.forcedDirection);
            if (usePower) shotBall.SetPowerLevel(page.forcedPowerLevel);
        }

        // 変更が禁止されている場合だけ、その値に固定し続ける。
        bool lockDirection = useDirection && !allowDirection;
        int lockPowerLevel = (usePower && !allowPower) ? page.forcedPowerLevel : -1;

        TutorialInputGate.Apply(allowDirection, allowPower, allowShot, allowMagic,
            lockDirection, page.forcedDirection.normalized, lockPowerLevel);
    }

    private void BindChoiceButtons(TutorialPage page)
    {
        if (!HasMode(page, TutorialAdvanceMode.Choice)) return;
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