using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class DiagonalWall : MonoBehaviour
{
    public enum Orientation
    {
        BottomLeftToTopRight,
        TopLeftToBottomRight,
    }

    [Tooltip("上面図での反射面の向き。+Zを上、+Xを右として扱います。")]
    [SerializeField] private Orientation orientation = Orientation.BottomLeftToTopRight;

    [Tooltip("斜め線を表示する子オブジェクト。未指定でも反射判定は動作します。")]
    [SerializeField] private Transform visualSurface;

    public Orientation WallOrientation
    {
        get => orientation;
        set => orientation = value;
    }

    private void OnValidate()
    {
        if (visualSurface == null) return;

        // Local X-axis is the visible diagonal line on the XZ board plane.
        float yRotation = orientation == Orientation.BottomLeftToTopRight ? -45f : 45f;
        visualSurface.localRotation = Quaternion.Euler(0f, yRotation, 0f);
    }

    /// <summary>
    /// この壁の反射面へ向かう入力だけを反射対象にします。
    /// 反射面の反対側へ向かう入力は通常移動として扱います。
    /// </summary>
    public bool TryGetReflectionNormal(Vector3 incomingDirection, out Vector3 normal)
    {
        incomingDirection.y = 0f;
        if (incomingDirection.sqrMagnitude < 0.0001f)
        {
            normal = Vector3.zero;
            return false;
        }

        incomingDirection.Normalize();

        normal = orientation == Orientation.BottomLeftToTopRight
            ? new Vector3(1f, 0f, -1f).normalized
            : new Vector3(-1f, 0f, -1f).normalized;

        // The wall only reflects approaches from the face that points toward -normal.
        if (Vector3.Dot(incomingDirection, normal) >= -0.01f)
        {
            normal = Vector3.zero;
            return false;
        }

        return true;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(1f, 0.1f, 1f));
    }
#endif
}
