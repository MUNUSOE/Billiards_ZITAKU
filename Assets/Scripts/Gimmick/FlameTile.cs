using UnityEngine;

/// <summary>
/// 水魔法で消火するまで危険な炎マスです。
/// BallPath が移動先セルでこのコンポーネントを確認し、水なしの到達をゲームオーバーとして扱います。
/// </summary>
[RequireComponent(typeof(Collider))]
public sealed class FlameTile : MonoBehaviour
{
    [Header("Flame Tile Settings")]
    [Tooltip("水魔法で消火済みかどうかです。実行中に変更する必要はありません。")]
    [SerializeField] private bool extinguished = false;

    [Tooltip("消火時に非表示にする炎の見た目。未指定ならこのオブジェクトのRendererを無効化します。")]
    [SerializeField] private GameObject flameVisual;

    [Tooltip("消火時に無効化するCollider。未指定なら同じオブジェクトのColliderを無効化します。")]
    [SerializeField] private Collider flameCollider;

    public bool IsActiveFlame => !extinguished;

    // アクティブな FlameTile の総数を保持する静的変数
    private static int activeFlameCount = 0;

    private void Awake()
    {
        if (flameCollider == null) flameCollider = GetComponent<Collider>();
        ApplyExtinguishedState();
    }

    private void OnEnable()
    {
        if (!extinguished)
        {
            RegisterFlame();
        }
    }

    private void OnDisable()
    {
        if (!extinguished)
        {
            UnregisterFlame();
        }
    }

    private void OnValidate()
    {
        if (flameCollider == null) flameCollider = GetComponent<Collider>();
        ApplyExtinguishedState();
    }

    /// <summary>
    /// 炎マスを消火し、同一ショット後の移動でも通過可能な状態にします。
    /// </summary>
    public void Extinguish()
    {
        if (extinguished) return;

        extinguished = true;
        UnregisterFlame();
        ApplyExtinguishedState();
    }

    private static void RegisterFlame()
    {
        activeFlameCount++;
        if (activeFlameCount == 1)
        {
            // 画面上に最初の1個目の炎が現れたらループ再生を開始
            SoundManager.Instance?.PlayLoopSE(SEType.FrameTile);
        }
    }

    private static void UnregisterFlame()
    {
        activeFlameCount--;
        if (activeFlameCount <= 0)
        {
            activeFlameCount = 0;
            // 画面上の炎がすべて消火（または破棄）されたらループ停止
            SoundManager.Instance?.StopLoopSE();
        }
    }

    private void ApplyExtinguishedState()
    {
        if (flameVisual != null)
        {
            flameVisual.SetActive(!extinguished);
        }
        else
        {
            Renderer renderer = GetComponent<Renderer>();
            if (renderer != null) renderer.enabled = !extinguished;
        }

        if (flameCollider != null)
        {
            flameCollider.enabled = !extinguished;
        }
    }
}