using System.Collections;
using UnityEngine;

public class Pocket : MonoBehaviour
{
    [Header("吸い込み演出の設定")]
    [SerializeField] private float duration = 0.3f;   // 吸い込まれる時間（秒）
    [SerializeField] private float sinkDepth = 0.5f;  // 下に沈む深さ

    private bool isPocketed = false; // 二重発動防止用

    // 現在ポケットへの吸い込み演出中の球の数。
    // 演出中はまだ球が Destroy されておらず、クリア判定が成立しません。
    // その隙にショットされると手数が減ってしまうため、
    // ShotBall 側はこの数が 0 でない間、操作を受け付けません。
    private static int activeSuckCount = 0;

    /// <summary>ポケットへの吸い込み演出中の球があるか。</summary>
    public static bool IsAnyBallBeingPocketed => activeSuckCount > 0;

    /// <summary>シーン切り替え時などにカウントをリセットします。</summary>
    public static void ResetPocketingState()
    {
        activeSuckCount = 0;
    }

    private void OnTriggerEnter(Collider other)
    {
        // 接触した相手のタグが "Pocket" かどうか判定
        if (other.CompareTag("Pocket") && !isPocketed)
        {
            isPocketed = true;
            StartCoroutine(SuckIntoPocketRoutine());
        }
    }

    private IEnumerator SuckIntoPocketRoutine()
    {
        // 演出が終わって Destroy されるまでの間、ショット操作を止める。
        activeSuckCount++;

        // 演出中に他の当たり判定が残らないようコライダーをオフにする
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        // 物理挙動がついていれば停止させる
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        Vector3 startScale = transform.localScale;
        Vector3 startPos = transform.position;
        Vector3 targetPos = startPos + Vector3.down * sinkDepth; // 少し下に落とす

        float elapsed = 0f;

        // スケールを0にしつつ下に沈ませるアニメーション
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
            transform.position = Vector3.Lerp(startPos, targetPos, t);

            yield return null;
        }

        // SEを鳴らす場合はここで呼び出し
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySE(SEType.Pocket);
        }

        // 演出が終わったのでカウントを戻す。
        // Destroy 直前に戻すことで、GameClear 側の消滅検知と入れ替わりに解除される。
        activeSuckCount = Mathf.Max(0, activeSuckCount - 1);

        // ボールを消去（非表示に留めたい場合は gameObject.SetActive(false); に変更）
        Destroy(gameObject);
    }
}