using System;
using System.Collections.Generic;
using UnityEngine;

public enum EndingKind { None = 0, Normal = 1, True = 2 }

[CreateAssetMenu(menuName = "MagiArchv/Ending Settings", fileName = "EndingSettings")]
public class EndingSettings : ScriptableObject
{
    [Serializable]
    public class StoryStage
    {
        public string sceneName;
        [Tooltip("StageInfo.StageId と一致。空欄なら sceneName。")]
        public string stageId;
        [Min(1)] public int parMoves = 3;
        public string Id => string.IsNullOrWhiteSpace(stageId) ? sceneName : stageId;
    }

    [Header("Story Mode")]
    [Tooltip("ON: 順次解放とエンディング機能が有効。OFF: 全ステージ解放、エンディング判定・自動遷移・挑戦回数等のストーリー記録を停止。通常のクリア記録・星は保存します。")]
    [UnityEngine.Serialization.FormerlySerializedAs("enableStageLocks")]
    public bool enableStoryMode = true;

    [Tooltip("通常ステージだけを解放順に全件登録。チュートリアル・エクストラは除外。先頭は最初からプレイ可能。")]
    public List<StoryStage> storyStages = new List<StoryStage>();
    public string finalStageId;
    public string normalEndingScene = "NormalEnding";
    public string trueEndingScene = "TrueEnding";
    public string stageSelectScene = "StageSelect";
    public List<string> extraStageScenes = new List<string>();

    [Header("Build Profile Import")]
    [Tooltip("自動取り込みから除外するシーン名。数字-数字形式のチュートリアル等を指定します。Extra Stage Scenesにあるシーンは自動で除外されます。")]
    public List<string> autoImportExcludedScenes = new List<string>();

    public StoryStage FindScene(string name) => storyStages.Find(s => s != null && s.sceneName == name);
    public StoryStage FindStage(string id) => storyStages.Find(s => s != null && s.Id == id);
    public bool IsExtra(string name) => extraStageScenes.Contains(name);
    public string SceneFor(EndingKind kind) => kind == EndingKind.True ? trueEndingScene : normalEndingScene;

    public bool Validate(out string error)
    {
        error = null;
        var ids = new HashSet<string>();
        var scenes = new HashSet<string>();
        if (storyStages == null || storyStages.Count == 0) { error = "通常ステージ一覧が空です。"; return false; }
        foreach (var s in storyStages)
        {
            if (s == null || string.IsNullOrWhiteSpace(s.sceneName) || string.IsNullOrWhiteSpace(s.Id)
                || s.parMoves < 1 || !ids.Add(s.Id) || !scenes.Add(s.sceneName))
            { error = "通常ステージに空欄・ID/シーン名の重複・不正な最短手数があります。"; return false; }
        }
        if (!ids.Contains(finalStageId)) { error = "Final Stage Id を通常ステージ一覧のIDに合わせてください。"; return false; }
        if (string.IsNullOrWhiteSpace(normalEndingScene) || string.IsNullOrWhiteSpace(trueEndingScene)
            || string.IsNullOrWhiteSpace(stageSelectScene) || normalEndingScene == trueEndingScene
            || normalEndingScene == stageSelectScene || trueEndingScene == stageSelectScene
            || scenes.Contains(normalEndingScene) || scenes.Contains(trueEndingScene) || scenes.Contains(stageSelectScene))
        { error = "エンディング/ステージ選択のシーン名を、通常ステージと重複しない名前にしてください。"; return false; }
        if (extraStageScenes == null) { error = "Extra Stage Scenes が未設定です。"; return false; }
        var extra = new HashSet<string>();
        foreach (string name in extraStageScenes)
        {
            if (string.IsNullOrWhiteSpace(name) || !extra.Add(name) || scenes.Contains(name)
                || name == normalEndingScene || name == trueEndingScene || name == stageSelectScene)
            { error = "エクストラのシーン名が空、または重複しています。"; return false; }
        }
        return true;
    }

#if UNITY_EDITOR
    [ContextMenu("★ Build ProfileからStory Stagesを取り込む")]
    private void ImportStoryStagesFromBuildProfile()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("[EndingSettings] Playモードを終了してから取り込んでください。", this);
            return;
        }

        // Inspectorの一時オブジェクトや未保存のインスタンスでは実行しない。
        // hideFlagsを変更して保存可能にするのではなく、実体のある設定アセットを選び直す。
        if (!UnityEditor.AssetDatabase.Contains(this)
            || (hideFlags & HideFlags.DontSaveInEditor) != 0)
        {
            Debug.LogWarning("[EndingSettings] ProjectウィンドウのEndingSettings.assetを選択して実行してください。一時オブジェクトからは取り込めません。", this);
            return;
        }

        // 既存StageSelectManagerと同じく、有効な「章-番号」形式のシーンだけを対象にする。
        var candidates = new List<KeyValuePair<string, Vector2Int>>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        var scenePaths = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var scene in UnityEditor.EditorBuildSettings.scenes)
        {
            if (!scene.enabled) continue;
            string name = System.IO.Path.GetFileNameWithoutExtension(scene.path);
            if (name == normalEndingScene || name == trueEndingScene || name == stageSelectScene
                || (extraStageScenes != null && extraStageScenes.Contains(name))
                || (autoImportExcludedScenes != null && autoImportExcludedScenes.Contains(name))) continue;

            var match = System.Text.RegularExpressions.Regex.Match(name, @"^(\d+)-(\d+)$");
            if (!match.Success
                || !int.TryParse(match.Groups[1].Value, out int chapter)
                || !int.TryParse(match.Groups[2].Value, out int number)) continue;
            if (!names.Add(name))
            {
                Debug.LogError("[EndingSettings] 同名シーンが複数あります。取り込みを中止しました: " + name, this);
                return;
            }
            candidates.Add(new KeyValuePair<string, Vector2Int>(name, new Vector2Int(chapter, number)));
            scenePaths.Add(name, scene.path);
        }

        if (candidates.Count == 0)
        {
            Debug.LogWarning("[EndingSettings] 対象シーンがありません。既存のStory Stagesは変更しません。Build Profileの有効なシーン名と除外設定を確認してください。", this);
            return;
        }
        candidates.Sort((a, b) =>
        {
            int comparison = a.Value.x.CompareTo(b.Value.x);
            if (comparison == 0) comparison = a.Value.y.CompareTo(b.Value.y);
            return comparison != 0 ? comparison : string.CompareOrdinal(a.Key, b.Key);
        });

        var existing = new Dictionary<string, StoryStage>(StringComparer.Ordinal);
        if (storyStages != null)
            foreach (var stage in storyStages)
                if (stage != null && !string.IsNullOrWhiteSpace(stage.sceneName)
                    && !existing.ContainsKey(stage.sceneName)) existing.Add(stage.sceneName, stage);

        string oldFinalScene = null;
        foreach (var item in existing.Values)
            if (item.Id == finalStageId) { oldFinalScene = item.sceneName; break; }

        // 全シーンを読めた場合だけ一覧を入れ替える。読み取り失敗で部分的な一覧を保存しない。
        var imported = new List<StoryStage>();
        var importedIds = new HashSet<string>(StringComparer.Ordinal);
        int added = 0;
        try
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                string name = candidates[i].Key;
                if (UnityEditor.EditorUtility.DisplayCancelableProgressBar(
                    "Story Stages / Par Moves 取り込み", name, (float)i / candidates.Count))
                {
                    Debug.Log("[EndingSettings] 取り込みを中止しました。既存の一覧は変更していません。", this);
                    return;
                }
                if (!TryReadStageInfo(scenePaths[name], name, out StoryStage stage, out string error))
                {
                    Debug.LogError("[EndingSettings] " + error + " 既存の一覧は変更していません。", this);
                    return;
                }
                if (!importedIds.Add(stage.Id))
                {
                    Debug.LogError("[EndingSettings] Stage Idが重複しています: " + stage.Id
                        + "。既存の一覧は変更していません。", this);
                    return;
                }
                imported.Add(stage);
                if (!existing.ContainsKey(name)) added++;
            }
        }
        finally
        {
            UnityEditor.EditorUtility.ClearProgressBar();
        }

        UnityEditor.Undo.RecordObject(this, "Import Story Stages From Build Profile");
        storyStages = imported;
        // 指定済みの最終シーンは維持し、そのシーンのID上書きが変わった場合だけ追従する。
        var finalEntry = oldFinalScene == null ? null : FindScene(oldFinalScene);
        if (finalEntry != null) finalStageId = finalEntry.Id;
        else if (string.IsNullOrWhiteSpace(finalStageId)) finalStageId = storyStages[storyStages.Count - 1].Id;
        UnityEditor.EditorUtility.SetDirty(this);
        // InspectorのGUIイベント中に全アセットを一括保存しない。
        // 変更はUndoとSetDirtyで記録。取り込み後にFile > Save Projectで保存する。

        Debug.Log("[EndingSettings] Story Stagesを" + storyStages.Count + "件取り込みました（新規" + added
            + "件）。StageInfoのPar MovesとStage Idを反映しました。Final Stage Idも確認してください。", this);
        if (FindStage(finalStageId) == null)
            Debug.LogWarning("[EndingSettings] 指定中のFinal Stage Idが取り込み結果にありません。Inspectorで修正してください。", this);
    }

    private static bool TryReadStageInfo(string scenePath, string sceneName,
        out StoryStage result, out string error)
    {
        result = null;
        error = null;
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scenePath);
        bool openedPreview = false;
        try
        {
            // 開いているシーンは現在の編集内容を読む。閉じているシーンは保存済みファイルを読む。
            if (!scene.IsValid() || !scene.isLoaded)
            {
                scene = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene(scenePath);
                openedPreview = true;
            }
            if (!scene.IsValid() || !scene.isLoaded)
            {
                error = sceneName + ": シーンを読み込めません。";
                return false;
            }
            var infos = new List<StageInfo>();
            foreach (var root in scene.GetRootGameObjects())
                infos.AddRange(root.GetComponentsInChildren<StageInfo>(true));
            if (infos.Count != 1)
            {
                error = sceneName + ": StageInfoが" + infos.Count + "個あります。各通常ステージに1個配置してください。";
                return false;
            }
            // StageInfo.StageIdはアクティブシーンを参照するため、ID上書きの保存値を直接読む。
            // シーン内オブジェクトそのものは設定アセットへ保存しない。
            var serializedInfo = new UnityEditor.SerializedObject(infos[0]);
            serializedInfo.Update();
            var par = serializedInfo.FindProperty("parMoves");
            var id = serializedInfo.FindProperty("stageIdOverride");
            if (par == null || id == null || par.intValue < 1)
            {
                error = sceneName + ": StageInfoのparMoves / stageIdOverride、または最短手数を確認してください。";
                return false;
            }
            result = new StoryStage
            {
                sceneName = sceneName,
                stageId = id.stringValue,
                parMoves = par.intValue
            };
            return true;
        }
        catch (Exception exception)
        {
            error = sceneName + ": " + exception.Message;
            return false;
        }
        finally
        {
            if (openedPreview && scene.IsValid())
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
        }
    }
#endif
}
