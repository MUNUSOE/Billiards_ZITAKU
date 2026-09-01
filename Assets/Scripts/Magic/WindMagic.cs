using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 風魔法のショット後効果です。
/// ショット球を中心に8方向・2マス以内にある可視なターゲット球を、
/// 同じ方向の3マス目へ放射状に吹き飛ばします。
///
/// [DEBUG] 不発の原因を切り分けるため、各方向・各判定ステップで
/// Debug.Log を出すように計装しています。ロジック自体は元のコードから変更していません。
/// 問題が再現したら Console の "[WindMagic]" ログをそのまま貼ってください。
/// </summary>
public static class WindMagic
{
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
    public static IEnumerator ApplyPush(GameObject centerBall)
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
            if (target == null) continue; // 理由は FindOutermostVisibleBall 内でログ済み

            if (reservedBalls.Contains(target))
            {
                Debug.Log($"[WindMagic] {dirName}: 対象球 {target.name} は既に他方向の移動で予約済みのためスキップ");
                continue;
            }

            if (!TryResolveWindDestination(target, centerCell, direction, panelSize, dirName, out Vector3 destination, out bool stopsOnTriangleWall))
            {
                continue; // 理由は TryResolveWindDestination 内でログ済み
            }

            if (BallPath.IsMagicMoveDestinationBlocked(destination, target, reservedCells, panelSize, allowTriangleWall: stopsOnTriangleWall))
            {
                Debug.Log($"[WindMagic] {dirName}: 対象球 {target.name} の着地予定セル {destination} が塞がっているため不発（予約済み/他の球/炎マス/壁/木箱のいずれか。詳細はBallPath側のログ参照）");
                continue;
            }

            Debug.Log($"[WindMagic] {dirName}: 対象球 {target.name} を {destination} へ移動確定（三角壁停止={stopsOnTriangleWall}）");
            moves.Add(new WindMove(target, destination));
            reservedBalls.Add(target);
            reservedCells.Add(destination);
        }

        Debug.Log($"[WindMagic] === 判定終了。実際に動く球の数={moves.Count} ===");

        foreach (WindMove move in moves)
        {
            if (move.ball == null) continue;
            yield return MagicBallSlide.SlideTo(move.ball, move.destination);
        }
    }

    /// <summary>
    /// 風による移動先を決定します。進行中に進入可能な三角壁マスがあれば、
    /// そのマスを終点として停止し、反射・通過は行いません。
    /// </summary>
    private static bool TryResolveWindDestination(GameObject target, Vector3 centerCell, Vector3 direction, float panelSize, string dirName, out Vector3 destination, out bool stopsOnTriangleWall)
    {
        destination = centerCell + direction * panelSize * 3f;
        stopsOnTriangleWall = false;
        if (target == null) return false;

        Vector3 currentCell = BallPath.SnapToGrid(target.transform.position, panelSize);

        // 三角壁マス内にいる対象球も、既存の通常移動規則上で出られる方向だけ移動可能にする。
        if (!BallPath.CanMagicBallLeaveTriangleWall(currentCell, direction, panelSize))
        {
            Debug.Log($"[WindMagic] {dirName}: 対象球 {target.name} は現在セル {currentCell} が三角壁で、その方向には出られないため不発（詳細はBallPath側のログ参照）");
            return false;
        }

        int startDistance = Mathf.RoundToInt(Mathf.Max(
            Mathf.Abs(currentCell.x - centerCell.x) / panelSize,
            Mathf.Abs(currentCell.z - centerCell.z) / panelSize));

        for (int distance = startDistance + 1; distance <= 3; distance++)
        {
            Vector3 nextCell = centerCell + direction * panelSize * distance;

            // 三角壁へ入れる面から押されたときは、その三角壁マスで停止する。
            if (BallPath.CanWindStopOnTriangleWall(currentCell, nextCell, direction, panelSize))
            {
                destination = nextCell;
                stopsOnTriangleWall = true;
                Debug.Log($"[WindMagic] {dirName}: 対象球 {target.name} は{distance}マス目の三角壁({nextCell})で停止（成功扱い）");
                return true;
            }

            // 進入できない三角壁、通常壁、木箱、間壁、炎マスは風を遮る。
            // [変更] 3マス目まで届かない場合でも丸ごと不発にはせず、遮られる手前のマスで止める
            // （3マス以内で動かせる最大距離まで移動する）。
            if (BallPath.IsWaterPullLineBlocked(currentCell, nextCell, direction, panelSize))
            {
                destination = currentCell;
                Debug.Log($"[WindMagic] {dirName}: 対象球 {target.name} は{distance}マス目手前（{currentCell}→{nextCell}）で遮られたため、{currentCell}で停止（動けるところまで移動、{distance - startDistance - 1}マス分）");
                return true;
            }

            currentCell = nextCell;
        }

        return true;
    }

    /// <summary>
    /// 1～2マス目を調べます。両方に球がある場合は、外側（2マス目）を返します。
    /// 壁・三角壁・木箱・マス間壁・炎マスが間にあれば、その方向には効果を及ぼしません。
    /// </summary>
    private static GameObject FindOutermostVisibleBall(GameObject centerBall, Vector3 centerCell, Vector3 direction, float panelSize, string dirName)
    {
        GameObject outermost = null;

        for (int distance = 1; distance <= 2; distance++)
        {
            Vector3 cell = centerCell + direction * panelSize * distance;
            // 終点が三角壁マスでも、そこにいる対象球自体は探索対象に含める。
            // 実際に三角壁から出られるかは TryResolveWindDestination 側で確認する。
            if (BallPath.IsWaterPullLineBlocked(centerCell, cell, direction, panelSize, allowTriangleAtEnd: true))
            {
                // [BUGFIX] 以前はここで return null しており、1マス目で正しく見つかっていた
                // 対象球（例：三角壁マスにちょうど乗っている球）まで巻き添えで消えていた。
                // 2マス目の視線が塞がれても、それより手前で確定していた発見は保持し、
                // 単に「これ以上遠くは探さない」だけにする。
                Debug.Log($"[WindMagic] {dirName}: {distance}マス目（{cell}）から先の視線が遮られたため探索を打ち切り（{(outermost != null ? "手前で発見済みの対象は維持" : "対象なし")}）");
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