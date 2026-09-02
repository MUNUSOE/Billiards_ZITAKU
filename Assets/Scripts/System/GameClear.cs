using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Inspector で登録した全ターゲット球が消滅したとき、クリアUIを表示する。
/// GameManager がゲームオーバー状態に入った後は、クリア表示を行わない。
/// </summary>
public class GameClear : MonoBehaviour
{
    [Header("Clear Settings")]
    [Tooltip("クリア対象のターゲット球をすべて登録する。登録順は判定に影響しない。")]
    [SerializeField] private List<GameObject> targetObjects = new List<GameObject>();

    [Tooltip("クリア時に表示するUI。開始時は非表示にする。")]
    [SerializeField] private GameObject ClearUI;

    [Tooltip("全ターゲット球が消滅してからクリアUIを表示するまでの秒数。")]
    [SerializeField, Min(0f)] private float clearDelay = 0.5f;

    private bool clearTriggered;

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

            // 炎マスで球を失っている場合、球が盤面から消えていてもクリアではない。
            if (GameManager.Instance != null && GameManager.Instance.HasLostBallToHazard)
            {
                yield break;
            }

            if (AreAllTargetsDestroyed())
            {
                yield return new WaitForSeconds(clearDelay);

                if (GameManager.Instance == null ||
                    (!GameManager.Instance.IsGameOver && !GameManager.Instance.HasLostBallToHazard))
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