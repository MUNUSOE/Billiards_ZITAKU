using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public static class BallPath
{
    public class PathPoint
    {
        public Vector3 position;
        public bool isBallHit;
        public bool skipAnimation;
        // 最初のターゲット接触時に炎魔法を終了させるためのフラグです。
        public bool consumeFireOnHit;
        // 物理コライダーの状態に依存せず、主ボールのポケット到達を伝えるフラグです。
        public bool isPocket;
        // 未消火の炎マスに到達し、ゲームオーバーになる経路点です。
        public bool isHazard;
        // この経路点へ球が到達した時点で破壊する木箱です。
        // 経路計算の時点で消すとショットの瞬間に消えてしまうため、再生時まで遅延させます。
        public List<GameObject> burnablesToDestroy;
        // この経路点へ球が到達した時点で消火する炎マスです。
        public List<FlameTile> flamesToExtinguish;

        public PathPoint(Vector3 position, bool isBallHit = false, bool skipAnimation = false, bool consumeFireOnHit = false, bool isPocket = false, bool isHazard = false)
        {
            this.position = position;
            this.isBallHit = isBallHit;
            this.skipAnimation = skipAnimation;
            this.consumeFireOnHit = consumeFireOnHit;
            this.isPocket = isPocket;
            this.isHazard = isHazard;
        }

        /// <summary>この経路点に到達したときの破壊・消火をまとめて実行します。</summary>
        public void ApplyPendingEffects()
        {
            if (burnablesToDestroy != null)
            {
                foreach (GameObject burnable in burnablesToDestroy)
                {
                    if (burnable != null) Object.Destroy(burnable);
                }
                burnablesToDestroy = null;
            }

            if (flamesToExtinguish != null)
            {
                foreach (FlameTile tile in flamesToExtinguish)
                {
                    if (tile != null) tile.Extinguish();
                }
                flamesToExtinguish = null;
            }
        }
    }

    public class ChainStep
    {
        public GameObject ball;
        public List<PathPoint> path;
    }

    public class SimState
    {
        public HashSet<GameObject> movedBalls = new HashSet<GameObject>();
        // Object.Destroy はフレーム末に実行されるため、同一ショット内で破壊済みとして扱う木箱を保持します。
        public HashSet<GameObject> destroyedBurnables = new HashSet<GameObject>();
        public List<Vector3> occupiedCells = new List<Vector3>();

        public Dictionary<GameObject, Vector3> virtualBalls = new Dictionary<GameObject, Vector3>();

        public bool ShouldIgnore(GameObject obj)
        {
            return obj != null && movedBalls.Contains(obj);
        }

        public bool IsDestroyedBurnable(GameObject obj)
        {
            return destroyedBurnables.Contains(obj);
        }

        public bool IsCellOccupied(Vector3 pos)
        {
            foreach (var c in occupiedCells)
            {
                if (Vector3.Distance(c, pos) < 0.1f) return true;
            }
            return false;
        }
    }

    public static Vector3 SnapToGrid(Vector3 pos, float panelSize)
    {
        float x = Mathf.Floor(pos.x / panelSize + 0.5f) * panelSize;
        float z = Mathf.Floor(pos.z / panelSize + 0.5f) * panelSize;
        return new Vector3(x, pos.y, z);
    }

    public static Vector3 Get8Direction(Vector3 dir)
    {
        float x = dir.x; float z = dir.z;
        float sx = Mathf.Abs(x) < 0.3f ? 0f : (x > 0 ? 1f : -1f);
        float sz = Mathf.Abs(z) < 0.3f ? 0f : (z > 0 ? 1f : -1f);
        if (sx == 0f && sz == 0f)
        {
            if (Mathf.Abs(x) > Mathf.Abs(z)) sx = x > 0 ? 1f : -1f;
            else sz = z > 0 ? 1f : -1f;
        }
        return new Vector3(sx, 0f, sz).normalized;
    }

    private static Vector3 StepOffset(Vector3 dir, float panelSize)
    {
        float dx = Mathf.Abs(dir.x) > 0.1f ? Mathf.Sign(dir.x) : 0f;
        float dz = Mathf.Abs(dir.z) > 0.1f ? Mathf.Sign(dir.z) : 0f;
        return new Vector3(dx * panelSize, 0f, dz * panelSize);
    }

    public static void GetBallSettings(GameObject ball, out float panelSize, out float ballRadius, out float shotSpeed, out float shotDuration)
    {
        ShotBall shot = ball.GetComponent<ShotBall>();
        if (shot != null)
        {
            panelSize = shot.panelSize;
            ballRadius = shot.ballRadius;
            shotSpeed = shot.shotSpeed;
            shotDuration = shot.shotDuration;
            return;
        }

        TargetBall target = ball.GetComponent<TargetBall>();
        if (target != null)
        {
            panelSize = target.panelSize;
            ballRadius = target.ballRadius;
            shotSpeed = target.shotSpeed;
            shotDuration = target.shotDuration;
            return;
        }

        panelSize = 1f;
        ballRadius = 0.25f;
        shotSpeed = 5f;
        shotDuration = 1.5f;
    }

    private static bool OverlapHasTag(Vector3 point, float radius, string tag, GameObject self, SimState state, out GameObject found)
    {
        found = null;
        Collider[] cols = Physics.OverlapSphere(point, radius);
        foreach (var c in cols)
        {
            if (self != null && c.gameObject == self) continue;
            if (state != null && state.ShouldIgnore(c.gameObject)) continue;

            try
            {
                if (c.CompareTag(tag))
                {
                    found = c.gameObject;
                    return true;
                }
            }
            catch (UnityException)
            {
                // タグが未登録の場合の例外防止
            }
        }
        return false;
    }

    /// <summary>
    /// マス間壁が、指定セルから指定方向への移動を遮るかを確認します。
    /// 通常移動とターゲットの押し出し前判定で共用し、隣接ターゲットのすり抜けを防ぎます。
    /// </summary>
    private static bool TryGetInterCellWallBlock(Vector3 fromCell, Vector3 axisDir, float panelSize, out Vector3 normal)
    {
        normal = Vector3.zero;
        if (panelSize <= 0f) return false;

        Vector3 boundaryCenter = fromCell + axisDir.normalized * (panelSize * 0.5f);
        Collider[] hits = Physics.OverlapSphere(boundaryCenter, panelSize * 0.6f);
        foreach (Collider hit in hits)
        {
            if (hit == null) continue;

            InterCellWall wall = hit.GetComponentInParent<InterCellWall>();
            if (wall != null && wall.TryGetBlockingNormal(fromCell, axisDir, panelSize, out normal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 斜め移動でマス間壁の端点に触れる場合を検出します。
    /// 端点の角判定だけは入射方向全体を反転し、壁面への斜め入射は通常壁と同じ成分反射に任せます。
    /// </summary>
    private static bool TryGetDiagonalInterCellWallBlock(Vector3 fromCell, Vector3 diagonalDir, float panelSize, out Vector3 normal)
    {
        normal = Vector3.zero;
        if (Mathf.Abs(diagonalDir.x) < 0.1f || Mathf.Abs(diagonalDir.z) < 0.1f) return false;

        Vector3 boundaryCenter = fromCell + StepOffset(diagonalDir, panelSize).normalized * (panelSize * 0.5f);
        Collider[] hits = Physics.OverlapSphere(boundaryCenter, panelSize * 0.8f);
        foreach (Collider hit in hits)
        {
            if (hit == null) continue;

            InterCellWall wall = hit.GetComponentInParent<InterCellWall>();
            if (wall == null || !wall.BlocksDiagonalMove(fromCell, diagonalDir, panelSize)) continue;

            if (wall.IsCornerDiagonalCollision(fromCell, diagonalDir, panelSize))
            {
                // 壁の端点へ当たる場合は、木箱の角と同じく斜め方向全体を反転します。
                normal = -Get8Direction(diagonalDir);
                return true;
            }

            // 壁面へ斜めに入る場合は、壁の向きに応じた成分だけを反転します。
            normal = wall.WallOrientation == InterCellWall.Orientation.Vertical
                ? new Vector3(-Mathf.Sign(diagonalDir.x), 0f, 0f)
                : new Vector3(0f, 0f, -Mathf.Sign(diagonalDir.z));
            return true;
        }

        return false;
    }

    private static bool TryGetActiveFlameTile(Vector3 cell, float panelSize, out FlameTile flameTile)
    {
        flameTile = null;
        Collider[] hits = Physics.OverlapSphere(cell, panelSize * 0.4f);
        foreach (Collider hit in hits)
        {
            if (hit == null) continue;

            FlameTile tile = hit.GetComponentInParent<FlameTile>();
            if (tile != null && tile.IsActiveFlame)
            {
                flameTile = tile;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 1マスの移動で接触する未消火の炎マスを収集します。
    /// 木箱と同様に、斜め移動では進行先に加えてX/Z方向の横切りマスも判定します。
    /// </summary>
    private static bool TryGetActiveFlameTilesOnMove(Vector3 currentCell, Vector3 dir, float panelSize, out List<FlameTile> flameTiles)
    {
        flameTiles = new List<FlameTile>();
        float dx = Mathf.Abs(dir.x) > 0.1f ? Mathf.Sign(dir.x) : 0f;
        float dz = Mathf.Abs(dir.z) > 0.1f ? Mathf.Sign(dir.z) : 0f;
        if (dx == 0f && dz == 0f) return false;

        AddActiveFlameTile(currentCell + StepOffset(dir, panelSize), panelSize, flameTiles);

        if (dx != 0f && dz != 0f)
        {
            AddActiveFlameTile(currentCell + new Vector3(dx * panelSize, 0f, 0f), panelSize, flameTiles);
            AddActiveFlameTile(currentCell + new Vector3(0f, 0f, dz * panelSize), panelSize, flameTiles);
        }

        return flameTiles.Count > 0;
    }

    private static void AddActiveFlameTile(Vector3 cell, float panelSize, List<FlameTile> flameTiles)
    {
        if (!TryGetActiveFlameTile(cell, panelSize, out FlameTile tile)) return;
        if (!flameTiles.Contains(tile)) flameTiles.Add(tile);
    }

    /// <summary>
    /// 斜め移動で炎マスを横切ったときも、炎マスの中心へ曲げず本来の移動線上で停止する位置を返します。
    /// </summary>
    private static Vector3 GetFlameContactPoint(Vector3 currentCell, Vector3 dir, float panelSize, float ballRadius)
    {
        Vector3 move = StepOffset(dir, panelSize);
        if (move.sqrMagnitude < 0.0001f) return currentCell;

        // 斜め・直進を問わず、次マスへ向かう移動線の中央手前で止めます。
        float contactDistance = Mathf.Max(0f, panelSize * 0.5f - ballRadius);
        return currentCell + move.normalized * contactDistance;
    }

    private static bool HasTriangleWallAtCell(Vector3 cell, float panelSize)
    {
        Collider[] hits = Physics.OverlapSphere(cell, panelSize * 0.4f);
        foreach (Collider hit in hits)
        {
            if (hit != null && hit.GetComponentInParent<TriangleWall>() != null)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 風魔法が三角壁マスへ到達したとき、そのマスへ進入可能な方向かを確認します。
    /// 進入可能なら球は三角壁マスで停止し、反射・通過は行いません。
    /// </summary>
    public static bool CanWindStopOnTriangleWall(Vector3 fromCell, Vector3 triangleCell, Vector3 direction, float panelSize)
    {
        if (!HasTriangleWallAtCell(triangleCell, panelSize)) return false;

        // 直進入射で既存ルールが進入前反射とする面からは、風でも入れない。
        if (TryGetTriangleWallEntryReflection(triangleCell, direction, panelSize, out _)) return false;

        // 斜め入射でも既存ルールが三角壁の斜辺反射とする場合は、風による進入を許可しない。
        if (TryGetTriangleWallAdjacentDiagonalReflection(triangleCell, fromCell, direction, panelSize, out _)) return false;

        return true;
    }

    /// <summary>
    /// 三角壁マス内の球が、既存の通常移動規則上で指定方向へ出られるかを確認します。
    /// 水・風魔法は三角壁内で反射や通過を再現せず、この判定が true のときだけ直線移動します。
    /// </summary>
    public static bool CanMagicBallLeaveTriangleWall(Vector3 fromCell, Vector3 direction, float panelSize)
    {
        if (!HasTriangleWallAtCell(fromCell, panelSize)) return true;

        // 三角壁マス内で塗りつぶし側へ向かう通常移動は反射するため、魔法移動では許可しない。
        if (TryGetTriangleWallReflection(fromCell, direction, panelSize, out _))
        {
            Debug.Log($"[BallPath] CanMagicBallLeaveTriangleWall: {fromCell} は三角壁の塗りつぶし側方向のため反射扱い→出られない");
            return false;
        }

        // 出口側のマスとの境界も、通常移動と同じく越えられる必要がある。
        if (GetWallBlock(fromCell, direction, panelSize, out _, includeBurnable: true))
        {
            Debug.Log($"[BallPath] CanMagicBallLeaveTriangleWall: {fromCell} の出口側に通常壁/木箱があり出られない");
            return false;
        }

        Vector3 nextCell = fromCell + StepOffset(direction, panelSize);
        if (TryGetDiagonalSideTriangleReflection(fromCell, direction, panelSize, out _))
        {
            Debug.Log($"[BallPath] CanMagicBallLeaveTriangleWall: {fromCell} は斜め移動で三角壁の斜辺に当たるため出られない");
            return false;
        }
        if (TryGetTriangleWallAdjacentDiagonalReflection(nextCell, fromCell, direction, panelSize, out _))
        {
            Debug.Log($"[BallPath] CanMagicBallLeaveTriangleWall: 隣接セル{nextCell}の三角壁の斜め反射に阻まれ出られない");
            return false;
        }
        if (TryGetTriangleWallEntryReflection(nextCell, direction, panelSize, out _))
        {
            Debug.Log($"[BallPath] CanMagicBallLeaveTriangleWall: 隣接セル{nextCell}の三角壁の進入前反射に阻まれ出られない");
            return false;
        }
        // [変更] 魔法の効果では炎マスを遮蔽物として扱わない。

        return true;
    }

    /// <summary>
    /// 指定グリッドにある別のターゲット球を、物理コライダーではなくグリッド座標で取得します。
    /// スライド直後のCollider更新のタイミングや高さの微差に左右されず、魔法対象の検出を安定させます。
    /// </summary>
    public static GameObject FindBallAtGridCell(Vector3 cell, GameObject self, float panelSize)
    {
        GameObject[] candidates;
        try
        {
            candidates = GameObject.FindGameObjectsWithTag("Ball");
        }
        catch (UnityException)
        {
            return null;
        }

        foreach (GameObject candidate in candidates)
        {
            if (candidate == null) continue;

            // Colliderが子オブジェクトに付いている構成でも、実際に動かすTargetBall本体を取得する。
            TargetBall targetComponent = candidate.GetComponentInParent<TargetBall>();
            if (targetComponent == null) continue;

            GameObject targetBall = targetComponent.gameObject;
            if (targetBall == self) continue;

            Vector3 candidateCell = SnapToGrid(targetBall.transform.position, panelSize);
            if (Mathf.Abs(candidateCell.x - cell.x) < 0.1f &&
                Mathf.Abs(candidateCell.z - cell.z) < 0.1f)
            {
                return targetBall;
            }
        }

        return null;
    }

    /// <summary>
    /// 水魔法の直線上に、球を引き寄せられない障害物があるかを確認します。
    /// allowTriangleAtEnd が true の場合だけ、終点にある三角壁を対象球の配置マスとして扱います。
    /// 経路途中の三角壁は従来どおり遮蔽物です。
    /// </summary>
    public static bool IsWaterPullLineBlocked(Vector3 startCell, Vector3 endCell, Vector3 direction, float panelSize, bool allowTriangleAtEnd = false)
    {
        Vector3 step = StepOffset(direction, panelSize);
        if (step.sqrMagnitude < 0.0001f) return true;

        int steps = Mathf.RoundToInt(Mathf.Max(
            Mathf.Abs(endCell.x - startCell.x) / panelSize,
            Mathf.Abs(endCell.z - startCell.z) / panelSize));
        Vector3 currentCell = startCell;

        for (int i = 0; i < steps; i++)
        {
            Vector3 nextCell = currentCell + step;
            bool isEndCell = i == steps - 1;
            bool ignoreTerminalTriangle = allowTriangleAtEnd && isEndCell;

            if (GetWallBlock(currentCell, direction, panelSize, out _, includeBurnable: true))
            {
                Debug.Log($"[BallPath] IsWaterPullLineBlocked: {currentCell}→{nextCell} 間に通常壁/木箱があり遮断");
                return true;
            }
            if (TryGetDiagonalSideTriangleReflection(currentCell, direction, panelSize, out _))
            {
                Debug.Log($"[BallPath] IsWaterPullLineBlocked: {currentCell} で斜め移動が三角壁の斜辺に当たり遮断");
                return true;
            }
            if (!ignoreTerminalTriangle &&
                TryGetTriangleWallAdjacentDiagonalReflection(nextCell, currentCell, direction, panelSize, out _))
            {
                Debug.Log($"[BallPath] IsWaterPullLineBlocked: {nextCell} の三角壁の隣接斜め反射で遮断（終点免除対象外）");
                return true;
            }
            if (!ignoreTerminalTriangle && TryGetTriangleWallEntryReflection(nextCell, direction, panelSize, out _))
            {
                Debug.Log($"[BallPath] IsWaterPullLineBlocked: {nextCell} の三角壁の進入前反射で遮断（終点免除対象外）");
                return true;
            }
            // [変更] 三角壁のマスがあるというだけでは遮断しない。
            // そのマスをその進行方向で「通り抜けられるか」で判定する。
            // 進入時の反射（隣接斜め・進入前）は上で既に判定済みなので、
            // ここでは「マス内で塗りつぶし側へ向かって反射するか」だけを見る。
            // 反射しない角度＝そのまま素通りできる角度なので、遮断しない。
            if (!ignoreTerminalTriangle && HasTriangleWallAtCell(nextCell, panelSize) &&
                TryGetTriangleWallReflection(nextCell, direction, panelSize, out _))
            {
                Debug.Log($"[BallPath] IsWaterPullLineBlocked: {nextCell} の三角壁が進行方向{direction}を反射するため遮断");
                return true;
            }
            // [変更] 魔法の効果では炎マスを遮蔽物として扱わない（素通りさせる）。
            // 炎マスに触れた場合の焼失判定は、移動確定後に呼び出し側が行う。

            currentCell = nextCell;
        }

        return false;
    }

    /// <summary>
    /// 水魔法で球を引き寄せる周囲1マスが、安全に配置可能かを確認します。
    /// </summary>
    /// <summary>
    /// 魔法（水・風）で球を fromCell から toCell へ移動させたとき、最初に炎へ触れる位置を返します。
    /// 魔法の効果は炎マスを遮蔽物として無視しますが、触れた球は通常移動と同じく焼失するため、
    /// 呼び出し側はこの位置で移動を打ち切ってゲームオーバーにします。
    /// 斜め移動で横のマスの炎に触れる場合は、その手前のマス（触れる直前の位置）を停止位置とします。
    /// </summary>
    /// <param name="burnCell">炎に触れて停止する位置。斜めに角をかすめる場合はマスの中間座標になる。触れない場合は toCell。</param>
    /// <returns>経路上または終点で炎に触れる場合 true。</returns>
    public static bool TryGetMagicPathBurnCell(Vector3 fromCell, Vector3 toCell, Vector3 direction, float panelSize, out Vector3 burnCell)
    {
        burnCell = toCell;

        int steps = Mathf.RoundToInt(Mathf.Max(
            Mathf.Abs(toCell.x - fromCell.x),
            Mathf.Abs(toCell.z - fromCell.z)) / panelSize);

        Vector3 step = StepOffset(direction, panelSize);
        Vector3 currentCell = fromCell;

        for (int i = 0; i < steps; i++)
        {
            Vector3 nextCell = currentCell + step;

            Debug.Log($"[BallPath] 炎経路走査 {i}歩目 現在={currentCell} 次={nextCell} "
                + $"次が炎={TryGetActiveFlameTile(nextCell, panelSize, out _)} "
                + $"斜め横が炎={TryGetActiveFlameTilesOnMove(currentCell, direction, panelSize, out _)}");

            // 進行先のマス自体が炎なら、そのマスへ入った時点で焼失する。
            if (TryGetActiveFlameTile(nextCell, panelSize, out _))
            {
                burnCell = nextCell;
                return true;
            }

            // 斜めに横切る隣のマスが炎の場合、接触点は currentCell と nextCell のちょうど中間
            // （マスの角をかすめる位置）にある。currentCell を返すと球が動かず、
            // nextCell を返すと炎を通り過ぎてから止まって見えるため、中間位置で止める。
            if (TryGetActiveFlameTilesOnMove(currentCell, direction, panelSize, out _))
            {
                burnCell = Vector3.Lerp(currentCell, nextCell, 0.5f);
                return true;
            }

            currentCell = nextCell;
        }

        // 始点がすでに炎の上（移動距離0を含む）。
        if (TryGetActiveFlameTile(toCell, panelSize, out _))
        {
            burnCell = toCell;
            return true;
        }

        return false;
    }

    public static bool IsWaterPullDestinationBlocked(Vector3 destination, GameObject movingBall, HashSet<Vector3> reservedCells, float panelSize)
    {
        // 球と三角壁は同じマスに共存できるため、引き寄せ先が三角壁のマスでも塞がっているとは扱わない。
        // 対象球がそのマスへ入れる方向かどうかは、呼び出し側(WaterMagic)が
        // CanMagicBallLeaveTriangleWall / IsWaterPullLineBlocked で既に確認している。
        return IsMagicMoveDestinationBlocked(destination, movingBall, reservedCells, panelSize, allowTriangleWall: true);
    }

    /// <summary>
    /// 水・風魔法で球を移動させる先が、安全に配置可能かを確認します。
    /// </summary>
    public static bool IsMagicMoveDestinationBlocked(Vector3 destination, GameObject movingBall, HashSet<Vector3> reservedCells, float panelSize, bool allowTriangleWall = false)
    {
        if (reservedCells != null && reservedCells.Contains(destination))
        {
            Debug.Log($"[BallPath] IsMagicMoveDestinationBlocked: {destination} は同一発動内で他の球がすでに予約済み");
            return true;
        }
        if (FindBallAtGridCell(destination, movingBall, panelSize) != null)
        {
            Debug.Log($"[BallPath] IsMagicMoveDestinationBlocked: {destination} に既に別の球がいる");
            return true;
        }
        // [変更] 移動先が炎マスでも配置は許可する（遮蔽物として扱わない）。
        // その結果球が焼失するかどうかは、呼び出し側が HasActiveFlameOnMagicPath で判定する。
        if (OverlapHasTag(destination, panelSize * 0.3f, "Reflect", null, null, out GameObject reflectObject))
        {
            // 風で進入可能な三角壁マスを終点にする場合だけ、三角壁自身のReflectタグを許可する。
            if (!allowTriangleWall || reflectObject == null || reflectObject.GetComponentInParent<TriangleWall>() == null)
            {
                Debug.Log($"[BallPath] IsMagicMoveDestinationBlocked: {destination} にReflectタグの壁がある（三角壁停止許可={allowTriangleWall}）");
                return true;
            }
        }
        if (TryGetTaggedObjectAtGridCell(destination, panelSize, "Burnable", movingBall, null, out _))
        {
            Debug.Log($"[BallPath] IsMagicMoveDestinationBlocked: {destination} に木箱(Burnable)がある");
            return true;
        }
        if (!allowTriangleWall && HasTriangleWallAtCell(destination, panelSize))
        {
            Debug.Log($"[BallPath] IsMagicMoveDestinationBlocked: {destination} は三角壁マス（このケースでは停止許可なし）");
            return true;
        }

        return false;
    }

    private static GameObject GetBallAtCell(Vector3 cell, GameObject self, SimState state, float panelSize)
    {
        GameObject found = null;
        if (state != null)
        {
            foreach (var kvp in state.virtualBalls)
            {
                if (kvp.Key == self) continue;
                if (Vector3.Distance(kvp.Value, cell) < 0.1f)
                {
                    found = kvp.Key;
                    break;
                }
            }
        }
        if (found == self) found = null;

        if (found == null)
        {
            OverlapHasTag(cell, panelSize * 0.3f, "Ball", self, state, out found);
        }
        return found;
    }

    private static bool TryGetTriangleWallAdjacentDiagonalReflection(Vector3 wallCell, Vector3 currentCell, Vector3 dir, float panelSize, out Vector3 reflectedDir)
    {
        reflectedDir = Vector3.zero;
        Collider[] hits = Physics.OverlapSphere(wallCell, panelSize * 0.3f);
        Vector3 wallOffset = wallCell - currentCell;
        foreach (Collider hit in hits)
        {
            if (hit == null) continue;
            TriangleWall wall = hit.GetComponentInParent<TriangleWall>();
            if (wall != null && wall.TryGetAdjacentDiagonalReflection(dir, wallOffset, out reflectedDir))
            {
                return true;
            }
        }
        return false;
    }

    private static bool TryGetDiagonalSideTriangleReflection(Vector3 currentCell, Vector3 dir, float panelSize, out Vector3 reflectedDir)
    {
        reflectedDir = Vector3.zero;
        if (Mathf.Abs(dir.x) < 0.1f || Mathf.Abs(dir.z) < 0.1f) return false;

        Vector3 horizontalSide = currentCell + new Vector3(Mathf.Sign(dir.x) * panelSize, 0f, 0f);
        if (TryGetTriangleWallAdjacentDiagonalReflection(horizontalSide, currentCell, dir, panelSize, out reflectedDir))
        {
            return true;
        }

        Vector3 verticalSide = currentCell + new Vector3(0f, 0f, Mathf.Sign(dir.z) * panelSize);
        if (TryGetTriangleWallAdjacentDiagonalReflection(verticalSide, currentCell, dir, panelSize, out reflectedDir))
        {
            return true;
        }

        return false;
    }

    private static bool TryGetTriangleWallEntryReflection(Vector3 cell, Vector3 dir, float panelSize, out Vector3 reflectedDir)
    {
        reflectedDir = Vector3.zero;
        Collider[] hits = Physics.OverlapSphere(cell, panelSize * 0.3f);
        foreach (Collider hit in hits)
        {
            if (hit == null) continue;
            TriangleWall wall = hit.GetComponentInParent<TriangleWall>();
            if (wall != null && wall.TryGetEntryReflection(dir, out reflectedDir))
            {
                return true;
            }
        }
        return false;
    }

    private static bool TryGetTriangleWallReflection(Vector3 cell, Vector3 dir, float panelSize, out Vector3 reflectedDir)
    {
        reflectedDir = Vector3.zero;
        Collider[] hits = Physics.OverlapSphere(cell, panelSize * 0.3f);
        foreach (Collider hit in hits)
        {
            if (hit == null) continue;
            TriangleWall wall = hit.GetComponentInParent<TriangleWall>();
            if (wall != null && wall.TryGetReflectedDirection(dir, out reflectedDir))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsAxisBlocked(Vector3 fromCell, Vector3 axisDir, float panelSize, GameObject self = null, SimState state = null, bool includeBurnable = false)
    {
        Vector3 neighbor = fromCell + axisDir * panelSize;
        if (OverlapHasTag(neighbor, panelSize * 0.2f, "Reflect", null, null, out _))
        {
            return true;
        }

        if (TryGetInterCellWallBlock(fromCell, axisDir, panelSize, out _))
        {
            return true;
        }

        return includeBurnable &&
               TryGetTaggedObjectAtGridCell(neighbor, panelSize, "Burnable", self, state, out _);
    }

    /// <summary>
    /// 通常壁と、必要に応じて木箱を同じ反射壁として判定します。
    /// ターゲットボールが反射後に進む方向にも使うことで、斜め経路の判定順序を統一します。
    /// </summary>
    private static bool GetWallBlock(Vector3 cell, Vector3 dir, float panelSize, out Vector3 normal, GameObject self = null, SimState state = null, bool includeBurnable = false)
    {
        normal = Vector3.zero;

        float dx = Mathf.Abs(dir.x) > 0.1f ? Mathf.Sign(dir.x) : 0f;
        float dz = Mathf.Abs(dir.z) > 0.1f ? Mathf.Sign(dir.z) : 0f;
        if (dx == 0f && dz == 0f) return false;

        // 間壁へ斜めに交差する場合は、壁面では成分反射、端点では木箱と同じ完全反転を行います。
        if (dx != 0f && dz != 0f && TryGetDiagonalInterCellWallBlock(cell, dir, panelSize, out Vector3 interCellNormal))
        {
            normal = interCellNormal;
            return true;
        }

        bool blockedX = (dx != 0f) && IsAxisBlocked(cell, new Vector3(dx, 0f, 0f), panelSize, self, state, includeBurnable);
        bool blockedZ = (dz != 0f) && IsAxisBlocked(cell, new Vector3(0f, 0f, dz), panelSize, self, state, includeBurnable);

        if (dx != 0f && dz != 0f)
        {
            if (blockedX && blockedZ)
            {
                normal = new Vector3(-dx, 0f, -dz).normalized;
                return true;
            }
            if (blockedX) { normal = new Vector3(-dx, 0f, 0f); return true; }
            if (blockedZ) { normal = new Vector3(0f, 0f, -dz); return true; }

            Vector3 diagCell = cell + new Vector3(dx * panelSize, 0f, dz * panelSize);
            if (OverlapHasTag(diagCell, panelSize * 0.2f, "Reflect", null, null, out _) ||
                (includeBurnable && TryGetTaggedObjectAtGridCell(diagCell, panelSize, "Burnable", self, state, out _)))
            {
                normal = new Vector3(-dx, 0f, -dz).normalized;
                return true;
            }
            return false;
        }

        if (blockedX) { normal = new Vector3(-dx, 0f, 0f); return true; }
        if (blockedZ) { normal = new Vector3(0f, 0f, -dz); return true; }
        return false;
    }

    /// <summary>
    /// 指定グリッドに存在するタグ付きオブジェクトを、X/Z座標で確定して取得します。
    /// 斜め移動時に、ボールと木箱の高さ・コライダー中心の微差で検出が漏れることを防ぎます。
    /// </summary>
    private static bool TryGetTaggedObjectAtGridCell(Vector3 cell, float panelSize, string tag, GameObject self, SimState state, out GameObject found)
    {
        found = null;

        // 木箱の有無は物理コライダーの半径ではなく、盤面に配置されたタグとグリッド座標で確定します。
        // これにより、斜め反射直後の位置・高さの微差による検出漏れを防ぎます。
        GameObject[] candidates;
        try
        {
            candidates = GameObject.FindGameObjectsWithTag(tag);
        }
        catch (UnityException)
        {
            return false;
        }

        foreach (GameObject candidate in candidates)
        {
            if (candidate == null || candidate == self) continue;
            if (state != null && state.ShouldIgnore(candidate)) continue;
            // 炎で破壊済みの木箱は、Destroyの実行前でも同一ショット内の判定から除外します。
            if (tag == "Burnable" && state != null && state.IsDestroyedBurnable(candidate)) continue;

            Vector3 candidateCell = SnapToGrid(candidate.transform.position, panelSize);
            if (Mathf.Abs(candidateCell.x - cell.x) < 0.1f &&
                Mathf.Abs(candidateCell.z - cell.z) < 0.1f)
            {
                found = candidate;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 燃える障害物を、炎魔法未使用時の反射壁として判定します。
    /// 通常移動時と対象ボールへの衝突前判定で同じ規則を使うため、
    /// 木箱に接した対象ボールへ衝突したときのすり抜けを防ぎます。
    /// </summary>
    private static bool TryGetBurnableBlock(Vector3 cell, Vector3 dir, float panelSize, GameObject self, SimState state, out GameObject burnable, out Vector3 normal)
    {
        burnable = null;
        normal = Vector3.zero;

        float dx = Mathf.Abs(dir.x) > 0.1f ? Mathf.Sign(dir.x) : 0f;
        float dz = Mathf.Abs(dir.z) > 0.1f ? Mathf.Sign(dir.z) : 0f;
        if (dx == 0f && dz == 0f) return false;

        Vector3 nextCell = cell + StepOffset(dir, panelSize);
        if (TryGetTaggedObjectAtGridCell(nextCell, panelSize, "Burnable", self, state, out burnable))
        {
            normal = -Get8Direction(dir);
            return true;
        }

        // 斜め移動時は、通過するX/Z方向の隣接マスも木箱として扱います。
        if (dx != 0f && dz != 0f)
        {
            Vector3 sideX = cell + new Vector3(dx * panelSize, 0f, 0f);
            if (TryGetTaggedObjectAtGridCell(sideX, panelSize, "Burnable", self, state, out burnable))
            {
                normal = new Vector3(-dx, 0f, 0f);
                return true;
            }

            Vector3 sideZ = cell + new Vector3(0f, 0f, dz * panelSize);
            if (TryGetTaggedObjectAtGridCell(sideZ, panelSize, "Burnable", self, state, out burnable))
            {
                normal = new Vector3(0f, 0f, -dz);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 1マスの移動で触れる木箱をすべて収集します。
    /// 斜め移動では進行先だけでなく、X/Z側の横切りマスも対象です。
    /// </summary>
    private static void CollectBurnableBlocksOnMove(Vector3 currentCell, Vector3 dir, float panelSize, GameObject self, SimState state, List<GameObject> burnables)
    {
        if (burnables == null) return;

        float dx = Mathf.Abs(dir.x) > 0.1f ? Mathf.Sign(dir.x) : 0f;
        float dz = Mathf.Abs(dir.z) > 0.1f ? Mathf.Sign(dir.z) : 0f;
        if (dx == 0f && dz == 0f) return;

        AddBurnableAtCell(currentCell + StepOffset(dir, panelSize), panelSize, self, state, burnables);

        if (dx != 0f && dz != 0f)
        {
            AddBurnableAtCell(currentCell + new Vector3(dx * panelSize, 0f, 0f), panelSize, self, state, burnables);
            AddBurnableAtCell(currentCell + new Vector3(0f, 0f, dz * panelSize), panelSize, self, state, burnables);
        }
    }

    private static void AddBurnableAtCell(Vector3 cell, float panelSize, GameObject self, SimState state, List<GameObject> burnables)
    {
        if (TryGetTaggedObjectAtGridCell(cell, panelSize, "Burnable", self, state, out GameObject burnable) &&
            burnable != null &&
            !burnables.Contains(burnable))
        {
            burnables.Add(burnable);
        }
    }

    /// <summary>
    /// 木箱を「このショットでは破壊済み」として経路計算上だけ無効化します。
    /// [変更] 以前はここで Object.Destroy を呼んでいたため、ショットした瞬間に木箱が消えていました。
    /// 実際の破壊は、球がその位置へ到達したときに PathPoint.ApplyPendingEffects が行います。
    /// </summary>
    private static void MarkBurnableDestroyedForCurrentShot(GameObject burnable, SimState state)
    {
        if (burnable == null) return;

        // Destroy はフレーム末までCollider・タグを残すため、経路計算上は同時に無効化する。
        if (state != null)
        {
            state.destroyedBurnables.Add(burnable);
        }
    }

    private static Vector3 ReflectDir(Vector3 dir, Vector3 normal)
    {
        return Get8Direction(Vector3.Reflect(dir, normal.normalized));
    }

    private static bool TryResolveTargetMove(GameObject target, Vector3 incomingDir, float panelSize, SimState state, out Vector3 actualMoveDir)
    {
        actualMoveDir = incomingDir;
        if (target == null) return false;

        Vector3 cell = SnapToGrid(target.transform.position, panelSize);

        if (state != null)
        {
            if (state.virtualBalls.TryGetValue(target, out Vector3 virtualCell))
            {
                cell = virtualCell;
            }
        }

        if (TryGetTriangleWallEntryReflection(cell, incomingDir, panelSize, out _))
        {
            return false;
        }

        if (TryGetTriangleWallReflection(cell, incomingDir, panelSize, out Vector3 triangleDir))
        {
            if (Vector3.Dot(triangleDir, incomingDir) < -0.5f) return false;
            if (GetWallBlock(cell, triangleDir, panelSize, out _, target, state, includeBurnable: true)) return false;
            actualMoveDir = triangleDir;
            return true;
        }

        // ターゲットの進行先については、通常壁と木箱を同じ優先順位で判定します。
        // これにより、壁で反射した直後の斜め経路でも木箱の横マスを通過できません。
        if (!GetWallBlock(cell, incomingDir, panelSize, out Vector3 wallNormal, target, state, includeBurnable: true))
        {
            return true;
        }

        Vector3 newDir = ReflectDir(incomingDir, wallNormal);

        if (Vector3.Dot(newDir, incomingDir) < -0.5f) return false;
        if (GetWallBlock(cell, newDir, panelSize, out _, target, state, includeBurnable: true)) return false;

        actualMoveDir = newDir;
        return true;
    }

    public static List<PathPoint> CalculatePath(GameObject self, Vector3 startPos, Vector3 dir, int totalPanels, float panelSize, float ballRadius, SimState state, bool isFireActive, out GameObject hitBall, out int passPanels, out Vector3 finalDir)
    {
        List<PathPoint> path = new List<PathPoint>();
        Vector3 currentCell = SnapToGrid(startPos, panelSize);
        Vector3 currentDir = Get8Direction(dir);
        hitBall = null;
        passPanels = 0;
        finalDir = currentDir;

        bool isShotBall = self.GetComponent<ShotBall>() != null;
        bool isWaterActive = isShotBall && MagicManager.Instance != null && MagicManager.Instance.ActiveMagic == MagicType.Water;
        // 炎魔法で木箱を破壊できるのは、最初にショットされた主ボールだけです。
        bool canBreakBurnable = isShotBall && isFireActive;
        // 炎魔法は、主ボールが最初のターゲットに接触した時点で終了します。
        bool shouldConsumeFireOnNextBallHit = canBreakBurnable;

        int remaining = totalPanels;
        int loopFailsafe = 0;
        int bounceGuard = 0;

        int maxBounceCount = Mathf.Max(8, totalPanels * 4);
        bool bouncedOffBall = false;

        while (remaining > 0 && loopFailsafe < 100)
        {
            loopFailsafe++;

            if (TryGetTriangleWallReflection(currentCell, currentDir, panelSize, out Vector3 triangleReflected))
            {
                path.Add(new PathPoint(currentCell + currentDir * (panelSize * 0.5f - ballRadius)));
                path.Add(new PathPoint(currentCell, skipAnimation: true));

                currentDir = triangleReflected;
                finalDir = currentDir;
                bounceGuard++;

                if (bounceGuard > maxBounceCount) break;
                continue;
            }

            if (GetWallBlock(currentCell, currentDir, panelSize, out Vector3 wallNormal))
            {
                Vector3 reflected = ReflectDir(currentDir, wallNormal);

                path.Add(new PathPoint(currentCell + currentDir * (panelSize * 0.5f - ballRadius)));
                path.Add(new PathPoint(currentCell, skipAnimation: true));

                currentDir = reflected;
                finalDir = currentDir;

                // 反射後も木箱・通常壁と同じ反射ループを使います。
                // これにより、{1,0}からの斜め入射のように、次の外周反射で
                // 左上へ抜けられる経路を早期停止させません。
                bounceGuard++;

                if (bounceGuard > maxBounceCount) break;
                continue;
            }

            Vector3 nextCell = currentCell + StepOffset(currentDir, panelSize);

            // このステップで球が到達したときに破壊／消火するものの予約。
            // 経路計算の時点では消さず、実際の適用は PlayChain の再生時に行う。
            List<GameObject> pendingBurnables = null;
            List<FlameTile> pendingFlames = null;

            // 燃える障害物（Burnableタグ）は、炎魔法中の主ボールだけが破壊して通過します。
            // ターゲットボール、および炎魔法未使用時は通常壁と同じ反射規則です。
            if (TryGetBurnableBlock(currentCell, currentDir, panelSize, self, state, out GameObject targetBurnable, out Vector3 burnableNormal))
            {
                if (canBreakBurnable)
                {
                    // 炎状態の主ボールは、斜め移動で同時に横切る木箱も含めてすべて破壊する。
                    List<GameObject> burnablesOnMove = new List<GameObject>();
                    CollectBurnableBlocksOnMove(currentCell, currentDir, panelSize, self, state, burnablesOnMove);

                    foreach (GameObject burnable in burnablesOnMove)
                    {
                        MarkBurnableDestroyedForCurrentShot(burnable, state);
                    }

                    // 実際の破壊は、球が到達したときに行う。ここでは予約するだけで、
                    // 移動そのものは下の通常処理（三角壁判定などを含む）にそのまま任せる。
                    if (burnablesOnMove.Count > 0)
                    {
                        pendingBurnables = burnablesOnMove;
                    }
                }
                else
                {
                    path.Add(new PathPoint(currentCell + currentDir * (panelSize * 0.5f - ballRadius)));
                    path.Add(new PathPoint(currentCell, skipAnimation: true));

                    currentDir = ReflectDir(currentDir, burnableNormal);
                    finalDir = currentDir;
                    bounceGuard++;

                    if (bounceGuard > maxBounceCount) break;
                    continue;
                }
            }

            if (TryGetDiagonalSideTriangleReflection(currentCell, currentDir, panelSize, out Vector3 sideReflected))
            {
                path.Add(new PathPoint(currentCell + currentDir * (panelSize * 0.5f - ballRadius)));
                path.Add(new PathPoint(currentCell, skipAnimation: true));

                currentDir = sideReflected;
                finalDir = currentDir;
                bounceGuard++;

                if (bounceGuard > maxBounceCount) break;
                continue;
            }

            if (TryGetTriangleWallAdjacentDiagonalReflection(nextCell, currentCell, currentDir, panelSize, out Vector3 adjacentReflected))
            {
                path.Add(new PathPoint(currentCell + currentDir * (panelSize * 0.5f - ballRadius)));
                path.Add(new PathPoint(currentCell, skipAnimation: true));

                currentDir = adjacentReflected;
                finalDir = currentDir;
                bounceGuard++;

                if (bounceGuard > maxBounceCount) break;
                continue;
            }

            if (TryGetTriangleWallEntryReflection(nextCell, currentDir, panelSize, out Vector3 entryReflected))
            {
                path.Add(new PathPoint(currentCell + currentDir * (panelSize * 0.5f - ballRadius)));
                path.Add(new PathPoint(currentCell, skipAnimation: true));

                currentDir = entryReflected;
                finalDir = currentDir;
                bounceGuard++;

                if (bounceGuard > maxBounceCount) break;
                continue;
            }

            if (TryGetActiveFlameTilesOnMove(currentCell, currentDir, panelSize, out List<FlameTile> flameTiles))
            {
                if (isWaterActive)
                {
                    // 水魔法中は、斜め移動で横切る炎マスも含めて消火して通過します。
                    // [変更] 以前はここで即座に Extinguish していたためショットの瞬間に炎が消えていました。
                    // 実際の消火は球が到達したときに行うため、ここでは予約するだけにして、
                    // 移動そのものは下の通常処理にそのまま任せます。
                    pendingFlames = new List<FlameTile>(flameTiles);
                }
                else
                {
                    // 木箱と同じ優先順位で、斜めに横切るX/Z方向の炎マスもゲームオーバーにします。
                    // 見た目は炎マス中心へ曲げず、本来の移動線上の接触位置で停止します。
                    path.Add(new PathPoint(GetFlameContactPoint(currentCell, currentDir, panelSize, ballRadius), isHazard: true));
                    hitBall = null;
                    passPanels = 0;
                    finalDir = currentDir;
                    break;
                }
            }

            if (OverlapHasTag(nextCell, panelSize * 0.3f, "Pocket", self, state, out GameObject pocket))
            {
                path.Add(new PathPoint(SnapToGrid(pocket.transform.position, panelSize), isPocket: true));
                hitBall = null;
                passPanels = 0;
                finalDir = currentDir;
                break;
            }

            GameObject other = GetBallAtCell(nextCell, self, state, panelSize);

            if (other != null)
            {
                bool targetCanMove = TryResolveTargetMove(other, currentDir, panelSize, state, out Vector3 targetMoveDir);

                if (!targetCanMove)
                {
                    bouncedOffBall = true;

                    // 壁際でターゲットが動けない場合も、炎の最初の命中では
                    // 通常の炎命中と同じく「初回衝突ボーナスを加えてから2倍」に統一します。
                    bool doubleReturnMomentum = shouldConsumeFireOnNextBallHit;
                    if (doubleReturnMomentum)
                    {
                        int impactBonus = isShotBall ? 1 : 0;
                        remaining = (remaining + impactBonus) * 2;
                    }

                    path.Add(new PathPoint(currentCell + currentDir * (panelSize * 0.5f - ballRadius), isBallHit: true, consumeFireOnHit: doubleReturnMomentum));
                    shouldConsumeFireOnNextBallHit = false;
                    canBreakBurnable = false;
                    path.Add(new PathPoint(currentCell));

                    currentDir = ReflectDir(currentDir, -currentDir);
                    finalDir = currentDir;
                    bounceGuard++;

                    GameObject overlapBall = GetBallAtCell(currentCell, self, state, panelSize);
                    if (overlapBall != null)
                    {
                        bool tCanMove = TryResolveTargetMove(overlapBall, currentDir, panelSize, state, out Vector3 tMoveDir);
                        if (!tCanMove)
                        {
                            if (bounceGuard > maxBounceCount) break;
                            continue;
                        }

                        hitBall = overlapBall;
                        path.Add(new PathPoint(currentCell, isBallHit: true, consumeFireOnHit: shouldConsumeFireOnNextBallHit));
                        shouldConsumeFireOnNextBallHit = false;

                        int panelsToPass = remaining + ((isShotBall && !bouncedOffBall) ? 1 : 0);
                        // この時点では最初のターゲット命中で炎魔法は終了済みです。
                        passPanels = panelsToPass;
                        finalDir = tMoveDir;
                        break;
                    }

                    if (bounceGuard > maxBounceCount) break;
                    continue;
                }

                int energyAtImpact = remaining;

                bool fireAtImpact = shouldConsumeFireOnNextBallHit;
                hitBall = other;
                path.Add(new PathPoint(nextCell, isBallHit: true, consumeFireOnHit: fireAtImpact));
                shouldConsumeFireOnNextBallHit = false;
                canBreakBurnable = false;
                remaining--;

                int bonus = (isShotBall && !bouncedOffBall) ? 1 : 0;
                int panelsToPassNormal = energyAtImpact + bonus;

                if (fireAtImpact) panelsToPassNormal *= 2;

                passPanels = panelsToPassNormal;

                finalDir = targetMoveDir;
                break;
            }

            currentCell = nextCell;

            // このマスへ到達したときに破壊／消火するものを、その経路点に紐づける。
            PathPoint movePoint = new PathPoint(currentCell);
            movePoint.burnablesToDestroy = pendingBurnables;
            movePoint.flamesToExtinguish = pendingFlames;
            path.Add(movePoint);

            remaining--;
            bounceGuard = 0;
            finalDir = currentDir;
        }

        return path;
    }

    public static List<ChainStep> SimulateChain(GameObject firstBall, Vector3 startDir, int startPanels, bool isFireActive = false)
    {
        List<ChainStep> steps = new List<ChainStep>();
        SimState state = new SimState();
        // 最初のターゲット命中後は、後続の連鎖計算へ炎効果を渡しません。
        bool fireActiveForCurrentBall = isFireActive;

        GameObject currentBall = firstBall;
        Vector3 currentDir = Get8Direction(startDir);
        int currentPanels = startPanels;

        int chainFailsafe = 0;
        while (currentBall != null && chainFailsafe < 50)
        {
            chainFailsafe++;

            GetBallSettings(currentBall, out float panelSize, out float ballRadius, out _, out _);

            Vector3 startPos = currentBall.transform.position;
            if (state.virtualBalls.TryGetValue(currentBall, out Vector3 virtualStart))
            {
                startPos = virtualStart;
            }

            List<PathPoint> path = CalculatePath(currentBall, startPos, currentDir, currentPanels, panelSize, ballRadius, state, fireActiveForCurrentBall, out GameObject hitBall, out int passPanels, out Vector3 finalDir);

            steps.Add(new ChainStep { ball = currentBall, path = path });

            // 経路上の最初のターゲット接触で炎魔法を消費し、以後の連鎖へ渡さない。
            if (fireActiveForCurrentBall && path != null)
            {
                foreach (PathPoint point in path)
                {
                    if (point != null && point.consumeFireOnHit)
                    {
                        fireActiveForCurrentBall = false;
                        break;
                    }
                }
            }

            Vector3 originCell = SnapToGrid(startPos, panelSize);
            Vector3 finalRestPos = path.Count > 0
                ? SnapToGrid(path[path.Count - 1].position, panelSize)
                : originCell;

            if (Vector3.Distance(finalRestPos, originCell) > 0.1f)
            {
                state.movedBalls.Add(currentBall);
            }
            state.occupiedCells.Add(finalRestPos);

            state.virtualBalls[currentBall] = finalRestPos;

            if (hitBall == null) break;

            currentBall = hitBall;
            currentDir = finalDir;
            currentPanels = passPanels;
        }

        return steps;
    }

    public static IEnumerator AnimateStep(ChainStep step)
    {
        if (step == null || step.ball == null) yield break;

        Transform tr = step.ball.transform;
        List<PathPoint> path = step.path;

        GetBallSettings(step.ball, out float panelSize, out _, out float shotSpeed, out float shotDuration);

        bool[] sePlayed = new bool[path.Count];

        float totalPhysicalDistance = 0f;
        Vector3 prev = tr.position;
        foreach (var p in path)
        {
            if (p.skipAnimation) continue;
            totalPhysicalDistance += Vector3.Distance(prev, p.position);
            prev = p.position;
        }

        float traveled = 0f;
        float t = 0f;
        int pathIndex = 0;

        while (traveled < totalPhysicalDistance && pathIndex < path.Count)
        {
            float ratio = Mathf.Clamp01(t / shotDuration);
            float factor = 1f - ratio;
            factor = factor * factor;
            if (factor < 0.02f) factor = 0.02f;

            float frameSpeed = shotSpeed * factor * Time.deltaTime;
            float remainingFrameSpeed = frameSpeed;

            while (remainingFrameSpeed > 0.0001f && pathIndex < path.Count)
            {
                PathPoint segment = path[pathIndex];
                if (segment.skipAnimation)
                {
                    // 演出は飛ばすが、その位置での破壊・消火は適用する。
                    segment.ApplyPendingEffects();
                    pathIndex++;
                    continue;
                }
                Vector3 segmentEnd = segment.position;
                float distToNext = Vector3.Distance(tr.position, segmentEnd);

                if (distToNext <= 0.0001f)
                {
                    tr.position = segmentEnd;
                    segment.ApplyPendingEffects();
                    PlayHitSE(segment, sePlayed, pathIndex);
                    if (HandlePocketArrival(step.ball, segment)) yield break;
                    if (HandleHazardArrival(step.ball, segment)) yield break;
                    MagicPotion.TryCollectAtBall(step.ball);
                    pathIndex++;
                    continue;
                }

                if (remainingFrameSpeed >= distToNext)
                {
                    tr.position = segmentEnd;
                    segment.ApplyPendingEffects();
                    PlayHitSE(segment, sePlayed, pathIndex);
                    if (HandlePocketArrival(step.ball, segment)) yield break;
                    if (HandleHazardArrival(step.ball, segment)) yield break;
                    MagicPotion.TryCollectAtBall(step.ball);
                    remainingFrameSpeed -= distToNext;
                    pathIndex++;
                }
                else
                {
                    tr.position = Vector3.MoveTowards(tr.position, segmentEnd, remainingFrameSpeed);
                    remainingFrameSpeed = 0f;
                }
            }

            traveled += frameSpeed;
            t += Time.deltaTime;
            yield return null;

            if (step.ball == null) yield break;
        }

        if (path.Count > 0)
        {
            tr.position = SnapToGrid(path[path.Count - 1].position, panelSize);
            path[path.Count - 1].ApplyPendingEffects();
            if (HandlePocketArrival(step.ball, path[path.Count - 1])) yield break;
            if (HandleHazardArrival(step.ball, path[path.Count - 1])) yield break;
            MagicPotion.TryCollectAtBall(step.ball);
        }
        else
        {
            tr.position = SnapToGrid(tr.position, panelSize);
        }

        for (int i = 0; i < path.Count; i++)
        {
            PlayHitSE(path[i], sePlayed, i);
        }
    }

    // 経路上で主ボールがポケットに到達した瞬間、ゲームオーバーを通知します。
    private static bool HandlePocketArrival(GameObject ball, PathPoint point)
    {
        if (ball == null || point == null || !point.isPocket) return false;

        ShotBall shotBall = ball.GetComponent<ShotBall>();
        if (shotBall == null) return false;

        shotBall.TriggerGameOverFromPocket();
        return true;
    }

    // 未消火の炎マスに到達した球は、主ボール・ターゲットを問わずゲームオーバーにします。
    private static bool HandleHazardArrival(GameObject ball, PathPoint point)
    {
        if (ball == null || point == null || !point.isHazard) return false;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.TriggerGameOver();
        }

        Object.Destroy(ball);
        return true;
    }

    private static void PlayHitSE(PathPoint point, bool[] sePlayed, int index)
    {
        if (point == null || !point.isBallHit) return;
        if (index < 0 || index >= sePlayed.Length) return;
        if (sePlayed[index]) return;
        sePlayed[index] = true;

        // 視覚的に最初のターゲットへ接触した瞬間、炎魔法を一度だけ終了します。
        if (point.consumeFireOnHit && MagicManager.Instance != null)
        {
            MagicManager.Instance.ConsumeMagic(MagicType.Fire);
        }

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySE(SEType.BallHit);
        }
    }

    public static IEnumerator PlayChain(List<ChainStep> steps)
    {
        foreach (var step in steps)
        {
            if (step == null || step.ball == null) continue;
            yield return AnimateStep(step);
        }
    }

    public static IEnumerator PushBallRoutine(GameObject ball, Vector3 pushDirection, int totalPanels)
    {
        if (ball == null) yield break;

        Vector3 dir = Get8Direction(pushDirection);
        List<ChainStep> steps = SimulateChain(ball, dir, totalPanels);
        yield return PlayChain(steps);
    }
}