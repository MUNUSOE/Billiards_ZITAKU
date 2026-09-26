using UnityEngine;
using UnityEngine.SceneManagement;

// NormalEnding/TrueEnding の各シーンに配置。映像・文章・Timelineの内容とは独立。
public class EndingSceneController : MonoBehaviour
{
    [SerializeField] private EndingKind endingKind = EndingKind.Normal;
    private bool completed;

    private void Start() { Time.timeScale = 1f; }

    // 任意の「ステージ選択へ」ボタン。閲覧済みはすでにシーン入場時に記録される。
    public void CompleteEnding()
    {
        if (completed || !EndingFlow.TryGetSettings(out var settings)) return;
        if ((endingKind != EndingKind.Normal && endingKind != EndingKind.True)
            || settings.SceneFor(endingKind) != gameObject.scene.name)
        {
            Debug.LogError("[Ending] このシーンとEnding Kindの設定が一致しません。");
            return;
        }
        if (!Application.CanStreamedLevelBeLoaded(settings.stageSelectScene))
        {
            Debug.LogError("[Ending] ステージ選択シーンがBuild Profileにありません。");
            return;
        }
        // 入場時にPendingが解除済みでも戻れる。再記録は安全に何度でも呼べる。
        if (settings.enableStoryMode && !EndingProgress.RecordEndingEntered(endingKind)) return;
        completed = true;
        Time.timeScale = 1f;
        SceneManager.LoadScene(settings.stageSelectScene);
    }
}
