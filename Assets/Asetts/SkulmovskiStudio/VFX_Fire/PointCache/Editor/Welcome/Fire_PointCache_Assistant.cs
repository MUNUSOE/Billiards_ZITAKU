// -----------------------------------------------------------------------------
// FirePointCacheAssistant.cs
//
// Copyright (c) [2026] Skulmovski Studio. All rights reserved.
// [Skulmovski Studio / https://skulmovski.studio/]
//
// Redistribution or resale outside of its original distribution channel is
// not permitted without written permission from Skulmovski Studio.
// -----------------------------------------------------------------------------


using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace SkulmovskiStudio.VFX_Fire.PointCache.Editor.Welcome
{
    public class FirePointCacheAssistant : SkulmovskiAssistantWindowBase
    {
        private const string
            CurrentVFXFolder = "Assets/SkulmovskiStudio/VFX_Fire/PointCache/"; // Assets/SkulmovskiStudio/Welcome/

        private const string ToolsFolder = "Tools/Skulmovski Studio/"; // Tools/Skulmovski Studio/

        private const string
            AssetName = "Fire VFX — Wraps Any Mesh · Point Cache · URP"; // Rain VFX · Top-Down Masking · URP

        private const string SetupAssistantName = "VFX Setup Assistant"; // VFX Setup Assistant
        private const string DemoScenePath = CurrentVFXFolder + "Demo/Scenes/FirePointCacheURPDemoScene.unity";

        [InitializeOnLoadMethod]
        private static void AutoShowOnFirstImport()
        {
            ShowOnFirstImport<FirePointCacheAssistant>(AssetName + " — " + SetupAssistantName);
        }

        [MenuItem(ToolsFolder + AssetName + "/" + SetupAssistantName, false, 1)]
        private static void Open()
        {
            OpenWindow<FirePointCacheAssistant>(AssetName + " — " + SetupAssistantName);
        }

        [MenuItem(ToolsFolder + AssetName + "/Enable Assistant", false, 100)]
        private static void EnableThisAssistant() => SetGloballyDisabled<FirePointCacheAssistant>(false);

        [MenuItem(ToolsFolder + AssetName + "/Disable Assistant", false, 101)]
        private static void DisableThisAssistant() => SetGloballyDisabled<FirePointCacheAssistant>(true);

        protected override string AssetTitle => AssetName;
        protected override string AssetTagline => SetupAssistantName;
        protected override string DocsPdfPath => CurrentVFXFolder + "/Documentation.pdf";

        private static string SafeModeAcknowledgedKey => "SkulmovskiStudio.AssistantWindow.SafeModeAcknowledged." +
                                                         typeof(FirePointCacheAssistant).FullName + "." +
                                                         Application.dataPath;

        private static bool IsSafeModeAcknowledged
        {
            get => EditorPrefs.GetBool(SafeModeAcknowledgedKey, false);
            set => EditorPrefs.SetBool(SafeModeAcknowledgedKey, value);
        }


        protected override List<ChecklistSection> BuildChecklist()
        {
            return new List<ChecklistSection>
            {
                // PROJECT-WIDE SETTINGS
                new("Project-Wide Settings", new List<ChecklistItem>
                {
                    ChecklistItem.ManualCheck(
                        "Active Pipeline is URP",
                        "This package targets Universal Render Pipeline only",
                        EvaluatePipelineIsUrp),

                    ChecklistItem.Check(
                        "Visual Effect Graph Package Installed",
                        "The Visual Effect Graph package is strictly required " +
                        "to simulate and render the particles",
                        () => HasPackage("com.unity.visualeffectgraph") ? ChecklistStatus.Ok : ChecklistStatus.Error,
                        OpenPackageManagerForVfxGraph,
                        "Open Package Manager"),

                    ChecklistItem.Check(
                        "Depth Texture Enabled",
                        "URP Asset must have Depth Texture enabled",
                        EvaluateDepthTexture,
                        EnableDepthTextureWithConfirmation,
                        "Enable Depth Texture"),

                    ChecklistItem.Check(
                        "Opaque Texture Enabled",
                        "URP Asset must have Opaque Texture enabled",
                        EvaluateOpaqueTexture,
                        EnableOpaqueTextureWithConfirmation,
                        "Enable Opaque Texture"),
                }, showProgress: true),

                // SCENE
                new("Scene Settings (checks the currently open scene)", new List<ChecklistItem>
                {
                    ChecklistItem.Check(
                        "New VFX Instances Start in \"Safe Mode\"",
                        "When adding a new raw VFX Graph to a scene (unlike our pre-configured prefabs)," +
                        " it spawns only 1 particle/sec by default to protect your GPU on dense meshes. " +
                        "This is expected behavior — adjust spawn rates in the Inspector to bring it to life." +
                        "\nSee \"Default Safe Mode\" in the docs.",
                        () => IsSafeModeAcknowledged ? ChecklistStatus.Ok : ChecklistStatus.Error,
                        AcknowledgeSafeMode,
                        "I Understand"),
                }, showProgress: true),

                // TOOLS
                new("Useful Tools", new List<ChecklistItem>
                {
                    ChecklistItem.QuickAction(
                        "Open Point Cache Bake Tool",
                        "Opens Unity's built-in Point Cache Bake Tool to generate .pCache position files for your custom meshes",
                        OpenPointCacheBakeTool,
                        actionLabel: "Open Bake Tool"),
                    ChecklistItem.QuickAction(
                        "Recompile VFX Graphs",
                        "Freshly-imported VFX Graphs sometimes don't compile correctly the first time",
                        ForceReimportVfxAssets,
                        actionLabel: "Recompile VFX Graphs"),
                    ChecklistItem.QuickAction(
                        "Open Demo Scene",
                        "Jump straight to the included demo scene showcasing the fire" +
                        " effect across all four flame intensities",
                        OpenDemoScene,
                        actionLabel: "Open Demo Scene"),
                }),

                // ADDITIONAL NOTES
                new("Additional Notes", new List<ChecklistItem>
                {
                    ChecklistItem.Reminder(
                        "PointCacheCount Must Match Baked Count",
                        "The PointCacheCount parameter in the Inspector must exactly match the point " +
                        "count baked in the Point Cache Bake Tool, or the effect will render incorrectly. " +
                        "\nSee 'How to Ignite Custom Models' in the docs."),

                    ChecklistItem.Reminder(
                        "Automatic Bounds Enabled by Default",
                        "Recalculates fire bounds every frame at a CPU cost. Before shipping, " +
                        "switch to Recorded or Manual bounds.\nSee 'Optimization for Production' in the docs."),

                    ChecklistItem.Reminder(
                        "Burn Masks Require Higher Point Counts",
                        "Points are baked evenly across the entire surface, ignoring masks. " +
                        "If your burn mask covers only a small area, bake a significantly higher total Point Count " +
                        "to keep the flame dense.\nSee 'Advanced Burn Masks Guide' in the docs."),

                    ChecklistItem.Reminder(
                        "Linear Color Space Recommended",
                        "Lit particles are authored for Linear color space. If your project " +
                        "runs in Gamma space, the effect will still function, but brightness and" +
                        " reflections will be visually incorrect."),
                }),
            };
        }

        // --- Checks -----------------------------------------------------------------

        private static ChecklistStatus EvaluateDepthTexture()
        {
            var pipelineAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (!pipelineAsset)
                return ChecklistStatus.Error;

            return pipelineAsset.supportsCameraDepthTexture ? ChecklistStatus.Ok : ChecklistStatus.Error;
        }

        private static ChecklistStatus EvaluateOpaqueTexture()
        {
            var pipelineAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (!pipelineAsset)
                return ChecklistStatus.Error;

            return pipelineAsset.supportsCameraOpaqueTexture ? ChecklistStatus.Ok : ChecklistStatus.Error;
        }

        private static ChecklistStatus EvaluatePipelineIsUrp()
        {
            return GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset
                ? ChecklistStatus.Ok
                : ChecklistStatus.Manual;
        }

        // --- Actions ------------------------------------------------------------------
        
        private static string OpenPointCacheBakeTool()
        {
            const string menuItemPath = "Window/Visual Effects/Utilities/Point Cache Bake Tool";

            return EditorApplication.ExecuteMenuItem(menuItemPath)
                ? "Opened Point Cache Bake Tool."
                : "Failed to open Bake Tool. Ensure Visual Effect Graph package is installed.";
        }

        private static string OpenDemoScene()
        {
            if (!File.Exists(DemoScenePath))
                return $"Demo scene not found at: {DemoScenePath}";

            if (SceneManager.GetActiveScene().isDirty && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return null; // user cancelled the save prompt

            EditorSceneManager.OpenScene(DemoScenePath);

            return "Opened the demo scene.";
        }

        private static string AcknowledgeSafeMode()
        {
            IsSafeModeAcknowledged = true;
            return "Acknowledged";
        }

        private static string OpenPackageManagerForVfxGraph()
        {
            UnityEditor.PackageManager.UI.Window.Open("com.unity.visualeffectgraph");
            return "Opened Package Manager — install it there, then click Refresh.";
        }

        private static string EnableDepthTextureWithConfirmation()
        {
            var pipelineAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (!pipelineAsset)
                return "No active URP Asset found in Graphics Settings.";

            if (pipelineAsset.supportsCameraDepthTexture)
                return "Depth Texture is already enabled.";

            var confirmed = EditorUtility.DisplayDialog(
                "Enable Depth Texture",
                $"This will enable Depth Texture on your active URP Asset ('{pipelineAsset.name}').\n\nProceed?",
                "Enable", "Cancel");

            if (!confirmed)
                return null;

            pipelineAsset.supportsCameraDepthTexture = true;
            EditorUtility.SetDirty(pipelineAsset);
            AssetDatabase.SaveAssets();
            return $"Enabled Depth Texture on '{pipelineAsset.name}'";
        }

        private static string EnableOpaqueTextureWithConfirmation()
        {
            var pipelineAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (!pipelineAsset)
                return "No active URP Asset found in Graphics Settings.";

            if (pipelineAsset.supportsCameraOpaqueTexture)
                return "Opaque Texture is already enabled";

            var confirmed = EditorUtility.DisplayDialog(
                "Enable Opaque Texture",
                $"This will enable Opaque Texture on your active URP Asset ('{pipelineAsset.name}').\n\nProceed?",
                "Enable", "Cancel");

            if (!confirmed)
                return null;

            pipelineAsset.supportsCameraOpaqueTexture = true;
            EditorUtility.SetDirty(pipelineAsset);
            AssetDatabase.SaveAssets();
            return $"Enabled Opaque Texture on '{pipelineAsset.name}'";
        }

        private static string ForceReimportVfxAssets()
        {
            if (!Directory.Exists(CurrentVFXFolder))
                return $"VFX folder not found at: {CurrentVFXFolder}";

            var guids = AssetDatabase.FindAssets("t:VisualEffectAsset", new[] { CurrentVFXFolder });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }

            AssetDatabase.Refresh();
            return guids.Length == 0
                ? "No VFX Graph assets found to reimport."
                : $"Reimported {guids.Length} VFX Graph asset(s).";
        }
    }
}
