using System.Collections;
using UnityEngine;

/// <summary>
/// 実際の左右のページ（RectTransform）を直接Y軸回転させて、
/// 本物のページがめくれるようなアニメーションを行います。
/// ダミーUIは使用せず、見た目が完全に維持されます。
/// </summary>
public class BookPageCurl : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float duration = 0.4f; // めくるスピード（秒）

    private bool isAnimating = false;
    public bool IsAnimating => isAnimating;

    // Pivot（回転軸）を位置をズレさせずに変更する便利関数
    private void SetPivotPreservingPosition(RectTransform rectTransform, Vector2 newPivot)
    {
        if (rectTransform == null) return;
        Vector2 size = rectTransform.rect.size;
        Vector2 deltaPivot = rectTransform.pivot - newPivot;
        Vector2 deltaPosition = new Vector2(deltaPivot.x * size.x, deltaPivot.y * size.y);
        rectTransform.pivot = newPivot;
        rectTransform.anchoredPosition -= deltaPosition;
    }

    /// <summary>
    /// 本物の左右ページを受け取り、3D回転アニメーションを行います。
    /// </summary>
    public IEnumerator PlayTurnAnimation(bool isNext, RectTransform leftPage, RectTransform rightPage, System.Action onHalfway)
    {
        if (isAnimating) yield break;
        isAnimating = true;

        // アニメーション後に元の状態に戻せるようPivotを保存
        Vector2 oldLeftPivot = leftPage != null ? leftPage.pivot : new Vector2(0.5f, 0.5f);
        Vector2 oldRightPivot = rightPage != null ? rightPage.pivot : new Vector2(0.5f, 0.5f);

        // 左ページは右端(1, 0.5)を軸に、右ページは左端(0, 0.5)を軸に設定
        if (leftPage != null) SetPivotPreservingPosition(leftPage, new Vector2(1f, 0.5f));
        if (rightPage != null) SetPivotPreservingPosition(rightPage, new Vector2(0f, 0.5f));

        float halfDuration = duration / 2f;

        if (isNext)
        {
            // 1. 右ページを 0度 -> 90度（奥へパタンと閉じるように）回転
            float elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                if (rightPage != null) rightPage.localRotation = Quaternion.Euler(0f, Mathf.Lerp(0f, 90f, t), 0f);
                yield return null;
            }
            if (rightPage != null) rightPage.localRotation = Quaternion.Euler(0f, 90f, 0f);

            // 2. ページが真横を向いて見えなくなった瞬間にデータを次へ更新
            onHalfway?.Invoke();

            // データ更新後、右ページは元に戻し、左ページを -90度から 0度（手前へ開くように）回転
            if (rightPage != null) rightPage.localRotation = Quaternion.identity;
            if (leftPage != null) leftPage.localRotation = Quaternion.Euler(0f, -90f, 0f);

            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                if (leftPage != null) leftPage.localRotation = Quaternion.Euler(0f, Mathf.Lerp(-90f, 0f, t), 0f);
                yield return null;
            }
            if (leftPage != null) leftPage.localRotation = Quaternion.identity;
        }
        else // isNext == false (前に戻る)
        {
            // 1. 左ページを 0度 -> -90度（奥へ）回転
            float elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                if (leftPage != null) leftPage.localRotation = Quaternion.Euler(0f, Mathf.Lerp(0f, -90f, t), 0f);
                yield return null;
            }
            if (leftPage != null) leftPage.localRotation = Quaternion.Euler(0f, -90f, 0f);

            // 2. ページが真横を向いて見えなくなった瞬間にデータを前へ更新
            onHalfway?.Invoke();

            // データ更新後、左ページは元に戻し、右ページを 90度から 0度（手前へ）回転
            if (leftPage != null) leftPage.localRotation = Quaternion.identity;
            if (rightPage != null) rightPage.localRotation = Quaternion.Euler(0f, 90f, 0f);

            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                if (rightPage != null) rightPage.localRotation = Quaternion.Euler(0f, Mathf.Lerp(90f, 0f, t), 0f);
                yield return null;
            }
            if (rightPage != null) rightPage.localRotation = Quaternion.identity;
        }

        // アニメーションが終わったらPivotを元に戻す
        if (leftPage != null) SetPivotPreservingPosition(leftPage, oldLeftPivot);
        if (rightPage != null) SetPivotPreservingPosition(rightPage, oldRightPivot);

        isAnimating = false;
    }
}