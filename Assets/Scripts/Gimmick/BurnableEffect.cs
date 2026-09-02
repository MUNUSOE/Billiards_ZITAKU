using UnityEngine;

/// <summary>
/// 木箱（Burnableタグ）に付けて、炎魔法で破壊されたときのエフェクトを再生します。
/// エフェクトのプレハブは Inspector にドラッグ＆ドロップで設定してください。
///
/// 破壊のタイミングは BallPath.PathPoint.ApplyPendingEffects から呼ばれます。
/// 木箱自体は直後に Destroy されるため、エフェクトは木箱の子ではなく
/// ワールド上に独立して生成します。
/// </summary>
public class BurnableEffect : MonoBehaviour
{
    [Header("Destroy Effect")]
    [Tooltip("破壊時に生成するエフェクトのプレハブ。未設定ならエフェクトは出ません。")]
    [SerializeField] private GameObject destroyEffectPrefab;

    [Tooltip("エフェクトを出す位置の、木箱からのオフセット。")]
    [SerializeField] private Vector3 effectOffset = Vector3.zero;

    [Tooltip("生成したエフェクトを自動で消すまでの秒数。0以下なら自動で消しません（パーティクル側のStop Actionに任せる場合は0にしてください）。")]
    [SerializeField] private float effectLifetime = 2f;

    [Tooltip("エフェクトを木箱の回転に合わせるか。falseなら回転なしで生成します。")]
    [SerializeField] private bool matchRotation = false;

    /// <summary>
    /// 破壊エフェクトを生成します。木箱が Destroy される直前に呼ばれます。
    /// </summary>
    public void PlayDestroyEffect()
    {
        if (destroyEffectPrefab == null) return;

        Vector3 position = transform.position + effectOffset;
        Quaternion rotation = matchRotation ? transform.rotation : Quaternion.identity;

        GameObject effect = Instantiate(destroyEffectPrefab, position, rotation);

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySE(SEType.DestroyBox);
        }

        if (effectLifetime > 0f)
        {
            Destroy(effect, effectLifetime);
        }
    }
}
