using UnityEngine;
using UnityEngine.UI; // Imageのコンポーネントを操作する場合

public class ItemCollector : MonoBehaviour
{
    [Header("UI設定")]
    [SerializeField] private GameObject itemImageUI; // 表示させるアイテムUI（ImageのGameObject）

    [Header("ステータス")]
    [SerializeField] private int itemCount = 0; // 取得したアイテムの数

    // 他のスクリプトから取得数を確認したい場合用のプロパティ
    public int ItemCount => itemCount;

    private void Start()
    {
        // ゲーム開始時はUIを非表示にしておく
        if (itemImageUI != null)
        {
            itemImageUI.SetActive(false);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // 接触したオブジェクトのタグが "Item" かどうか判定
        if (other.CompareTag("Item"))
        {
            // 1. カウントを増やす
            itemCount++;

            // 2. アイテムUIを表示する
            if (itemImageUI != null)
            {
                itemImageUI.SetActive(true);
            }

            // 3. 拾ったフィールド上のアイテムを消去する
            Destroy(other.gameObject);

            Debug.Log($"アイテムを取得しました！ 現在の所持数: {itemCount}");
        }
    }
}