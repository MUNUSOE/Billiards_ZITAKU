using System.Collections;
using UnityEngine;

public class Pocket : MonoBehaviour
{
    [Header("吸い込み演出の設定")]
    [SerializeField] private float duration = 0.3f;   // 吸い込まれる時間（秒）
    [SerializeField] private float sinkDepth = 0.5f;  // 下に沈む深さ

    private bool isPocketed = false; // 二重発動防止用

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

        // ボールを消去（非表示に留めたい場合は gameObject.SetActive(false); に変更）
        Destroy(gameObject);
    }
}