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
    /// <param name="centerBall">中心となる主ボール</param>
    /// <param name="effectPrefab">発動時に生成するエフェクトのプレハブ（省略可）</param>
    /// <param name="offsetY">エフェクト表示位置のY軸オフセット（省略可）</param>
    public static IEnumerator ApplyPull(GameObject centerBall, GameObject effectPrefab = null, float offsetY = 0f)
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

        // 引き寄せる対象が存在する場合、発動エフェクト生成・SE再生・1秒ディレイを実行
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
                SoundManager.Instance.PlaySE(SEType.WaterMagic);
            }

            yield return new WaitForSeconds(1f);
        }

        // 確定した移動先へ、対象の球を「同時に」スライドさせます。
        List<IEnumerator> slides = new List<IEnumerator>();
        bool anyBurned = false;
        Vector3 burnedAt = Vector3.zero;

        foreach (WaterMove move in moves)
        {
            if (move.ball == null) continue;

            Vector3 fromCell = BallPath.SnapToGrid(move.ball.transform.position, panelSize);
            Vector3 moveDirection = BallPath.Get8Direction((move.destination - fromCell).normalized);

            bool burns = BallPath.TryGetMagicPathBurnCell(
                fromCell, move.destination, moveDirection, panelSize, out Vector3 stopCell);

            Debug.Log($"[WaterMagic] 移動判定 球={move.ball.name} 現在={fromCell} 目的地={move.destination} "
                    + $"方向={moveDirection} 炎接触={burns} 停止位置={stopCell}");

            if (burns)
            {
                anyBurned = true;
                burnedAt = stopCell;
            }

            slides.Add(MagicBallSlide.SlideTo(move.ball, stopCell, snapToGrid: !burns));
        }

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
            Debug.Log($"[WaterMagic] 引き寄せた球が炎マスに触れたためゲームオーバー: {burnedAt}");

            for (int i = 0; i < GameOverDelayFrames; i++) yield return null;

            if (GameManager.Instance != null) GameManager.Instance.TriggerGameOver();
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

            if (BallPath.IsWaterPullLineBlocked(centerCell, cell, direction, panelSize, allowTriangleAtEnd: true))
            {
                return null;
            }

            GameObject found = BallPath.FindBallAtGridCell(cell, centerBall, panelSize);
            if (found != null)
            {
                Vector3 targetCell = BallPath.SnapToGrid(found.transform.position, panelSize);
                Vector3 pullDirection = -direction;

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