using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Inspector で登録した全ターゲット球が消滅したとき、クリアUIを表示する。
/// GameManager がゲームオーバー状態に入った後は、クリア表示を行わない。
/// </summary>
public class GameClear : MonoBehaviour
{
    public static GameClear Instance { get; private set; }

    [Header("Clear Settings")]
    [Tooltip("クリア対象のターゲット球をすべて登録する。登録順は判定に影響しない。")]
    [SerializeField] private List<GameObject> targetObjects = new List<GameObject>();

    [Tooltip("クリア時に表示するUI。開始時は非表示にする。")]
    [SerializeField] private GameObject ClearUI;

    [Tooltip("全ターゲット球が消滅してからクリアUIを表示するまでの秒数。")]
    [SerializeField, Min(0f)] private float clearDelay = 0.5f;

    [Tooltip("クリアUIのフェードインにかかる秒数。")]
    [SerializeField, Min(0f)] private float fadeInDuration = 0.5f;

    [Header("Result UI (Optional)")]
    [Tooltip("クリア画面に表示する獲得した星のUI")]
    [SerializeField] private StarRatingView starRatingView;

    [Header("Star Condition Texts")]
    [Tooltip("左から順番に、星1・星2・星3の下に配置するテキストを登録します (TextMeshPro用)")]
    [SerializeField] private TMP_Text[] conditionTextsTMP = new TMP_Text[3];

    [Tooltip("左から順番に、星1・星2・星3の下に配置するテキストを登録します (レガシーText用)")]
    [SerializeField] private Text[] conditionTextsLegacy = new Text[3];

    private bool clearTriggered;
    private bool clearPending;
    private bool finalizing;
    private ShotBall shotBall;
    private bool hadShotBallAtStart;
    private CanvasGroup clearCanvasGroup;

    /// <summary>クリアが確定済み、または確定待ちの状態か。</summary>
    public bool IsClearPendingOrTriggered => clearPending || clearTriggered;

    private void Awake()
    {
        Instance = this;

        if (ClearUI != null)
        {
            clearCanvasGroup = ClearUI.GetComponent<CanvasGroup>();
            if (clearCanvasGroup == null)
            {
                clearCanvasGroup = ClearUI.AddComponent<CanvasGroup>();
            }
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        if (ClearUI != null)
        {
            if (clearCanvasGroup != null)
            {
                clearCanvasGroup.alpha = 0f;
            }
            ClearUI.SetActive(false);
        }

        if (targetObjects == null || targetObjects.Count == 0)
        {
            Debug.LogWarning("[GameClear] targetObjects が未設定のため、クリア判定を開始しません。");
            enabled = false;
            return;
        }

        shotBall = FindObjectOfType<ShotBall>();
        hadShotBallAtStart = shotBall != null;
        StartCoroutine(WatchTargetsRoutine());
    }

    private IEnumerator WatchTargetsRoutine()
    {
        while (!clearTriggered)
        {
            if (GameManager.Instance != null && GameManager.Instance.HasLostBallToHazard)
            {
                yield break;
            }

            if (AreAllTargetsDestroyed())
            {
                OpenClearUI();
                yield break;
            }

            if (GameManager.Instance != null && GameManager.Instance.IsGameOver)
            {
                yield break;
            }

            yield return null;
        }
    }

    private EndingKind RecordStageResult()
    {
        if (StageInfo.Instance == null) return EndingKind.None;
        if (GameManager.Instance == null) return EndingKind.None;

        string stageId = StageInfo.Instance.StageId;
        int movesUsed = GameManager.Instance.MovesUsed;

        StageResult.RecordClear(stageId, movesUsed);

        int stars = StageInfo.Instance.StarCount;
        Debug.Log($"[GameClear] {stageId} 獲得星={stars} / 3"
                + $"（最速手={StageInfo.Instance.ParMoves} 星2つの条件={StageInfo.Instance.TwoStarMoves}）");
        return EndingFlow.RecordClear(StageInfo.Instance, movesUsed);
    }

    private bool AreAllTargetsDestroyed()
    {
        return targetObjects.TrueForAll(target => target == null);
    }

    public void OpenClearUI()
    {
        if (clearTriggered || finalizing) return;
        clearPending = true;
        finalizing = true;
        StartCoroutine(FinalizeClearRoutine());
    }

    private IEnumerator FinalizeClearRoutine()
    {
        // 球の消滅だけでは、最後のショットの手数消費が完了したとは限らない。
        bool hadShotBall = hadShotBallAtStart;
        while ((shotBall != null && shotBall.IsShotSequenceRunning) || Pocket.IsAnyBallBeingPocketed)
        {
            if ((GameManager.Instance != null && GameManager.Instance.HasLostBallToHazard)
                || (hadShotBall && shotBall == null))
            {
                clearPending = false;
                finalizing = false;
                yield break;
            }
            yield return null;
        }
        yield return null;
        if (clearDelay > 0f) yield return new WaitForSecondsRealtime(clearDelay);
        if ((hadShotBall && shotBall == null)
            || (GameManager.Instance != null && GameManager.Instance.HasLostBallToHazard)
            || !AreAllTargetsDestroyed())
        {
            clearPending = false;
            finalizing = false;
            yield break;
        }
        ShowClearUI();
    }

    private void ShowClearUI()
    {
        if (clearTriggered) return;
        clearTriggered = true;

        EndingKind ending = RecordStageResult();
        if (ending != EndingKind.None && EndingFlow.TryResumePendingEnding()) return;

        if (ClearUI == null)
        {
            Debug.LogWarning("[GameClear] Clear UI が未設定です。");
            return;
        }

        // 3つの星それぞれの条件を個別のテキストに書き込む
        if (StageInfo.Instance != null)
        {
            if (starRatingView != null)
            {
                starRatingView.gameObject.SetActive(true);
                starRatingView.SetStarCount(StageInfo.Instance.StarCount);
            }

            string[] conditionStrings = new string[3]
            {
                "クリア",
                $"{StageInfo.Instance.TwoStarMoves}手以内",
                $"{StageInfo.Instance.ParMoves}手以内"
            };

            for (int i = 0; i < 3; i++)
            {
                if (conditionTextsTMP != null && i < conditionTextsTMP.Length && conditionTextsTMP[i] != null)
                {
                    conditionTextsTMP[i].text = conditionStrings[i];
                }

                if (conditionTextsLegacy != null && i < conditionTextsLegacy.Length && conditionTextsLegacy[i] != null)
                {
                    conditionTextsLegacy[i].text = conditionStrings[i];
                }
            }
        }

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySE(SEType.DecideButton);
        }

        StartCoroutine(FadeInClearUIRoutine());
    }

    private IEnumerator FadeInClearUIRoutine()
    {
        ClearUI.SetActive(true);

        if (clearCanvasGroup != null)
        {
            clearCanvasGroup.alpha = 0f;
            float elapsedTime = 0f;

            // フェードイン完了後に Time.timeScale を 0 にするため Realtime を使用
            while (elapsedTime < fadeInDuration)
            {
                elapsedTime += Time.unscaledDeltaTime;
                clearCanvasGroup.alpha = Mathf.Clamp01(elapsedTime / fadeInDuration);
                yield return null;
            }

            clearCanvasGroup.alpha = 1f;
        }

        Time.timeScale = 0f;
    }
}