using UnityEngine;
using System;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Stage Settings")]
    [SerializeField] private int maxMoves = 5; // ステージの初期手数
    private int currentMoves;
    private bool gameOverTriggered;

    [Header("UI Settings")]
    public GameObject GameOverUI;
    [SerializeField] private float gameOverDelay = 1.0f; // ★ ゲームオーバー表示までの遅延時間（秒）

    // 手数変更時の通知イベント（引数: 残り手数）
    public event Action<int> OnMovesChanged;
    // 手数が0になった時・落ちた時のイベント
    public event Action OnGameOver;

    private bool gameOverPending; // ゲームオーバー表示待機中（まだ確定していない）

    public int CurrentMoves => currentMoves;
    public bool IsGameOver => gameOverTriggered;

    private void Awake()
    {
        if (Instance == null)
        {
            // オプション画面を開いたままのシーン再読み込み・開始後も、ゲーム開始時は必ず通常速度に戻す。
            Time.timeScale = 1f;
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        currentMoves = maxMoves;
    }

    private void Start()
    {
        // UI等の初期状態更新用に発火
        OnMovesChanged?.Invoke(currentMoves);
        if (GameOverUI != null) GameOverUI.SetActive(false);
    }

    /// <summary>
    /// 手数を1つ消費する（移動完了時に呼ぶ）
    /// </summary>
    public void ConsumeMove()
    {
        if (currentMoves <= 0) return;

        currentMoves--;
        Debug.Log($"[GameManager] 手数を消費しました。残り: {currentMoves}");

        OnMovesChanged?.Invoke(currentMoves);

        if (currentMoves <= 0)
        {
            TriggerGameOver();
        }
    }

    /// <summary>
    /// ★ ゲームオーバー処理を実行する（1秒遅延）
    /// </summary>
    public void TriggerGameOver()
    {
        if (gameOverTriggered || gameOverPending) return;

        gameOverPending = true;
        StartCoroutine(GameOverRoutine());
    }

    /// <summary>
    /// 指定秒数待機してからゲームオーバーUIを表示するコルーチン。
    /// [変更] 待機している間、毎フレーム「全ターゲットクリア済みか」を確認し続ける。
    /// ターゲット消滅の演出などで判定が多少遅れても、この待機時間内にクリアが成立すれば
    /// ゲームオーバーの表示自体を中止し、クリアを優先する。
    /// </summary>
    private IEnumerator GameOverRoutine()
    {
        Debug.Log($"ゲームオーバー判定発生。{gameOverDelay}秒後に表示します（その間もクリア成立を監視）。");

        float elapsed = 0f;
        while (elapsed < gameOverDelay)
        {
            if (GameClear.Instance != null && GameClear.Instance.AreAllTargetsCleared())
            {
                Debug.Log("[GameManager] 表示待機中に全ターゲットクリアを検知。ゲームオーバー表示を中止し、クリアを優先します。");
                gameOverPending = false;
                yield break;
            }

            yield return null;
            elapsed += Time.deltaTime;
        }

        // ここまで来て初めてゲームオーバーを確定する。
        gameOverTriggered = true;
        gameOverPending = false;
        OnGameOver?.Invoke();

        if (GameOverUI != null)
        {
            GameOverUI.SetActive(true);
            Debug.Log("ゲームオーバーUIを表示しました。");
        }
    }

    /// <summary>
    /// ステージ変更時などに手数を再設定する
    /// </summary>
    public void SetMaxMoves(int moves)
    {
        maxMoves = moves;
        currentMoves = moves;
        gameOverTriggered = false;
        gameOverPending = false;
        OnMovesChanged?.Invoke(currentMoves);
    }
}