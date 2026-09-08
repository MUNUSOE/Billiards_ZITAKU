using System.Collections;
using UnityEngine;
using UnityEngine.UI; // レガシーTextに必要
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
    [Tooltip("めくれるページの最速クリア表示(レガシーText)。")]
    [SerializeField] private Text frontFastestClearText; // ★レガシーTextに変更

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

    private static void MatchRectTo(RectTransform target, RectTransform source)
    {
        if (target == null || source == null) return;

        target.anchorMin = source.anchorMin;
        target.anchorMax = source.anchorMax;
        target.pivot = source.pivot;
        target.anchoredPosition = source.anchoredPosition;
        target.sizeDelta = source.sizeDelta;
    }

    private static void SetPivotPreservingPosition(RectTransform rectTransform, Vector2 newPivot)
    {
        if (rectTransform == null) return;

        Vector2 size = rectTransform.rect.size;
        Vector2 deltaPivot = rectTransform.pivot - newPivot;
        Vector2 deltaPosition = new Vector2(deltaPivot.x * size.x, deltaPivot.y * size.y);

        rectTransform.pivot = newPivot;
        rectTransform.anchoredPosition -= deltaPosition;
    }

    /// <summary>
    /// めくれるページ自身に、実際のページと同じ見た目（タイトル・ページ番号・星・最速表示）を表示する。
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

        // ★最速クリア表示の設定（レガシーText）
        if (frontFastestClearText != null)
        {
            if (hasStage)
            {
                bool isFastest = StageResult.IsFastestAchieved(stage.stageId, stage.parMoves);
                frontFastestClearText.gameObject.SetActive(isFastest);
            }
            else
            {
                frontFastestClearText.gameObject.SetActive(false);
            }
        }
    }

    public IEnumerator PlayCurlAnimation(bool isNext, RectTransform sourcePageRect, StageData outgoingStage, int outgoingPageNumber, System.Action onHalfway)
    {
        if (isAnimating) yield break;
        isAnimating = true;

        gameObject.SetActive(true);

        if (curlPageTransform != null && sourcePageRect != null)
        {
            MatchRectTo(curlPageTransform, sourcePageRect);
        }

        ApplyFrontContent(outgoingStage, outgoingPageNumber);

        Vector2 pivot = isNext ? nextPivot : prevPivot;
        if (curlPageTransform != null)
        {
            SetPivotPreservingPosition(curlPageTransform, pivot);
        }

        float startAngle = isNext ? nextStartAngle : prevStartAngle;
        float endAngle = isNext ? nextEndAngle : prevEndAngle;

        float elapsed = 0f;
        bool halfwayTriggered = false;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            float currentAngle = Mathf.Lerp(startAngle, endAngle, smoothT);
            if (curlPageTransform != null)
            {
                curlPageTransform.localRotation = Quaternion.Euler(0f, currentAngle, 0f);
            }

            if (!halfwayTriggered && t >= 0.5f)
            {
                halfwayTriggered = true;
                onHalfway?.Invoke();
            }

            yield return null;
        }

        if (curlPageTransform != null)
        {
            curlPageTransform.localRotation = Quaternion.identity;
        }
        gameObject.SetActive(false);

        isAnimating = false;
    }
}