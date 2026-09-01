using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 本棚に手動配置する本ボタン1つ1つに付けておく参照用コンポーネント。
/// StageSelectManager から表紙画像・タイトルを反映するために使う。
/// （GetComponentInChildren だとボタン自身の背景Imageを誤って拾う可能性があるため、
///   参照を明示的にInspectorで結線する方式にしている。）
/// </summary>
public class BookShelfItemView : MonoBehaviour
{
    [SerializeField] private Image coverImage;
    [SerializeField] private TMP_Text titleText;
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
                // 表紙画像が未設定の本は、Image自体を無効化してデフォルトの白四角が出ないようにする。
                coverImage.enabled = false;
            }
        }
    }
}