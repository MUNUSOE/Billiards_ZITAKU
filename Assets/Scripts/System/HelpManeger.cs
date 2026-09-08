using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ヘルプ画面の表示・非表示、およびメニュー項目の切り替えを管理するクラス
/// </summary>
public class HelpManager : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("ヘルプ画面全体のコンテナ（暗い背景＋ヘルプウィンドウ）")]
    [SerializeField] private GameObject helpContainer;

    [Tooltip("ヘルプを開くボタン（ヒントアイコン/電球）")]
    [SerializeField] private Button openButton;

    [Tooltip("ヘルプを閉じるボタン（✖アイコン）")]
    [SerializeField] private Button closeButton;

    [Header("Content Switching")]
    [Tooltip("左側のメニューボタンの配列")]
    [SerializeField] private Button[] menuButtons;

    [Tooltip("右側に表示する各項目のコンテンツパネル配列（menuButtonsとインデックスを合わせる）")]
    [SerializeField] private GameObject[] contentPanels;

    private void Start()
    {
        // ヘルプ画面の初期状態を非表示にする
        if (helpContainer != null)
        {
            helpContainer.SetActive(false);
        }

        // ボタンの表示状態を初期化（開くボタンを表示、閉じるボタンを非表示）
        if (openButton != null) openButton.gameObject.SetActive(true);
        if (closeButton != null) closeButton.gameObject.SetActive(false);

        // 基本ボタンのイベント登録
        if (openButton != null) openButton.onClick.AddListener(OpenHelp);
        if (closeButton != null) closeButton.onClick.AddListener(CloseHelp);

        // メニューボタンのクリックイベントを動的に登録
        for (int i = 0; i < menuButtons.Length; i++)
        {
            int index = i; // クロージャ（コールバック内での変数参照）のためにローカルコピーを作成
            if (menuButtons[i] != null)
            {
                menuButtons[i].onClick.AddListener(() => SwitchContent(index));
            }
        }
    }

    /// <summary>
    /// ヘルプ画面を開く
    /// </summary>
    public void OpenHelp()
    {
        if (helpContainer != null) helpContainer.SetActive(true);

        // 開くボタンを隠し、閉じるボタンを表示する
        if (openButton != null) openButton.gameObject.SetActive(false);
        if (closeButton != null) closeButton.gameObject.SetActive(true);

        // 開いた直後はすべてのコンテンツパネルを非表示にする（ボタンが押されるまで空っぽにする）
        if (contentPanels != null)
        {
            for (int i = 0; i < contentPanels.Length; i++)
            {
                if (contentPanels[i] != null)
                {
                    contentPanels[i].SetActive(false);
                }
            }
        }

        // 必要に応じてここで Time.timeScale = 0f; などを実行しゲームをポーズします
    }

    /// <summary>
    /// ヘルプ画面を閉じる
    /// </summary>
    public void CloseHelp()
    {
        if (helpContainer != null) helpContainer.SetActive(false);

        // 閉じるボタンを隠し、開くボタンを表示する
        if (openButton != null) openButton.gameObject.SetActive(true);
        if (closeButton != null) closeButton.gameObject.SetActive(false);

        // 必要に応じてここで Time.timeScale = 1f; などを実行しポーズを解除します
    }

    /// <summary>
    /// 選択されたインデックスのコンテンツのみを表示し、他を非表示にする
    /// </summary>
    private void SwitchContent(int index)
    {
        if (contentPanels == null) return;

        for (int i = 0; i < contentPanels.Length; i++)
        {
            if (contentPanels[i] != null)
            {
                // 一致するインデックスのパネルだけをアクティブにする
                contentPanels[i].SetActive(i == index);
            }
        }
    }
}