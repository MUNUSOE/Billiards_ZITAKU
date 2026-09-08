using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

    private bool clearTriggered;

    // 全ターゲットが消えてから、クリアUIが出るまでの待機中を表します。
    // この間にショットされると手数が減ってゲームオーバーになってしまうため、
    // ShotBall 側はこのフラグが立っている間、操作を受け付けません。
    private bool clearPending;

    /// <summary>クリアが確定済み、または確定待ちの状態か。</summary>
    public bool IsClearPendingOrTriggered => clearPending || clearTriggered;

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
        if (ClearUI != null)
        {
            ClearUI.SetActive(false);
        }

        // 監視対象が未設定のステージを自動クリアしない。
        if (targetObjects == null || targetObjects.Count == 0)
        {
            Debug.LogWarning("[GameClear] targetObjects が未設定のため、クリア判定を開始しません。");
            enabled = false;
            return;
        }

        StartCoroutine(WatchTargetsRoutine());
    }

    private IEnumerator WatchTargetsRoutine()
    {
        while (!clearTriggered)
        {
            // 炎マスで球を失っている場合、球が盤面から消えていてもクリアではない。
            if (GameManager.Instance != null && GameManager.Instance.HasLostBallToHazard)
            {
                yield break;
            }

            // クリア成立の確認をゲームオーバー判定より先に行う。
            // GameManager.IsGameOver は TriggerGameOver を呼んだ瞬間に true になるため、
            // 先にゲームオーバーを見てしまうと、最後の球を落とすのと同時に手数が0になった場合に
            // クリアを取りこぼしてしまう。
            if (AreAllTargetsDestroyed())
            {
                // 待機に入る前にフラグを立て、この間の追加ショットを止める。
                clearPending = true;

                yield return new WaitForSeconds(clearDelay);

                // ここでは IsGameOver を見ない。クリアが成立している場合は
                // GameManager 側が表示待機中にゲームオーバーを取り消す。
                if (GameManager.Instance == null || !GameManager.Instance.HasLostBallToHazard)
                {
                    OpenClearUI();
                }

                yield break;
            }

            // ターゲットが残ったままのゲームオーバーは確定なので監視を終える。
            if (GameManager.Instance != null && GameManager.Instance.IsGameOver)
            {
                yield break;
            }

            yield return null;
        }
    }

    /// <summary>
    /// クリア記録（クリア済みフラグ・自己ベスト手数）を保存します。
    /// ステージの識別と最速手は、シーンに置いた StageInfo から取得します。
    /// StageInfo が無いシーン（チュートリアルなど）では何もしません。
    /// </summary>
    private void RecordStageResult()
    {
        if (StageInfo.Instance == null) return;
        if (GameManager.Instance == null) return;

        string stageId = StageInfo.Instance.StageId;
        int movesUsed = GameManager.Instance.MovesUsed;

        StageResult.RecordClear(stageId, movesUsed);

        if (StageInfo.Instance.IsFastestAchieved)
        {
            Debug.Log($"[GameClear] {stageId} は最速手（{StageInfo.Instance.ParMoves}手以内）を達成しています。");
        }
    }

    private bool AreAllTargetsDestroyed()
    {
        return targetObjects.TrueForAll(target => target == null);
    }

    /// <summary>
    /// クリアUIを表示し、ゲーム時間を停止する。複数回呼ばれても一度だけ実行する。
    /// </summary>
    public void OpenClearUI()
    {
        if (clearTriggered)
        {
            return;
        }

        clearTriggered = true;

        RecordStageResult();

        if (ClearUI == null)
        {
            Debug.LogWarning("[GameClear] Clear UI が未設定です。");
            return;
        }

        Time.timeScale = 0f;
        ClearUI.SetActive(true);

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySE(SEType.DecideButton);
        }
    }
}