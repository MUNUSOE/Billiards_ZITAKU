using UnityEngine;

[RequireComponent(typeof(Collider), typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class TriangleWall : MonoBehaviour
{
    public enum Corner
    {
        UpperLeft = 0,
        UpperRight = 1,
        LowerRight = 2,
        LowerLeft = 3,
    }

    [Tooltip("塗りつぶされる三角形の角。UpperLeft は共有された図と同じ向きです。")]
    [SerializeField] private Corner corner = Corner.UpperLeft;

    [Tooltip("見た目の三角形をマスより小さくする倍率です。判定範囲には影響しません。直角の角（マスの角）を基準に縮小します。")]
    [Range(0.25f, 1f)]
    [SerializeField] private float visualScale = 0.5f;

    private Mesh generatedMesh;

    public Corner WallCorner
    {
        get => corner;
        set
        {
            corner = value;
            RebuildVisual();
        }
    }

    private void Awake()
    {
        RebuildVisual();
    }

    private void OnValidate()
    {
        RebuildVisual();
    }

    private void OnDestroy()
    {
        if (generatedMesh == null) return;

        if (Application.isPlaying) Destroy(generatedMesh);
        else DestroyImmediate(generatedMesh);
    }

    private void RebuildVisual()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null) return;

        if (generatedMesh != null)
        {
            if (Application.isPlaying) Destroy(generatedMesh);
            else DestroyImmediate(generatedMesh);
        }

        // [変更] 直角の角（マスの角＝固定点）を基準に、そこから伸びる2辺の長さを visualScale で
        // 縮小する方式に変更。以前はマス中心(0,0)を基準に全頂点を等比縮小していたため、
        // 直角の角自体もマス中心へ寄ってしまっていた。
        Vector3[] vertices = GetTriangleVertices(visualScale);

        generatedMesh = new Mesh
        {
            name = "TriangleWallVisual",
            hideFlags = HideFlags.DontSave,
            vertices = vertices,
            triangles = new[] { 0, 1, 2, 2, 1, 0 },
            uv = new[] { Vector2.zero, Vector2.up, Vector2.right },
        };
        generatedMesh.RecalculateNormals();
        generatedMesh.RecalculateBounds();
        meshFilter.sharedMesh = generatedMesh;
    }

    /// <summary>
    /// 直角の角をマスの角に固定したまま、そこから伸びる2辺(leg)の長さだけを
    /// visualScale(0〜1、1でマス全体)に応じて変化させて三角形の頂点を求めます。
    /// </summary>
    /// <param name="leg">直角の角から各辺方向へ伸ばす長さ（1.0でマス全体＝元のUpperLeft基準の挙動と一致）。</param>
    private Vector3[] GetTriangleVertices(float leg)
    {
        const float y = 0.06f;
        const float full = 0.5f; // マス半分の距離。直角の角はここに固定し、スケールでは動かさない。

        switch (corner)
        {
            case Corner.UpperRight:
                {
                    Vector3 c = new Vector3(full, y, full);
                    Vector3 xLeg = new Vector3(full - leg, y, full);
                    Vector3 zLeg = new Vector3(full, y, full - leg);
                    return new[] { c, xLeg, zLeg };
                }

            case Corner.LowerRight:
                {
                    Vector3 c = new Vector3(full, y, -full);
                    Vector3 zLeg = new Vector3(full, y, -full + leg);
                    Vector3 xLeg = new Vector3(full - leg, y, -full);
                    return new[] { c, zLeg, xLeg };
                }

            case Corner.LowerLeft:
                {
                    Vector3 c = new Vector3(-full, y, -full);
                    Vector3 xLeg = new Vector3(-full + leg, y, -full);
                    Vector3 zLeg = new Vector3(-full, y, -full + leg);
                    return new[] { c, xLeg, zLeg };
                }

            default: // UpperLeft
                {
                    Vector3 c = new Vector3(-full, y, full);
                    Vector3 zLeg = new Vector3(-full, y, full - leg);
                    Vector3 xLeg = new Vector3(-full + leg, y, full);
                    return new[] { c, zLeg, xLeg };
                }
        }
    }

    /// <summary>
    /// 現在の壁マスで進行方向が三角形の塗りつぶし側へ向く場合に、8方向グリッド用の反射後方向を返します。
    /// UpperLeft を基準にし、他の3方向は盤面上で90度ずつ時計回りに回転して求めます。
    /// </summary>
    public bool TryGetReflectedDirection(Vector3 incomingDirection, out Vector3 reflectedDirection)
    {
        Vector2Int incoming = ToDirection(incomingDirection);
        if (incoming == Vector2Int.zero)
        {
            reflectedDirection = Vector3.zero;
            return false;
        }

        Vector2Int localIncoming = RotateCounterClockwise(incoming, (int)corner);
        if (!TryReflectUpperLeft(localIncoming, out Vector2Int localReflected))
        {
            reflectedDirection = Vector3.zero;
            return false;
        }

        Vector2Int reflected = RotateClockwise(localReflected, (int)corner);
        reflectedDirection = new Vector3(reflected.x, 0f, reflected.y);
        return true;
    }

    /// <summary>
    /// 上下左右の面から三角壁へ入る場合に、壁マスへ入る前の反射方向を返します。
    /// 斜め入力はここでは判定せず、現在マスと壁マスの位置を確認する専用処理へ任せます。
    /// </summary>
    public bool TryGetEntryReflection(Vector3 incomingDirection, out Vector3 reflectedDirection)
    {
        Vector2Int incoming = ToDirection(incomingDirection);
        if (incoming == Vector2Int.zero)
        {
            reflectedDirection = Vector3.zero;
            return false;
        }

        Vector2Int localIncoming = RotateCounterClockwise(incoming, (int)corner);
        if (!TryReflectEntryUpperLeft(localIncoming, out Vector2Int localReflected))
        {
            reflectedDirection = Vector3.zero;
            return false;
        }

        Vector2Int reflected = RotateClockwise(localReflected, (int)corner);
        reflectedDirection = new Vector3(reflected.x, 0f, reflected.y);
        return true;
    }

    private static bool TryReflectEntryUpperLeft(Vector2Int incoming, out Vector2Int reflected)
    {
        // UpperLeft triangle: only cardinal arrivals from the top and left reflect before entry.
        // Diagonal arrivals are evaluated together with their source-cell position.
        if (incoming == Vector2Int.down)
        {
            reflected = Vector2Int.up;
            return true;
        }

        if (incoming == Vector2Int.right)
        {
            reflected = Vector2Int.left;
            return true;
        }

        reflected = Vector2Int.zero;
        return false;
    }

    /// <summary>
    /// 三角壁マスの上または左の隣接マスから、斜辺へ斜めに当たる特別な反射を返します。
    /// UpperLeft では「上のマスから左下」と「左のマスから右上」が左上へ反射します。
    /// 他のCornerではこの規則を90度ずつ回転して適用します。
    /// </summary>
    public bool TryGetAdjacentDiagonalReflection(Vector3 incomingDirection, Vector3 wallOffset, out Vector3 reflectedDirection)
    {
        Vector2Int incoming = ToDirection(incomingDirection);
        Vector2Int offset = ToDirection(wallOffset);
        if (incoming == Vector2Int.zero || offset == Vector2Int.zero)
        {
            reflectedDirection = Vector3.zero;
            return false;
        }

        Vector2Int localIncoming = RotateCounterClockwise(incoming, (int)corner);
        Vector2Int localOffset = RotateCounterClockwise(offset, (int)corner);

        Vector2Int localReflected;
        if (localOffset == Vector2Int.down && localIncoming == new Vector2Int(-1, -1))
        {
            localReflected = new Vector2Int(-1, 1);
        }
        else if (localOffset == Vector2Int.right && localIncoming == new Vector2Int(1, 1))
        {
            localReflected = new Vector2Int(-1, 1);
        }
        else
        {
            reflectedDirection = Vector3.zero;
            return false;
        }

        Vector2Int reflected = RotateClockwise(localReflected, (int)corner);
        reflectedDirection = new Vector3(reflected.x, 0f, reflected.y);
        return true;
    }

    private static bool TryReflectUpperLeft(Vector2Int incoming, out Vector2Int reflected)
    {
        // UpperLeft triangle: only the three inputs that head into the filled corner reflect.
        // The source-side mapping is right→down, bottom→right, bottom-right→bottom-right.
        if (incoming == Vector2Int.left)
        {
            reflected = Vector2Int.down;
            return true;
        }

        if (incoming == Vector2Int.up)
        {
            reflected = Vector2Int.right;
            return true;
        }

        if (incoming == new Vector2Int(-1, 1))
        {
            reflected = new Vector2Int(1, -1);
            return true;
        }

        reflected = Vector2Int.zero;
        return false;
    }

    private static Vector2Int ToDirection(Vector3 direction)
    {
        int x = Mathf.Abs(direction.x) > 0.1f ? (direction.x > 0f ? 1 : -1) : 0;
        int z = Mathf.Abs(direction.z) > 0.1f ? (direction.z > 0f ? 1 : -1) : 0;
        return new Vector2Int(x, z);
    }

    private static Vector2Int RotateClockwise(Vector2Int value, int quarterTurns)
    {
        for (int i = 0; i < quarterTurns; i++)
        {
            value = new Vector2Int(value.y, -value.x);
        }
        return value;
    }

    private static Vector2Int RotateCounterClockwise(Vector2Int value, int quarterTurns)
    {
        for (int i = 0; i < quarterTurns; i++)
        {
            value = new Vector2Int(-value.y, value.x);
        }
        return value;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(0.9f, 0.1f, 0.9f));
    }
#endif
}