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

    private bool ballLostToHazard; // 炎マスなどで球を失ったか（この場合クリアにはできない）

    public int CurrentMoves => currentMoves;
    public bool IsGameOver => gameOverTriggered;

    /// <summary>炎マスなどで球を失っている場合 true。クリア判定を抑制するために使う。</summary>
    public bool HasLostBallToHazard => ballLostToHazard;

    /// <summary>
    /// 炎マスで球が焼失したことを通知します。
    /// 焼失した球も盤面から消えるため、これを記録しておかないと
    /// 「全ターゲットが消えた＝クリア」と誤判定されてしまいます。
    /// </summary>
    public void NotifyBallLostToHazard()
    {
        ballLostToHazard = true;
        Debug.Log("[GameManager] 球が炎で焼失しました。以降クリア判定は成立しません。");
    }

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
        if (gameOverTriggered) return;

        gameOverTriggered = true;
        StartCoroutine(GameOverRoutine());
    }

    /// <summary>
    /// 指定秒数待機してからゲームオーバーUIを表示するコルーチン
    /// </summary>
    private IEnumerator GameOverRoutine()
    {
        Debug.Log($"ゲームオーバー判定発生。{gameOverDelay}秒後に表示します。");
        OnGameOver?.Invoke();

        // 指定した秒数（1秒）待機
        yield return new WaitForSeconds(gameOverDelay);

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
        ballLostToHazard = false;
        OnMovesChanged?.Invoke(currentMoves);
    }
}