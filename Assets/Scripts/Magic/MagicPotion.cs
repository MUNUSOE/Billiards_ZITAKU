using UnityEngine;

/// <summary>
/// 炎・水・風の使用回数を増やす盤面ポーションです。
/// InspectorのpotionTypeを変更すると、見た目の色と加算対象が自動で切り替わります。
/// </summary>
[RequireComponent(typeof(Collider))]
public class MagicPotion : MonoBehaviour
{
    [Header("Potion Settings")]
    [SerializeField] private MagicType potionType = MagicType.Fire;
    [Min(1)]
    [SerializeField] private int restoreAmount = 1;

    [Header("Visual Settings")]
    [SerializeField] private Renderer potionRenderer;
    [SerializeField] private Color firePotionColor = new Color(1f, 0.2f, 0.08f, 1f);
    [SerializeField] private Color waterPotionColor = new Color(0.08f, 0.55f, 1f, 1f);
    [SerializeField] private Color windPotionColor = new Color(0.25f, 1f, 0.35f, 1f);

    private bool collected;

    public MagicType PotionType => potionType;
    public int RestoreAmount => restoreAmount;

    private void Awake()
    {
        EnsureTriggerCollider();
        ApplyPotionColor();
    }

    private void OnValidate()
    {
        if (potionType == MagicType.None) potionType = MagicType.Fire;
        if (restoreAmount < 1) restoreAmount = 1;

        EnsureTriggerCollider();
        ApplyPotionColor();
    }

    private void OnTriggerEnter(Collider other)
    {
        // ポーションを回収できるのは主ボールだけ。ターゲット球は重なっても回収しない。
        if (other == null || other.GetComponentInParent<ShotBall>() == null) return;
        Collect();
    }

    /// <summary>
    /// 経路アニメーションと魔法スライドの終点から呼び出し、物理トリガーの有無にかかわらず回収します。
    /// </summary>
    public static void TryCollectAtBall(GameObject ball)
    {
        // 経路移動・水・風による移動を問わず、ターゲット球はポーションを取得しない。
        if (ball == null || ball.GetComponent<ShotBall>() == null) return;

        BallPath.GetBallSettings(ball, out float panelSize, out _, out _, out _);
        Vector3 cell = BallPath.SnapToGrid(ball.transform.position, panelSize);
        Collider[] hits = Physics.OverlapSphere(cell, panelSize * 0.4f);

        foreach (Collider hit in hits)
        {
            if (hit == null) continue;
            MagicPotion potion = hit.GetComponentInParent<MagicPotion>();
            if (potion != null)
            {
                potion.Collect();
            }
        }
    }

    private bool Collect()
    {
        if (collected || MagicManager.Instance == null) return false;
        if (!MagicManager.Instance.AddMagic(potionType, restoreAmount)) return false;

        collected = true;
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        Destroy(gameObject);
        return true;
    }

    private void EnsureTriggerCollider()
    {
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void ApplyPotionColor()
    {
        if (potionRenderer == null) potionRenderer = GetComponentInChildren<Renderer>();
        if (potionRenderer == null) return;

        Color color = potionType switch
        {
            MagicType.Water => waterPotionColor,
            MagicType.Wind => windPotionColor,
            _ => firePotionColor,
        };

        // 実行時（Game View）はインスタンス化した material を変更し、
        // 編集時（Scene View / Inspector）は sharedMaterial を安全に使用・更新する
        if (Application.isPlaying)
        {
            if (potionRenderer.material != null)
            {
                potionRenderer.material.color = color;
            }
        }
        else
        {
            if (potionRenderer.sharedMaterial != null)
            {
                potionRenderer.sharedMaterial.color = color;
            }
        }
    }
}