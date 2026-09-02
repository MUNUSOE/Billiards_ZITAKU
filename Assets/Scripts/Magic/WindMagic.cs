using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 風魔法のショット後効果です。
/// ショット球を中心に8方向・2マス以内にある可視なターゲット球を、
/// 同じ方向の3マス目へ放射状に吹き飛ばします。
/// </summary>
public static class WindMagic
{
    /// <summary>炎マスに触れて停止してから、ゲームオーバーへ移るまでの待機フレーム数。</summary>
    private const int GameOverDelayFrames = 10;

    private static readonly Vector3[] Directions =
    {
        new Vector3(-1f, 0f, -1f),
        new Vector3( 0f, 0f, -1f),
        new Vector3( 1f, 0f, -1f),
        new Vector3(-1f, 0f,  0f),
        new Vector3( 1f, 0f,  0f),
        new Vector3(-1f, 0f,  1f),
        new Vector3( 0f, 0f,  1f),
        new Vector3( 1f, 0f,  1f),
    };

    // Directions と同じ並び順のラベル（ログ表示用）
    private static readonly string[] DirectionNames =
    {
        "左上", "上", "右上", "左", "右", "左下", "下", "右下",
    };

    private struct WindMove
    {
        public GameObject ball;
        public Vector3 destination;

        public WindMove(GameObject ball, Vector3 destination)
        {
            this.ball = ball;
            this.destination = destination;
        }
    }

    /// <summary>
    /// 全ての移動先を先に確定してから、対象球を順番にスライドさせます。
    /// 同一直線上に1マス目・2マス目の2球がある場合は、外側の2マス目の球だけを3マス目へ移動します。
    /// </summary>
    /// <param name="centerBall">中心となる主ボール</param>
    /// <param name="effectPrefab">発動時に生成するエフェクトのプレハブ（省略可）</param>
    /// <param name="offsetY">エフェクト表示位置のY軸オフセット（省略可）</param>
    public static IEnumerator ApplyPush(GameObject centerBall, GameObject effectPrefab = null, float offsetY = 0f)
    {
        if (centerBall == null) yield break;

        BallPath.GetBallSettings(centerBall, out float panelSize, out _, out _, out _);
        Vector3 centerCell = BallPath.SnapToGrid(centerBall.transform.position, panelSize);
        List<WindMove> moves = new List<WindMove>();
        HashSet<GameObject> reservedBalls = new HashSet<GameObject>();
        HashSet<Vector3> reservedCells = new HashSet<Vector3>();

        Debug.Log($"[WindMagic] === 発動開始 中心球={centerBall.name} 中心セル={centerCell} ===");

        for (int i = 0; i < Directions.Length; i++)
        {
            Vector3 direction = Directions[i];
            string dirName = DirectionNames[i];

            GameObject target = FindOutermostVisibleBall(centerBall, centerCell, direction, panelSize, dirName);
            if (target == null) continue;

            if (reservedBalls.Contains(target))
            {
                Debug.Log($"[WindMagic] {dirName}: 対象球 {target.name} は既に他方向の移動で予約済みのためスキップ");
                continue;
            }

            if (!TryResolveWindDestination(target, centerCell, direction, panelSize, dirName, out Vector3 destination, out bool stopsOnTriangleWall))
            {
                continue;
            }

            if (BallPath.IsMagicMoveDestinationBlocked(destination, target, reservedCells, panelSize, allowTriangleWall: stopsOnTriangleWall))
            {
                Debug.Log($"[WindMagic] {dirName}: 対象球 {target.name} の着地予定セル {destination} が塞がっているため不発");
                continue;
            }

            Debug.Log($"[WindMagic] {dirName}: 対象球 {target.name} を {destination} へ移動確定（三角壁停止={stopsOnTriangleWall}）");
            moves.Add(new WindMove(target, destination));
            reservedBalls.Add(target);
            reservedCells.Add(destination);
        }

        Debug.Log($"[WindMagic] === 判定終了。実際に動く球の数={moves.Count} ===");

        // 吹き飛ばす対象が存在する場合、発動エフェクト生成・SE再生・1秒ディレイを実行
        if (moves.Count > 0)
        {
            if (effectPrefab != null)
            {
                Vector3 effectPos = centerCell + new Vector3(0f, offsetY, 0f);
                GameObject effectInstance = Object.Instantiate(effectPrefab, effectPos, Quaternion.identity);
                Object.Destroy(effectInstance, 5f);
            }

            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlaySE(SEType.WindMagic);
            }

            yield return new WaitForSeconds(1f);
        }

        // 確定した移動先へ、対象の球を「同時に」スライドさせます。
        // 各球のコルーチンを1フレームずつ並行して進め、全員の完了を待ちます。
        // 魔法の効果は炎マスを遮蔽物として無視しますが、経路上の炎に触れた球は焼失します。
        List<IEnumerator> slides = new List<IEnumerator>();
        bool anyBurned = false;
        Vector3 burnedAt = Vector3.zero;

        foreach (WindMove move in moves)
        {
            // オブジェクトが既に破棄（Destroy）されていないか事前にチェック
            if (move.ball == null) continue;

            Vector3 fromCell = BallPath.SnapToGrid(move.ball.transform.position, panelSize);
            Vector3 moveDirection = BallPath.Get8Direction((move.destination - fromCell).normalized);

            // 炎に触れる場合は、その位置で移動を打ち切る（炎マスを通り過ぎてから止まらないようにする）。
            bool burns = BallPath.TryGetMagicPathBurnCell(
                fromCell, move.destination, moveDirection, panelSize, out Vector3 stopCell);

            Debug.Log($"[WindMagic] 移動判定 球={move.ball.name} 現在={fromCell} 目的地={move.destination} "
                    + $"方向={moveDirection} 炎接触={burns} 停止位置={stopCell}");

            if (burns)
            {
                anyBurned = true;
                burnedAt = stopCell;
            }

            // 炎への接触点で止める場合はマスの中間座標になりうるため、グリッド吸着を無効にする。
            // ここでは実行せずコルーチンを貯めておき、後でまとめて並行実行する。
            slides.Add(MagicBallSlide.SlideTo(move.ball, stopCell, snapToGrid: !burns));
        }

        // 全ての球を1フレームずつ並行して進め、最後の1つが終わるまで待つ。
        // MagicBallSlide.SlideTo は内部で球のnullチェックを行うため、
        // スライド中に球が破棄されてもここで例外にはならない。
        while (slides.Count > 0)
        {
            for (int i = slides.Count - 1; i >= 0; i--)
            {
                if (!slides[i].MoveNext()) slides.RemoveAt(i);
            }
            yield return null;
        }

        if (anyBurned)
        {
            Debug.Log($"[WindMagic] 吹き飛ばした球が炎マスに触れたためゲームオーバー: {burnedAt}");

            // 停止した瞬間にゲームオーバーへ移ると唐突なので、少し間を置く。
            for (int i = 0; i < GameOverDelayFrames; i++) yield return null;

            if (GameManager.Instance != null)
            {
                // 焼失をクリア判定から除外するため、先に通知する。
                GameManager.Instance.NotifyBallLostToHazard();
                GameManager.Instance.TriggerGameOver();
            }
        }
    }

    /// <summary>
    /// 風による移動先を決定します。
    /// </summary>
    private static bool TryResolveWindDestination(GameObject target, Vector3 centerCell, Vector3 direction, float panelSize, string dirName, out Vector3 destination, out bool stopsOnTriangleWall)
    {
        destination = centerCell + direction * panelSize * 3f;
        stopsOnTriangleWall = false;
        if (target == null) return false;

        Vector3 currentCell = BallPath.SnapToGrid(target.transform.position, panelSize);

        if (!BallPath.CanMagicBallLeaveTriangleWall(currentCell, direction, panelSize))
        {
            Debug.Log($"[WindMagic] {dirName}: 対象球 {target.name} は現在セル {currentCell} が三角壁で、その方向には出られないため不発");
            return false;
        }

        int startDistance = Mathf.RoundToInt(Mathf.Max(
            Mathf.Abs(currentCell.x - centerCell.x) / panelSize,
            Mathf.Abs(currentCell.z - centerCell.z) / panelSize));

        for (int distance = startDistance + 1; distance <= 3; distance++)
        {
            Vector3 nextCell = centerCell + direction * panelSize * distance;

            if (BallPath.CanWindStopOnTriangleWall(currentCell, nextCell, direction, panelSize))
            {
                destination = nextCell;
                stopsOnTriangleWall = true;
                Debug.Log($"[WindMagic] {dirName}: 対象球 {target.name} は{distance}マス目の三角壁({nextCell})で停止（成功扱い）");
                return true;
            }

            if (BallPath.IsWaterPullLineBlocked(currentCell, nextCell, direction, panelSize))
            {
                destination = currentCell;
                Debug.Log($"[WindMagic] {dirName}: 対象球 {target.name} は{distance}マス目手前で遮られたため、{currentCell}で停止");
                return true;
            }

            currentCell = nextCell;
        }

        return true;
    }

    /// <summary>
    /// 1〜2マス目を調べます。両方に球がある場合は、外側（2マス目）を返します。
    /// </summary>
    private static GameObject FindOutermostVisibleBall(GameObject centerBall, Vector3 centerCell, Vector3 direction, float panelSize, string dirName)
    {
        GameObject outermost = null;

        for (int distance = 1; distance <= 2; distance++)
        {
            Vector3 cell = centerCell + direction * panelSize * distance;

            if (BallPath.IsWaterPullLineBlocked(centerCell, cell, direction, panelSize, allowTriangleAtEnd: true))
            {
                Debug.Log($"[WindMagic] {dirName}: {distance}マス目（{cell}）から先の視線が遮られたため探索を打ち切り");
                break;
            }

            GameObject found = BallPath.FindBallAtGridCell(cell, centerBall, panelSize);
            if (found != null)
            {
                outermost = found;
            }
        }

        if (outermost == null)
        {
            Debug.Log($"[WindMagic] {dirName}: 1〜2マス以内に対象球なし");
        }

        return outermost;
    }
}