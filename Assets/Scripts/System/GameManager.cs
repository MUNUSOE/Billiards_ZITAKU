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

    [Header("Tutorial / Debug")]
    [Tooltip("ショットの予測（通過マス・停止マス・最初に当たる球の運動量）を表示します。チュートリアル用。")]
    [SerializeField] private bool showShotPreview = false;

    /// <summary>ショット予測表示が有効か。ステージごとに Inspector で切り替えます。</summary>
    public bool ShowShotPreview => showShotPreview;

    [Header("UI Settings")]
    public GameObject GameOverUI;
    [SerializeField] private float gameOverDelay = 1.0f; // ★ ゲームオーバー表示までの遅延時間（秒）

    // 手数変更時の通知イベント（引数: 残り手数）
    public event Action<int> OnMovesChanged;
    // 手数が0になった時・落ちた時のイベント
    public event Action OnGameOver;

    private bool ballLostToHazard; // 炎マスなどで球を失ったか（この場合クリアにはできない）

    public int CurrentMoves => currentMoves;

    /// <summary>このステージで使った手数。クリア記録の保存に使います。</summary>
    public int MovesUsed => Mathf.Max(0, maxMoves - currentMoves);

    /// <summary>このステージの初期手数。</summary>
    public int MaxMoves => maxMoves;
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
        Debug.Log($"ゲームオーバー判定発生。{gameOverDelay}秒後に表示します（その間もクリア成立を監視）。");

        // クリアは GameClear 側で clearDelay 待ってから確定するため、
        // 最後の球を落とすのと同時に手数が0になると、先にゲームオーバーが出てしまう。
        // 待機中も毎フレーム確認し、クリアが成立したら表示を中止する。
        float elapsed = 0f;
        while (elapsed < gameOverDelay)
        {
            if (IsClearWinning())
            {
                Debug.Log("[GameManager] クリア成立を検知したため、ゲームオーバー表示を中止します。");
                gameOverTriggered = false;
                yield break;
            }

            yield return null;
            elapsed += Time.deltaTime;
        }

        if (IsClearWinning())
        {
            Debug.Log("[GameManager] クリア成立を検知したため、ゲームオーバー表示を中止します。");
            gameOverTriggered = false;
            yield break;
        }

        OnGameOver?.Invoke();

        if (GameOverUI != null)
        {
            GameOverUI.SetActive(true);
            Debug.Log("ゲームオーバーUIを表示しました。");
        }
    }

    /// <summary>
    /// クリアが成立している（または確定待ち）ためゲームオーバーにすべきでないか。
    /// 炎で球を失っている場合はクリアではないので false を返します。
    /// </summary>
    private bool IsClearWinning()
    {
        if (ballLostToHazard) return false;
        if (GameClear.Instance == null) return false;

        return GameClear.Instance.IsClearPendingOrTriggered;
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