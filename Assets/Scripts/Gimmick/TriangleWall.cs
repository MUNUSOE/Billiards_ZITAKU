using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class TriangleWall : MonoBehaviour
{
    public enum Corner
    {
        UpperLeft = 0,
        UpperRight = 1,
        LowerRight = 2,
        LowerLeft = 3,
    }

    [Tooltip("塗りつぶされる三角形の角。直角部分がこの角に合うように配置されます。")]
    [SerializeField] private Corner corner = Corner.UpperLeft;

    [Header("Visual")]
    [Tooltip("表示に使う三角形の3Dオブジェクト。直角部分がマスの角に合うように配置されます。")]
    [SerializeField] private GameObject visualPrefab;

    [Tooltip("1マスの大きさ。直角の角の位置を求めるのに使います。")]
    [SerializeField] private float cellSize = 1f;

    [Tooltip("配置後の微調整用オフセット（プレハブ側の原点のズレを吸収します）。")]
    [SerializeField] private Vector3 visualOffset = Vector3.zero;

    [Tooltip("プレハブに適用する追加の回転（度）。プレハブの向きが揃っていない場合に使います。")]
    [SerializeField] private Vector3 visualRotationOffset = Vector3.zero;

    [Tooltip("プレハブに適用するスケール。")]
    [SerializeField] private Vector3 visualScale = Vector3.one;

    // 生成した見た目のインスタンス。Corner を変えるたびに作り直します。
    private GameObject visualInstance;

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
        // OnValidate から直接 Instantiate / Destroy はできないため、エディタでは次のタイミングに回す。
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            RebuildVisual();
        };
#endif
    }

    /// <summary>
    /// 見た目のオブジェクトを作り直します。
    /// 直角の角がマスの角に来るように配置し、Corner に応じてY軸で90度ずつ回転させます。
    /// </summary>
    [ContextMenu("見た目を再構築")]
    public void RebuildVisual()
    {
        // 自前のメッシュ表示は使わないため、付いていれば非表示にする。
        MeshRenderer ownRenderer = GetComponent<MeshRenderer>();
        if (ownRenderer != null) ownRenderer.enabled = false;

        DestroyVisualInstance();

        if (visualPrefab == null) return;

        visualInstance = Instantiate(visualPrefab, transform);
        visualInstance.name = "TriangleWallVisual";
        visualInstance.hideFlags = HideFlags.DontSave;

        visualInstance.transform.localPosition = GetRightAngleCornerPosition() + visualOffset;
        visualInstance.transform.localRotation = Quaternion.Euler(visualRotationOffset) * GetCornerRotation();
        visualInstance.transform.localScale = visualScale;
    }

    private void DestroyVisualInstance()
    {
        if (visualInstance == null)
        {
            // 再コンパイル後などで参照が切れている場合に備え、子から探して消す。
            Transform existing = transform.Find("TriangleWallVisual");
            if (existing != null) visualInstance = existing.gameObject;
        }

        if (visualInstance == null) return;

        if (Application.isPlaying) Destroy(visualInstance);
        else DestroyImmediate(visualInstance);

        visualInstance = null;
    }

    /// <summary>
    /// 直角の角にあたるマスの角の位置（ローカル座標）を返します。
    /// </summary>
    private Vector3 GetRightAngleCornerPosition()
    {
        float half = cellSize * 0.5f;

        switch (corner)
        {
            case Corner.UpperRight: return new Vector3(half, 0f, half);
            case Corner.LowerRight: return new Vector3(half, 0f, -half);
            case Corner.LowerLeft: return new Vector3(-half, 0f, -half);
            default: return new Vector3(-half, 0f, half); // UpperLeft
        }
    }

    /// <summary>
    /// Corner に応じた回転を返します。UpperLeft を基準に、90度ずつ時計回りに回します。
    /// これは反射規則の回転（RotateClockwise）と同じ向きです。
    /// </summary>
    private Quaternion GetCornerRotation()
    {
        return Quaternion.Euler(0f, 90f * (int)corner, 0f);
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

        // 直角の角（UpperLeft なら左上）へ斜めに突っ込む方向は、
        // マスへ入ると塗りつぶし部分の内側に入ってしまうため、進入前に跳ね返す。
        // 角に正面から当たる形なので、来た方向へそのまま戻す。
        if (incoming == new Vector2Int(1, -1))
        {
            reflected = new Vector2Int(-1, 1);
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