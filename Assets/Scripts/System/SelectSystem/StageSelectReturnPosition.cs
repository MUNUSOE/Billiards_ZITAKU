using UnityEngine;
using UnityEngine.SceneManagement;

// ステージ選択へ戻る位置だけを、一時的に記録する。セーブ・ストーリー進行とは独立。
public static class StageSelectReturnPosition
{
    private static string lastStageScene;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        lastStageScene = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 装飾や共通UIの追加読み込みで、戻り先を上書きしない。
        if (mode != LoadSceneMode.Single) return;

        bool isStage = false;
        foreach (var root in scene.GetRootGameObjects())
        {
            // Startで記録を受け取るまで保持する。
            if (root.GetComponentInChildren<StageSelectManager>(true) != null) return;
            if (root.GetComponentInChildren<StageInfo>(true) != null) isStage = true;
        }

        var settings = EndingFlow.Settings;
        if (settings != null)
        {
            if (scene.name == settings.stageSelectScene) return;
            // 最終ステージ→エンディング→選択画面でも最終ステージへ戻す。
            if (scene.name == settings.normalEndingScene || scene.name == settings.trueEndingScene) return;
            if (settings.storyStages != null
                && settings.storyStages.Exists(s => s != null && s.sceneName == scene.name)) isStage = true;
            if (settings.extraStageScenes != null && settings.extraStageScenes.Contains(scene.name)) isStage = true;
            if (settings.IsTutorial(scene.name)) isStage = true;
        }
        if (EndingSettings.HasTutorialName(scene.name)) isStage = true;

        // タイトルなど別の画面を経由した場合は先頭へ戻す。
        lastStageScene = isStage ? scene.name : null;
    }

    public static string ConsumeSceneName()
    {
        string scene = lastStageScene;
        lastStageScene = null;
        return scene;
    }
}
