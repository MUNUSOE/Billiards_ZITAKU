using UnityEngine;

/// <summary>
/// BallPath（静的クラス）から使うエフェクトのプレハブを、Inspector で設定するためのコンポーネントです。
/// シーンに空のGameObjectを1つ作り、これをアタッチしてプレハブをドラッグ＆ドロップしてください。
///
/// 静的クラスは Inspector を持てないため、この MonoBehaviour が橋渡し役になります。
/// </summary>
public class MagicEffectSettings : MonoBehaviour
{
    public static MagicEffectSettings Instance { get; private set; }

    [Header("炎魔法：ターゲット球に命中したとき")]
    [Tooltip("炎魔法をまとったショット球が球に当たった瞬間に生成するエフェクト。")]
    [SerializeField] private GameObject fireHitEffectPrefab;

    [Tooltip("エフェクト位置のY軸オフセット。")]
    [SerializeField] private float fireHitEffectOffsetY = 0f;

    [Tooltip("生成したエフェクトを自動で消すまでの秒数。0以下なら自動で消しません。")]
    [SerializeField] private float fireHitEffectLifetime = 5f;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// 炎魔法の命中エフェクトを生成し、効果音を鳴らします。
    /// </summary>
    /// <param name="position">命中位置（ワールド座標）。</param>
    public void PlayFireHitEffect(Vector3 position)
    {
        if (fireHitEffectPrefab != null)
        {
            Vector3 effectPos = position + new Vector3(0f, fireHitEffectOffsetY, 0f);
            GameObject instance = Instantiate(fireHitEffectPrefab, effectPos, Quaternion.identity);

            if (fireHitEffectLifetime > 0f)
            {
                Destroy(instance, fireHitEffectLifetime);
            }
        }

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySE(SEType.UseFrame);
        }
    }
}
