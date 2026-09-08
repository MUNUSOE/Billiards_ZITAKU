using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 本棚に手動配置する本ボタン1つ1つに付けておく参照用コンポーネント。
/// StageSelectManager から表紙画像・タイトルを反映するために使う。
/// </summary>
public class BookShelfItemView : MonoBehaviour
{
    [SerializeField] private Image coverImage;
    [SerializeField] private Text titleText;
    [SerializeField] private Button button;

    public Button Button => button;

    public void SetData(BookData book)
    {
        if (book == null) return;

        if (titleText != null)
        {
            titleText.text = book.bookTitle;
        }

        if (coverImage != null)
        {
            if (book.bookCover != null)
            {
                coverImage.sprite = book.bookCover;
                coverImage.enabled = true;
            }
            else
            {
                coverImage.enabled = false;
            }
        }
    }
}