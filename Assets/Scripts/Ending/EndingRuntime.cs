using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// 通常ステージの挑戦回数と、エンディングシーンに入った時点の閲覧済みを記録する。
public class EndingRuntime : MonoBehaviour
{
    private readonly HashSet<int> recordedScenes = new HashSet<int>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        var go = new GameObject("EndingRuntime");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<EndingRuntime>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnLoaded;
        SceneManager.sceneUnloaded += OnUnloaded;
    }
    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnLoaded;
        SceneManager.sceneUnloaded -= OnUnloaded;
    }
    private void OnUnloaded(Scene scene) => recordedScenes.Remove(scene.handle);
    private void OnLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!EndingFlow.TryGetSettings(out var settings)) return;
        if (!settings.enableStoryMode) return;
        // 完了ボタンやEndingSceneControllerがない空シーンでも入場だけで記録する。
        if (scene.name == settings.normalEndingScene)
        {
            EndingProgress.RecordEndingEntered(EndingKind.Normal);
            Time.timeScale = 1f;
            return;
        }
        if (scene.name == settings.trueEndingScene)
        {
            EndingProgress.RecordEndingEntered(EndingKind.True);
            Time.timeScale = 1f;
            return;
        }
        var entry = settings.FindScene(scene.name);
        if (entry == null || !recordedScenes.Add(scene.handle)) return;
        EndingProgress.RegisterEntry(entry.Id);
        Debug.Log("[Ending] " + entry.Id + " 挑戦回数=" + EndingProgress.Attempts(entry.Id));
    }
}
