using UnityEngine;

/// <summary>
/// ステージの獲得済み星の数（0〜3）を表示するUIパーツ。
/// starIcons に星アイコンのGameObjectを左から順番に3つ登録しておく。
/// </summary>
public class StarRatingView : MonoBehaviour
{
    [Tooltip("星アイコンを順番に登録する（通常3つ）。")]
    [SerializeField] private GameObject[] starIcons = new GameObject[3];

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
                starIcons[i].SetActive(i < clamped);
            }
        }
    }
}