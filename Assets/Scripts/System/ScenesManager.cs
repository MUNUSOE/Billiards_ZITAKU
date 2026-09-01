using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// UIボタンから指定シーンへ遷移する。
/// クリア画面・ゲームオーバー画面のどちらから遷移しても、停止中の時間を再開する。
/// </summary>
[RequireComponent(typeof(Button))]
public class ScenesManager : MonoBehaviour
{
    [Header("Scene Settings")]
    [Tooltip("Build Settings に登録済みの遷移先シーン名。")]
    [SerializeField] private string sceneName;

    [Tooltip("連打防止と遷移演出用の待機秒数。")]
    [SerializeField, Min(0f)] private float cooldownTime = 1.0f;

    private Button button;
    private bool isTransitioning;

    private void Awake()
    {
        button = GetComponent<Button>();
    }

    private void OnEnable()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        button.onClick.AddListener(OnButtonClicked);
    }

    private void OnDisable()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OnButtonClicked);
        }
    }

    private void OnButtonClicked()
    {
        if (isTransitioning)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning($"[{gameObject.name}] 遷移先シーン名が設定されていません。");
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogWarning($"[{gameObject.name}] シーン '{sceneName}' は Build Settings に登録されていません。");
            return;
        }

        StartCoroutine(ChangeSceneRoutine());
    }

    private IEnumerator ChangeSceneRoutine()
    {
        isTransitioning = true;
        button.interactable = false;

        // クリア・ゲームオーバー・オプション画面からの遷移でも停止状態を持ち越さない。
        Time.timeScale = 1f;

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySE(SEType.DecideButton);
        }

        if (cooldownTime > 0f)
        {
            yield return new WaitForSecondsRealtime(cooldownTime);
        }

        SceneManager.LoadScene(sceneName);
    }
}