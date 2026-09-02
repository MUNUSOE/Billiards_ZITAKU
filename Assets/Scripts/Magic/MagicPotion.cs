using UnityEngine;

/// <summary>
/// 炎・水・風の使用回数を増やす盤面ポーションです。
/// InspectorのpotionTypeを変更すると、対応するプレハブの見た目に自動で切り替わります。
/// X=60, Y=0, Z=0 の固定角度で表示し、位置オフセットの調整が可能です。
/// </summary>
[RequireComponent(typeof(Collider))]
public class MagicPotion : MonoBehaviour
{
    [Header("Potion Settings")]
    [SerializeField] private MagicType potionType = MagicType.Fire;
    [Min(1)]
    [SerializeField] private int restoreAmount = 1;

    [Header("Prefab Settings")]
    [Tooltip("炎ポーションの見た目用プレハブ")]
    [SerializeField] private GameObject firePotionPrefab;
    [Tooltip("水ポーションの見た目用プレハブ")]
    [SerializeField] private GameObject waterPotionPrefab;
    [Tooltip("風ポーションの見た目用プレハブ")]
    [SerializeField] private GameObject windPotionPrefab;

    [Header("Transform Settings")]
    [Tooltip("生成位置の微調整オフセット (X, Y, Z)")]
    [SerializeField] private Vector3 visualOffset = Vector3.zero;

    private static readonly Quaternion FixedRotation = Quaternion.Euler(60f, 0f, 0f);

    private bool collected;
    private GameObject currentVisualInstance;

    public MagicType PotionType => potionType;
    public int RestoreAmount => restoreAmount;

    private void Awake()
    {
        EnsureTriggerCollider();
        UpdatePotionModel();
    }

    private void OnValidate()
    {
        if (potionType == MagicType.None) potionType = MagicType.Fire;
        if (restoreAmount < 1) restoreAmount = 1;

        EnsureTriggerCollider();

        // エディタ上でパラメータ変更時に見た目をリアルタイム更新
        // （シーン上のオブジェクトのみ更新し、Project内のPrefabアセット内では実行しない）
#if UNITY_EDITOR
        if (!Application.isPlaying && !UnityEditor.PrefabUtility.IsPartOfPrefabAsset(this))
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null) UpdatePotionModel();
            };
        }
#endif
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

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySE(SEType.GetPotion);
        }

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

    /// <summary>
    /// 選択されている属性に応じてモデル（プレハブ）を入れ替えます。
    /// </summary>
    private void UpdatePotionModel()
    {
        // --- 1. 重複防止処理 ---
        if (currentVisualInstance != null)
        {
            CleanUpObject(currentVisualInstance);
            currentVisualInstance = null;
        }

        // 残ってしまった子オブジェクトをすべて検索して破棄
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.name.EndsWith("(Clone)") || child.name.Contains("Visual"))
            {
                CleanUpObject(child.gameObject);
            }
        }

        // --- 2. 新しいプレハブの生成とトランスフォーム設定 ---
        GameObject targetPrefab = potionType switch
        {
            MagicType.Water => waterPotionPrefab,
            MagicType.Wind => windPotionPrefab,
            _ => firePotionPrefab,
        };

        if (targetPrefab != null)
        {
            currentVisualInstance = Instantiate(targetPrefab, transform);
            currentVisualInstance.transform.localPosition = visualOffset;
            currentVisualInstance.transform.localRotation = FixedRotation;
        }
    }

    /// <summary>
    /// Playモード/編集モードに応じた適切なオブジェクト破棄処理
    /// </summary>
    private static void CleanUpObject(GameObject obj)
    {
        if (obj == null) return;

        if (Application.isPlaying)
        {
            Destroy(obj);
        }
        else
        {
            // エディタ実行時のアセット保護エラー回避フラグ（allowDestroyingAssets = true）を指定
            DestroyImmediate(obj, true);
        }
    }
}