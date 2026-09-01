using UnityEngine;

/// <summary>
/// マスとマスの境界に置く薄壁です。
/// Vertical はZ方向に伸びる壁でX方向の移動を、Horizontal はX方向に伸びる壁でZ方向の移動を遮ります。
/// </summary>
[RequireComponent(typeof(Collider))]
public sealed class InterCellWall : MonoBehaviour
{
    public enum Orientation
    {
        Vertical = 0,
        Horizontal = 1,
    }

    [Header("Wall Settings")]
    [Tooltip("Vertical はZ方向に伸びる壁、Horizontal はX方向に伸びる壁です。")]
    [SerializeField] private Orientation orientation = Orientation.Vertical;

    [Tooltip("壁が連続して覆うマス数です。長い壁にする場合だけ増やしてください。")]
    [Min(1)]
    [SerializeField] private int lengthInCells = 1;

    [Tooltip("境界線との位置ずれを吸収する許容値です。通常は変更しません。")]
    [SerializeField, Min(0.01f)] private float boundaryTolerance = 0.08f;

    public Orientation WallOrientation
    {
        get => orientation;
        set => orientation = value;
    }

    /// <summary>
    /// 指定セルから隣接セルへ進むとき、このマス間壁が遮るかを判定します。
    /// 返す法線は BallPath の通常壁反射でそのまま使用できます。
    /// </summary>
    public bool TryGetBlockingNormal(Vector3 fromCell, Vector3 axisDirection, float panelSize, out Vector3 normal)
    {
        normal = Vector3.zero;
        if (panelSize <= 0f) return false;

        float dx = Mathf.Abs(axisDirection.x) > 0.1f ? Mathf.Sign(axisDirection.x) : 0f;
        float dz = Mathf.Abs(axisDirection.z) > 0.1f ? Mathf.Sign(axisDirection.z) : 0f;

        // マス間壁は、一度にXかZのどちらか一方の境界だけを判定します。
        if (orientation == Orientation.Vertical)
        {
            if (dx == 0f || dz != 0f) return false;

            float crossingX = fromCell.x + dx * panelSize * 0.5f;
            float halfLength = Mathf.Max(0, lengthInCells - 1) * panelSize * 0.5f + boundaryTolerance;

            if (Mathf.Abs(transform.position.x - crossingX) > boundaryTolerance) return false;
            if (Mathf.Abs(transform.position.z - fromCell.z) > halfLength) return false;

            normal = new Vector3(-dx, 0f, 0f);
            return true;
        }

        if (dz == 0f || dx != 0f) return false;

        float crossingZ = fromCell.z + dz * panelSize * 0.5f;
        float horizontalHalfLength = Mathf.Max(0, lengthInCells - 1) * panelSize * 0.5f + boundaryTolerance;

        if (Mathf.Abs(transform.position.z - crossingZ) > boundaryTolerance) return false;
        if (Mathf.Abs(transform.position.x - fromCell.x) > horizontalHalfLength) return false;

        normal = new Vector3(0f, 0f, -dz);
        return true;
    }

    /// <summary>
    /// 斜め移動がこの薄壁の線分に触れるかを判定します。
    /// 移動線と境界線が交差した場合だけ、入射方向全体を反転して来た方向へ戻します。
    /// </summary>
    public bool BlocksDiagonalMove(Vector3 fromCell, Vector3 diagonalDirection, float panelSize)
    {
        if (panelSize <= 0f) return false;

        float dx = Mathf.Abs(diagonalDirection.x) > 0.1f ? Mathf.Sign(diagonalDirection.x) : 0f;
        float dz = Mathf.Abs(diagonalDirection.z) > 0.1f ? Mathf.Sign(diagonalDirection.z) : 0f;
        if (dx == 0f || dz == 0f) return false;

        float halfLength = Mathf.Max(1, lengthInCells) * panelSize * 0.5f + boundaryTolerance;

        if (orientation == Orientation.Vertical)
        {
            // (x, z) から (x+dx, z+dz) へ進む対角線と、x=壁位置の縦線分の交点を確認します。
            float movementMinX = Mathf.Min(fromCell.x, fromCell.x + dx * panelSize);
            float movementMaxX = Mathf.Max(fromCell.x, fromCell.x + dx * panelSize);
            if (transform.position.x < movementMinX - boundaryTolerance || transform.position.x > movementMaxX + boundaryTolerance)
                return false;

            float t = (transform.position.x - fromCell.x) / (dx * panelSize);
            if (t < -boundaryTolerance || t > 1f + boundaryTolerance) return false;

            float crossingZ = fromCell.z + dz * panelSize * t;
            return Mathf.Abs(crossingZ - transform.position.z) <= halfLength;
        }

        // (x, z) から (x+dx, z+dz) へ進む対角線と、z=壁位置の横線分の交点を確認します。
        float movementMinZ = Mathf.Min(fromCell.z, fromCell.z + dz * panelSize);
        float movementMaxZ = Mathf.Max(fromCell.z, fromCell.z + dz * panelSize);
        if (transform.position.z < movementMinZ - boundaryTolerance || transform.position.z > movementMaxZ + boundaryTolerance)
            return false;

        float u = (transform.position.z - fromCell.z) / (dz * panelSize);
        if (u < -boundaryTolerance || u > 1f + boundaryTolerance) return false;

        float crossingX = fromCell.x + dx * panelSize * u;
        return Mathf.Abs(crossingX - transform.position.x) <= halfLength;
    }

    /// <summary>
    /// 薄壁の端点を斜めに横切る場合だけ、入射方向全体を反転させるべきかを返します。
    /// 壁と同じ列・行から斜めに入る場合は、通常壁と同様に片方の成分だけ反射します。
    /// </summary>
    public bool IsCornerDiagonalCollision(Vector3 fromCell, Vector3 diagonalDirection, float panelSize)
    {
        if (!BlocksDiagonalMove(fromCell, diagonalDirection, panelSize)) return false;

        // 通常の横・縦隣接マスの範囲。ここからの斜め入射は通常壁と同じ成分反射です。
        float innerHalfLength = Mathf.Max(0, lengthInCells - 1) * panelSize * 0.5f + boundaryTolerance;
        float alongDistance = orientation == Orientation.Vertical
            ? Mathf.Abs(fromCell.z - transform.position.z)
            : Mathf.Abs(fromCell.x - transform.position.x);

        // 壁の端点を斜めに横切る場合は、入射した経路をそのまま反転します。
        // 例: 壁の右下から左上へ入射した場合は、右下へ戻ります。
        return alongDistance > innerHalfLength + boundaryTolerance;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Vector3 size = orientation == Orientation.Vertical
            ? new Vector3(0.08f, 0.1f, Mathf.Max(1, lengthInCells))
            : new Vector3(Mathf.Max(1, lengthInCells), 0.1f, 0.08f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, size);
    }
#endif
}