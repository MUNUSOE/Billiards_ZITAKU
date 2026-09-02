using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 水魔法のショット後効果です。
/// ショット球を中心として8方向の直線上にあり、障害物で遮られていない球を中心の周囲1マスへ引き寄せます。
/// </summary>
public static class WaterMagic
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

    /// <summary>
    /// ショット球の停止位置を中心に、8方向の最も手前にある対象球を周囲1マスへ引き寄せます。
    /// 壁、三角壁、木箱、マス間壁、未消火の炎マスは遮蔽物として扱います。
    /// </summary>
    public static IEnumerator ApplyPull(GameObject centerBall)
    {
        if (centerBall == null) yield break;

        BallPath.GetBallSettings(centerBall, out float panelSize, out _, out _, out _);
        Vector3 centerCell = BallPath.SnapToGrid(centerBall.transform.position, panelSize);
        List<WaterMove> moves = new List<WaterMove>();
        HashSet<GameObject> reservedBalls = new HashSet<GameObject>();
        HashSet<Vector3> reservedCells = new HashSet<Vector3>();

        // 全ての移動先を先に確定し、移動中の球による判定ぶれを防ぎます。
        foreach (Vector3 direction in Directions)
        {
            GameObject target = FindFirstVisibleBall(centerBall, centerCell, direction, panelSize);
            if (target == null || reservedBalls.Contains(target)) continue;

            Vector3 destination = centerCell + direction * panelSize;
            if (BallPath.IsWaterPullDestinationBlocked(destination, target, reservedCells, panelSize)) continue;

            moves.Add(new WaterMove(target, destination));
            reservedBalls.Add(target);
            reservedCells.Add(destination);
        }

        // 瞬間移動ではなく、確定した経路を1球ずつスライドさせます。
        // 魔法の効果は炎マスを遮蔽物として無視しますが、経路上の炎に触れた球は焼失します。
        foreach (WaterMove move in moves)
        {
            // ★ オブジェクトが既に破棄（Destroy）されていないか事前にチェック
            if (move.ball == null) continue;

            Vector3 fromCell = BallPath.SnapToGrid(move.ball.transform.position, panelSize);
            Vector3 moveDirection = BallPath.Get8Direction((move.destination - fromCell).normalized);

            // 炎に触れる場合は、その位置で移動を打ち切る（炎マスを通り過ぎてから止まらないようにする）。
            bool burns = BallPath.TryGetMagicPathBurnCell(
                fromCell, move.destination, moveDirection, panelSize, out Vector3 stopCell);

            Debug.Log($"[WaterMagic] 移動判定 球={move.ball.name} 現在={fromCell} 目的地={move.destination} "
                    + $"方向={moveDirection} 炎接触={burns} 停止位置={stopCell}");

            // 炎への接触点で止める場合はマスの中間座標になりうるため、グリッド吸着を無効にする。
            yield return MagicBallSlide.SlideTo(move.ball, stopCell, snapToGrid: !burns);

            // ★ スライド処理中に対象球が消滅（Destroy）しているか判定
            if (move.ball == null)
            {
                Debug.Log($"[WaterMagic] スライド中に球が消失しました（炎接触または破棄）");
                if (burns)
                {
                    for (int i = 0; i < GameOverDelayFrames; i++) yield return null;
                    if (GameManager.Instance != null) GameManager.Instance.TriggerGameOver();
                    yield break;
                }
                continue;
            }

            Debug.Log($"[WaterMagic] スライド完了 球={move.ball.name} 実際の位置={move.ball.transform.position}");

            if (burns)
            {
                Debug.Log($"[WaterMagic] 引き寄せた球が炎マスに触れたためゲームオーバー: {stopCell}");

                // 停止した瞬間にゲームオーバーへ移ると唐突なので、少し間を置く。
                for (int i = 0; i < GameOverDelayFrames; i++) yield return null;

                if (GameManager.Instance != null) GameManager.Instance.TriggerGameOver();
                yield break;
            }
        }
    }

    private struct WaterMove
    {
        public GameObject ball;
        public Vector3 destination;

        public WaterMove(GameObject ball, Vector3 destination)
        {
            this.ball = ball;
            this.destination = destination;
        }
    }

    private static GameObject FindFirstVisibleBall(GameObject centerBall, Vector3 centerCell, Vector3 direction, float panelSize)
    {
        for (int distance = 1; distance <= 100; distance++)
        {
            Vector3 cell = centerCell + direction * panelSize * distance;

            // 終点が三角壁マスでも、そのマスにいる対象球自体は探索対象に含める。
            // ただし、後段で対象球が三角壁から実際に出られる方向かを必ず確認する。
            if (BallPath.IsWaterPullLineBlocked(centerCell, cell, direction, panelSize, allowTriangleAtEnd: true))
            {
                return null;
            }

            GameObject found = BallPath.FindBallAtGridCell(cell, centerBall, panelSize);
            if (found != null)
            {
                Vector3 targetCell = BallPath.SnapToGrid(found.transform.position, panelSize);
                Vector3 pullDirection = -direction;

                // 対象球から中心側への退出が、既存の三角壁規則で許される場合だけ引き寄せる。
                if (!BallPath.CanMagicBallLeaveTriangleWall(targetCell, pullDirection, panelSize))
                {
                    return null;
                }

                return found;
            }
        }

        return null;
    }
}