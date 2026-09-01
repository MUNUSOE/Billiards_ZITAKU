using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Inspector で登録した全ターゲット球が消滅したとき、クリアUIを表示する。
/// GameManager がゲームオーバー状態に入った後は、クリア表示を行わない。
/// </summary>
public class GameClear : MonoBehaviour
{
    // [追加] GameManager 側から「手数0と同時に全ターゲットが消えているか」を
    // フレームのポーリングを待たず同期的に確認できるようにするための参照。
    public static GameClear Instance { get; private set; }

    [Header("Clear Settings")]
    [Tooltip("クリア対象のターゲット球をすべて登録する。登録順は判定に影響しない。")]
    [SerializeField] private List<GameObject> targetObjects = new List<GameObject>();

    [Tooltip("クリア時に表示するUI。開始時は非表示にする。")]
    [SerializeField] private GameObject ClearUI;

    [Tooltip("全ターゲット球が消滅してからクリアUIを表示するまでの秒数。")]
    [SerializeField, Min(0f)] private float clearDelay = 0.5f;

    private bool clearTriggered;

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
            // 主ボールのポケット、炎マス、手数切れによるゲームオーバーを優先する。
            if (GameManager.Instance != null && GameManager.Instance.IsGameOver)
            {
                yield break;
            }

            if (AreAllTargetsDestroyed())
            {
                yield return new WaitForSeconds(clearDelay);

                if (GameManager.Instance == null || !GameManager.Instance.IsGameOver)
                {
                    OpenClearUI();
                }

                yield break;
            }

            yield return null;
        }
    }

    private bool AreAllTargetsDestroyed()
    {
        return targetObjects.TrueForAll(target => target == null);
    }

    /// <summary>
    /// [追加] 全ターゲット球が既に消滅済みかどうかを外部（GameManager）から同期的に確認するための公開メソッド。
    /// Destroy() されたオブジェクトは呼び出し直後から == null になるため、
    /// このチェックは WatchTargetsRoutine のフレーム経過を待たずに正しい結果を返す。
    /// </summary>
    public bool AreAllTargetsCleared()
    {
        return AreAllTargetsDestroyed();
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