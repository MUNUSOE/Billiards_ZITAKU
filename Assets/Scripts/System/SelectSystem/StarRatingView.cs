using UnityEngine;
using UnityEngine.UI; // ★ Imageの操作に必要

/// <summary>
/// ステージの獲得済み星の数（0〜3）を表示するUIパーツ。
/// starIcons に星アイコンのGameObjectを左から順番に3つ登録しておく。
/// </summary>
public class StarRatingView : MonoBehaviour
{
    [Tooltip("星アイコンを順番に登録する（通常3つ）。")]
    [SerializeField] private GameObject[] starIcons = new GameObject[3];

    [Header("Color Settings")]
    [Tooltip("獲得済みの星の色")]
    [SerializeField] private Color activeColor = Color.white;

    [Tooltip("未獲得の星の色（グレー）")]
    [SerializeField] private Color inactiveColor = new Color(0.3f, 0.3f, 0.3f, 1f); // 暗めのグレー

    /// <summary>
    /// 星の数を反映する。範囲外の値は自動的にクランプする。
    /// </summary>
    public void SetStarCount(int count)
    {
        if (starIcons == null) return;

        int clamped = Mathf.Clamp(count, 0, starIcons.Length);
        for (int i = 0; i < starIcons.Length; i++)
        {
            if (starIcons[i] != null)
            {
                // ★ オブジェクト自体は常に表示状態にする
                starIcons[i].SetActive(true);

                // ★ Imageコンポーネントを取得して、獲得状況に応じて色を切り替える
                Image starImage = starIcons[i].GetComponent<Image>();
                if (starImage != null)
                {
                    if (i < clamped)
                    {
                        starImage.color = activeColor; // 獲得済み
                    }
                    else
                    {
                        starImage.color = inactiveColor; // 未獲得
                    }
                }
            }
        }
    }
}