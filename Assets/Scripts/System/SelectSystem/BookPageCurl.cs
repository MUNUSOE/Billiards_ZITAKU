using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// 本のページをめくる演出。呼び出し側は PlayCurlAnimation を yield return して
/// アニメーション完了を待つ。半分（90度）を越えたタイミングで onHalfway を呼ぶので、
/// 呼び出し側はそこでページの中身（表示ステージ）を裏側のデータに差し替える。
/// </summary>
public class BookPageCurl : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private RectTransform curlPageTransform; // めくる紙面用のRectTransform

    [Header("Front Face Content (めくれる紙自体に表示する内容)")]
    [Tooltip("めくれるページのタイトル表示。未設定なら何も表示しない。")]
    [SerializeField] private TMP_Text frontTitleText;
    [Tooltip("めくれるページのページ番号表示。")]
    [SerializeField] private TMP_Text frontPageNumberText;
    [Tooltip("めくれるページの星表示。")]
    [SerializeField] private StarRatingView frontStarRating;

    [Header("Settings")]
    [SerializeField] private float duration = 0.4f; // めくるスピード（秒）

    [Header("Curl Direction (Inspectorで調整可能)")]
    [Tooltip("Nextのときの回転軸（pivot）。")]
    [SerializeField] private Vector2 nextPivot = new Vector2(0f, 0.5f);
    [Tooltip("Prevのときの回転軸（pivot）。")]
    [SerializeField] private Vector2 prevPivot = new Vector2(0f, 0.5f);
    [Tooltip("Nextのときの開始Y角度。")]
    [SerializeField] private float nextStartAngle = 0f;
    [Tooltip("Nextのときの最終Y角度。向きが逆に感じる場合は符号を反転してみてください。")]
    [SerializeField] private float nextEndAngle = 180f;
    [Tooltip("Prevのときの開始Y角度。")]
    [SerializeField] private float prevStartAngle = -180f;
    [Tooltip("Prevのときの最終Y角度。-360を指定すると-180から-270経由でぐるっと0(=-360)まで回る。")]
    [SerializeField] private float prevEndAngle = -360f;

    private bool isAnimating = false;
    public bool IsAnimating => isAnimating;

    private void Awake()
    {
        if (curlPageTransform == null)
        {
            curlPageTransform = GetComponent<RectTransform>();
        }
        gameObject.SetActive(false);
    }

    /// <summary>
    /// curlPageTransform の見た目（アンカー・位置・サイズ）を、実際にめくれるべきページの
    /// RectTransform（sourcePageRect）に一致させる。これをやらないと、常に固定位置のまま
    /// 回転するだけになり、Prev（左ページ側）のときに見た目がおかしくなる。
    /// ※ sourcePageRect と curlPageTransform が同じ親（同じ座標基準）にある前提。
    /// </summary>
    private static void MatchRectTo(RectTransform target, RectTransform source)
    {
        if (target == null || source == null) return;

        target.anchorMin = source.anchorMin;
        target.anchorMax = source.anchorMax;
        target.pivot = source.pivot;
        target.anchoredPosition = source.anchoredPosition;
        target.sizeDelta = source.sizeDelta;
    }

    /// <summary>
    /// RectTransformのpivotだけを変更すると見た目の位置がズレてしまうため、
    /// 変更前後の差分をanchoredPositionで打ち消しながらpivotを切り替える。
    /// </summary>
    private static void SetPivotPreservingPosition(RectTransform rectTransform, Vector2 newPivot)
    {
        if (rectTransform == null) return;

        Vector2 size = rectTransform.rect.size;
        Vector2 deltaPivot = rectTransform.pivot - newPivot;
        Vector2 deltaPosition = new Vector2(deltaPivot.x * size.x, deltaPivot.y * size.y);

        rectTransform.pivot = newPivot;
        rectTransform.anchoredPosition -= deltaPosition;

        Debug.Log($"[BookPageCurl] SetPivotPreservingPosition newPivot={newPivot} deltaPosition={deltaPosition} 結果anchoredPosition={rectTransform.anchoredPosition}");
    }

    /// <summary>
    /// めくれるページ自身に、実際のページと同じ見た目（タイトル・ページ番号・星）を表示する。
    /// これをやらないと、中身が空っぽの板がただ回転するだけになる。
    /// </summary>
    private void ApplyFrontContent(StageData stage, int pageNumber)
    {
        bool hasStage = stage != null;

        if (frontTitleText != null)
        {
            frontTitleText.gameObject.SetActive(hasStage);
            if (hasStage) frontTitleText.text = stage.stageName;
        }

        if (frontPageNumberText != null)
        {
            frontPageNumberText.gameObject.SetActive(hasStage);
            if (hasStage) frontPageNumberText.text = $"- {pageNumber} -";
        }

        if (frontStarRating != null)
        {
            frontStarRating.gameObject.SetActive(hasStage);
            if (hasStage) frontStarRating.SetStarCount(stage.starCount);
        }
    }

    /// <summary>
    /// ページめくりアニメーションを再生する。
    /// </summary>
    /// <param name="isNext">Next(右から左へ)の場合はtrue、Prev(左から右へ)の場合はfalse</param>
    /// <param name="sourcePageRect">
    /// 実際にめくれるページのRectTransform（Nextなら右ページ、Prevなら左ページ）。
    /// 渡された場合、アニメーション開始前にcurlPageTransformの位置・サイズをこれに合わせる。
    /// </param>
    /// <param name="outgoingStage">めくれるページに表示中だったステージデータ（空きページならnull）。</param>
    /// <param name="outgoingPageNumber">めくれるページのページ番号表示用の数値。</param>
    /// <param name="onHalfway">ページが垂直（半分、90度）になったタイミングで呼ばれる処理</param>
    public IEnumerator PlayCurlAnimation(bool isNext, RectTransform sourcePageRect, StageData outgoingStage, int outgoingPageNumber, System.Action onHalfway)
    {
        Debug.Log($"[BookPageCurl] PlayCurlAnimation 開始 isNext={isNext} isAnimating={isAnimating} sourcePageRect={(sourcePageRect != null ? sourcePageRect.name : "null")} duration={duration}");

        if (isAnimating)
        {
            Debug.Log("[BookPageCurl] 既にアニメーション中のため何もせず終了");
            yield break;
        }
        isAnimating = true;

        gameObject.SetActive(true);

        if (curlPageTransform != null && sourcePageRect != null)
        {
            MatchRectTo(curlPageTransform, sourcePageRect);
        }

        ApplyFrontContent(outgoingStage, outgoingPageNumber);

        // 綴じ目（回転軸）は Inspector の Next Pivot / Prev Pivot で調整する。
        Vector2 pivot = isNext ? nextPivot : prevPivot;
        if (curlPageTransform != null)
        {
            SetPivotPreservingPosition(curlPageTransform, pivot);
        }
        else
        {
            Debug.LogWarning("[BookPageCurl] curlPageTransform が null です。回転できません。");
        }

        float startAngle = isNext ? nextStartAngle : prevStartAngle;
        float endAngle = isNext ? nextEndAngle : prevEndAngle;

        float elapsed = 0f;
        bool halfwayTriggered = false;
        int frameCount = 0;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            frameCount++;
            float t = Mathf.Clamp01(elapsed / duration);

            // イージング（SmoothStep）
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            // Y軸回転
            float currentAngle = Mathf.Lerp(startAngle, endAngle, smoothT);
            if (curlPageTransform != null)
            {
                curlPageTransform.localRotation = Quaternion.Euler(0f, currentAngle, 0f);
            }

            if (frameCount % 30 == 0)
            {
                Debug.Log($"[BookPageCurl] 経過中 frame={frameCount} elapsed={elapsed:F3} t={t:F2} angle={currentAngle:F1} unscaledDeltaTime={Time.unscaledDeltaTime:F4}");
            }

            // ちょうど半分（90度）を越えたタイミングで裏のページデータに切り替える。
            if (!halfwayTriggered && t >= 0.5f)
            {
                halfwayTriggered = true;
                Debug.Log("[BookPageCurl] halfway到達。onHalfway を呼びます。");
                try
                {
                    onHalfway?.Invoke();
                    Debug.Log("[BookPageCurl] onHalfway 正常終了。");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[BookPageCurl] onHalfway で例外発生: {e}");
                    // ここで再スローせず、アニメーション自体は最後まで続行してisAnimatingを正しく戻す。
                }
            }

            yield return null;

            if (frameCount > 100000)
            {
                Debug.LogError("[BookPageCurl] ループが異常に長引いています。強制終了します。elapsed/duration の値を確認してください。");
                break;
            }
        }

        Debug.Log($"[BookPageCurl] ループ終了 総frame={frameCount} halfwayTriggered={halfwayTriggered}");

        // 回転のリセットと非表示化
        if (curlPageTransform != null)
        {
            curlPageTransform.localRotation = Quaternion.identity;
        }
        gameObject.SetActive(false);

        isAnimating = false;
        Debug.Log("[BookPageCurl] PlayCurlAnimation 完了。isAnimating=false");
    }
}