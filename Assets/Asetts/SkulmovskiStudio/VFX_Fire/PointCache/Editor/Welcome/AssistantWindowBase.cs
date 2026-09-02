// -----------------------------------------------------------------------------
// WelcomeWindowBase.cs
//
// Copyright (c) [2026] Skulmovski Studio. All rights reserved.
// [Skulmovski Studio / https://skulmovski.studio/]
//
// Redistribution or resale outside of its original distribution channel is
// not permitted without written permission from Skulmovski Studio.
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SkulmovskiStudio.VFX_Fire.PointCache.Editor.Welcome
{
    public struct SocialLink
    {
        public readonly Texture2D Icon;
        public readonly string Url;
        public readonly string Tooltip;

        public SocialLink(Texture2D icon, string url, string tooltip = null)
        {
            Icon = icon;
            Url = url;
            Tooltip = tooltip;
        }
    }

    public abstract class AssistantWindowBase : EditorWindow
    {
        // BASE
        public static string Version = "1.0.4";

        public const string CurrentAssistantFolder = "Assets/SkulmovskiStudio/VFX_Fire/PointCache/Editor/Welcome/";
        // ------------------------------------------

        protected virtual string StudioName => "Skulmovski Studio";
        protected virtual Texture2D StudioLogo => null;
        protected virtual IEnumerable<SocialLink> SocialLinks => System.Array.Empty<SocialLink>();

        // ---- Per-asset overrides ----
        protected abstract string AssetTitle { get; }
        protected virtual string AssetTagline => "VFX Setup Assistant";
        protected virtual string DocsUrl => null;
        protected virtual string DocsPdfPath => null;
        protected virtual Texture2D AssetIcon => null;
        protected virtual string ThemeStylesheetPath => null;
        protected virtual string LightThemeStylesheetPath => null;

        protected abstract List<ChecklistSection> BuildChecklist();
        private static readonly Dictionary<ChecklistStatus, Texture2D> StatusIconCache = new();

        // ---- Internals ----
        private const string IconsFolder = CurrentAssistantFolder + "Textures/";
        private const string UxmlPath = CurrentAssistantFolder + "UI/AssistantWindow.uxml";
        private const string CardUxmlPath = CurrentAssistantFolder + "UI/AssistantWindowCard.uxml";
        private const string LightIconPath = CurrentAssistantFolder + "Textures/T_Icon_ThemeLight.png";
        private const string DarkIconPath = CurrentAssistantFolder + "Textures/T_Icon_ThemeDark.png";

        private static Texture2D _lightThemeIconCache;
        private static Texture2D _darkThemeIconCache;
        private List<ChecklistSection> _sections;
        private VisualElement _listContainer;
        private VisualTreeAsset _cardTemplate;
        private VFX_Fire.PointCache.Editor.Welcome.RingProgressElement _ring;
        private StyleSheet _activeThemeSheet;
        private bool _isLightTheme;

        private string ThemePrefKey => "SkulmovskiStudio.AssistantWindow.LightTheme." + GetType().FullName;

        private static T RequireElement<T>(VisualElement root, string name) where T : VisualElement
        {
            var element = root.Q<T>(name);
            if (element == null)
                Debug.LogError($"[AssistantWindow] UI element '{name}' (type {typeof(T).Name}) not found. " +
                               "Check that AssistantWindow.uxml / AssistantWindowCard.uxml element names match the " +
                               "Q<>() calls in AssistantWindowBase.cs — they must match exactly.");
            return element;
        }

        protected static T OpenWindow<T>(string title, int width = 600, int height = 750) where T : AssistantWindowBase
        {
            var window = GetWindow<T>(true, title, true);
            window.minSize = new Vector2(600, 400);
            window.maxSize = new Vector2(1400, 4000);

            var main = EditorGUIUtility.GetMainWindowPosition();
            var centerX = main.x + (main.width - width) * 0.5f;
            var centerY = main.y + (main.height - height) * 0.5f;
            window.position = new Rect(centerX, centerY, width, height);

            return window;
        }

        protected static void ShowOnFirstImport<T>(string title) where T : AssistantWindowBase
        {
            if (IsGloballyDisabled<T>())
                return;

            var prefKey = "SkulmovskiStudio.AssistantWindow.FirstShown." + typeof(T).FullName + "." +
                          Application.dataPath;
            if (EditorPrefs.GetBool(prefKey, false))
                return;

            EditorApplication.delayCall += () =>
            {
                EditorPrefs.SetBool(prefKey, true);
                OpenWindow<T>(title);
            };
        }

        private const string TestedUnityVersion = "6000.0.77f1";

        private static string GlobalDisableKey(System.Type concreteType) =>
            "SkulmovskiStudio.AssistantWindow.GloballyDisabled." + concreteType.FullName + "." + Application.dataPath;

        protected static void SetGloballyDisabled<T>(bool disabled) where T : AssistantWindowBase =>
            EditorPrefs.SetBool(GlobalDisableKey(typeof(T)), disabled);

        protected static bool IsGloballyDisabled<T>() where T : AssistantWindowBase =>
            EditorPrefs.GetBool(GlobalDisableKey(typeof(T)), false);

        private bool IsDisabledForThisWindow => EditorPrefs.GetBool(GlobalDisableKey(GetType()), false);

        public void CreateGUI()
        {
            if (IsDisabledForThisWindow)
            {
                rootVisualElement.Add(new Label("The VFX Setup Assistant is currently disabled " +
                                                "(Tools > Skulmovski Studio > (this asset) > Enable " +
                                                "Assistant to turn it back on)."));
                return;
            }

            try
            {
                BuildWindow();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Setup Assistant] Failed to build window: {e}\n\n" +
                               "This doesn't affect the actual VFX asset — only this helper window. " +
                               "You can disable it via Tools > Skulmovski Studio > " +
                               "(this asset) > Disable Assistant and continue setting " +
                               "things up manually (see Documentation).");

                rootVisualElement.Clear();
                rootVisualElement.Add(new Label("Sorry — the Setup Assistant hit an error and couldn't build" +
                                                " its window. See the Console for details. This doesn't " +
                                                "affect the actual VFX asset — please check the Documentation " +
                                                "for the asset/project/demo setup requirements instead. You can" +
                                                " also disable this assistant via Tools > Skulmovski Studio" +
                                                " > (this asset) > Disable Assistant."));
            }
        }

        private void BuildWindow()
        {
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            if (!visualTree)
            {
                rootVisualElement.Add(new Label($"AssistantWindow.uxml not found at {UxmlPath}." +
                                                $" Update UxmlPath in AssistantWindowBase."));
                return;
            }

            visualTree.CloneTree(rootVisualElement);
            _cardTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(CardUxmlPath);

            SetupTheme();
            BuildStudioBar();
            BuildHeader();
            SetupCloseButton();
            SetupDocsButton();

            var disclaimerLabel = RequireElement<Label>(rootVisualElement, "footer-disclaimer");
            if (disclaimerLabel != null)
                disclaimerLabel.text = $"Tested on Unity {TestedUnityVersion}. Other versions aren't guaranteed to " +
                                       "behave the same — if something looks wrong, disable this assistant via " +
                                       "Tools > Skulmovski Studio > (this asset) > Disable Assistant.";

            _listContainer = RequireElement<VisualElement>(rootVisualElement, "checklist-container");
            _sections = BuildChecklist();

            RefreshAndRedraw();
        }

        private void SetupTheme()
        {
            var toggle = RequireElement<VisualElement>(rootVisualElement, "theme-toggle-button");

            if (string.IsNullOrEmpty(ThemeStylesheetPath))
            {
                if (toggle != null)
                    toggle.style.display = DisplayStyle.None;
                return;
            }

            var isLight = EditorPrefs.GetBool(ThemePrefKey, true);
            ApplyTheme(isLight);

            if (toggle == null)
                return;

            if (string.IsNullOrEmpty(LightThemeStylesheetPath))
            {
                toggle.style.display = DisplayStyle.None;
                return;
            }

            toggle.RegisterCallback<ClickEvent>(_ =>
            {
                _isLightTheme = !_isLightTheme;
                EditorPrefs.SetBool(ThemePrefKey, _isLightTheme);
                ApplyTheme(_isLightTheme);
            });
        }

        private void SetupCloseButton()
        {
            var closeButton = RequireElement<Button>(rootVisualElement, "close-button");
            if (closeButton != null)
                closeButton.clicked += Close;
        }

        private void SetupDocsButton()
        {
            var docsButton = RequireElement<Button>(rootVisualElement, "docs-button");
            if (docsButton == null)
                return;

            if (!string.IsNullOrEmpty(DocsPdfPath))
            {
                docsButton.clicked += OpenDocsPdf;
                return;
            }

            if (!string.IsNullOrEmpty(DocsUrl))
            {
                docsButton.clicked += () => Application.OpenURL(DocsUrl);
                return;
            }

            docsButton.style.display = DisplayStyle.None;
        }

        private void OpenDocsPdf()
        {
            var pdfAsset = AssetDatabase.LoadMainAssetAtPath(DocsPdfPath);
            if (pdfAsset)
                AssetDatabase.OpenAsset(pdfAsset);
            else Debug.LogWarning($"[AssistantWindow] Documentation PDF not found at: {DocsPdfPath}");
        }


        private void ApplyTheme(bool light)
        {
            if (_activeThemeSheet)
                rootVisualElement.styleSheets.Remove(_activeThemeSheet);

            var path = light ? LightThemeStylesheetPath : ThemeStylesheetPath;
            _activeThemeSheet = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<StyleSheet>(path);

            if (_activeThemeSheet)
                rootVisualElement.styleSheets.Add(_activeThemeSheet);
            else if (!string.IsNullOrEmpty(path))
                Debug.LogWarning($"[AssistantWindow] Theme stylesheet not found at: {path}");

            _isLightTheme = light;

            var lightIcon = RequireElement<VisualElement>(rootVisualElement, "theme-icon-light");
            var darkIcon = RequireElement<VisualElement>(rootVisualElement, "theme-icon-dark");

            if (lightIcon != null)
            {
                if (!_lightThemeIconCache)
                    _lightThemeIconCache = AssetDatabase.LoadAssetAtPath<Texture2D>(LightIconPath);
                if (_lightThemeIconCache)
                    lightIcon.style.backgroundImage = new StyleBackground(_lightThemeIconCache);
                lightIcon.EnableInClassList("theme-toggle-icon--active", light);
            }

            if (darkIcon != null)
            {
                if (!_darkThemeIconCache)
                    _darkThemeIconCache = AssetDatabase.LoadAssetAtPath<Texture2D>(DarkIconPath);
                if (_darkThemeIconCache)
                    darkIcon.style.backgroundImage = new StyleBackground(_darkThemeIconCache);
                darkIcon.EnableInClassList("theme-toggle-icon--active", !light);
            }

            _ring?.MarkDirtyRepaint();
        }

        private void BuildStudioBar()
        {
            var studioNameLabel = RequireElement<Label>(rootVisualElement, "studio-name");
            if (studioNameLabel != null)
                studioNameLabel.text = StudioName;

            var logoSlot = RequireElement<VisualElement>(rootVisualElement, "studio-logo-slot");
            if (logoSlot != null && StudioLogo)
                logoSlot.style.backgroundImage = new StyleBackground(StudioLogo);

            var linksContainer = RequireElement<VisualElement>(rootVisualElement, "social-links");
            if (linksContainer == null)
                return;

            foreach (var link in SocialLinks)
            {
                var button = new VisualElement();
                button.AddToClassList("social-button");

                var icon = new VisualElement();
                icon.AddToClassList("social-button-icon");
                if (link.Icon)
                    icon.style.backgroundImage = new StyleBackground(link.Icon);
                button.Add(icon);

                var label = new Label(link.Tooltip);
                label.AddToClassList("social-button-label");
                button.Add(label);

                if (!string.IsNullOrEmpty(link.Tooltip))
                    button.tooltip = link.Tooltip;

                var url = link.Url;
                button.RegisterCallback<ClickEvent>(_ => Application.OpenURL(url));
                linksContainer.Add(button);
            }
        }


        private void BuildHeader()
        {
            var titleLabel = RequireElement<Label>(rootVisualElement, "asset-title");
            if (titleLabel != null)
                titleLabel.text = AssetTitle;

            var taglineLabel = RequireElement<Label>(rootVisualElement, "asset-tagline");
            if (taglineLabel != null)
                taglineLabel.text = AssetTagline;

            _ring = RequireElement<VFX_Fire.PointCache.Editor.Welcome.RingProgressElement>(rootVisualElement,
                "progress-ring");
            if (_ring != null)
                _ring.CompleteIcon = LoadStatusIcon(ChecklistStatus.Ok);

            var refreshButton = RequireElement<VisualElement>(rootVisualElement, "refresh-button");
            if (refreshButton != null)
                refreshButton.RegisterCallback<ClickEvent>(_ => RefreshAndRedraw());

            var refreshIcon = RequireElement<VisualElement>(rootVisualElement, "refresh-icon");
            if (refreshIcon != null)
            {
                var refreshTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(IconsFolder + "T_Icon_Refresh.png");
                if (refreshTexture)
                    refreshIcon.style.backgroundImage = new StyleBackground(refreshTexture);
                else Debug.LogWarning($"[AssistantWindow] Missing refresh icon at {IconsFolder}T_Icon_Refresh.png.");
            }
        }

        private void RefreshAndRedraw()
        {
            foreach (var item in AllItems())
                item.Refresh();

            _listContainer.Clear();
            foreach (var section in _sections)
            {
                _listContainer.Add(BuildSectionHeader(section));

                if (section.ShowProgress && !section.IsExpanded)
                    continue;

                foreach (var item in section.Items)
                    _listContainer.Add(BuildCard(item));
            }

            UpdateRing();
        }

        private IEnumerable<ChecklistItem> AllItems()
        {
            foreach (var section in _sections)
            foreach (var item in section.Items)
                yield return item;
        }

        private void UpdateRing()
        {
            var total = 0;
            var done = 0;
            foreach (var item in AllItems())
            {
                if (item.IsStaticReminder)
                    continue;
                total++;
                if (item.Status == ChecklistStatus.Ok)
                    done++;
            }

            _ring?.SetProgress(done, total);
        }

        private VisualElement BuildSectionHeader(ChecklistSection section)
        {
            var row = new VisualElement();
            row.AddToClassList("section-header");

            var total = 0;
            var done = 0;
            if (section.ShowProgress)
            {
                foreach (var item in section.Items)
                {
                    if (item.IsStaticReminder)
                        continue;
                    total++;
                    if (item.Status == ChecklistStatus.Ok)
                        done++;
                }
            }

            var isComplete = section.ShowProgress && total > 0 && done == total;

            if (section.ShowProgress)
            {
                if (isComplete && !section.HasAutoCollapsedOnce)
                {
                    section.IsExpanded = false;
                    section.HasAutoCollapsedOnce = true;
                }
                else if (!isComplete && section.HasAutoCollapsedOnce)
                {
                    section.HasAutoCollapsedOnce = false;
                    section.IsExpanded = true;
                }
            }

            if (section.ShowProgress)
            {
                var chevron = new Label(section.IsExpanded ? "\u25BE" : "\u25B8");
                chevron.AddToClassList("section-chevron");
                row.Add(chevron);
            }

            var title = new Label(section.Title);
            title.AddToClassList("section-title");
            row.Add(title);

            if (section.ShowProgress)
            {
                var spacer = new VisualElement();
                spacer.AddToClassList("section-header-spacer");
                row.Add(spacer);

                var progress = new Label();
                if (isComplete)
                {
                    progress.text = "\u2713 Section Done";
                    progress.AddToClassList("section-complete");
                }
                else
                {
                    progress.text = $"Needs Attention ({total - done} Left)";
                    progress.AddToClassList("section-progress");
                }

                row.Add(progress);

                row.AddToClassList("section-header--clickable");
                row.RegisterCallback<ClickEvent>(_ =>
                {
                    section.IsExpanded = !section.IsExpanded;
                    RefreshAndRedraw();
                });
            }

            return row;
        }

        private VisualElement BuildCard(ChecklistItem item)
        {
            var card = RequireElement<VisualElement>(_cardTemplate.CloneTree(), "card-root");
            if (card == null)
                return new VisualElement();

            var icon = RequireElement<VisualElement>(card, "card-icon");
            var titleLabel = RequireElement<Label>(card, "card-title");
            var descLabel = RequireElement<Label>(card, "card-description");
            var tagLabel = RequireElement<Label>(card, "card-status-tag");
            var actionButton = RequireElement<Button>(card, "card-action");
            var actionHint = RequireElement<Label>(card, "card-action-hint");
            var resultLabel = RequireElement<Label>(card, "card-result");

            if (titleLabel != null) titleLabel.text = item.Title;
            if (descLabel != null) descLabel.text = item.Description;

            foreach (var cls in StatusClasses(item.Status))
                card.AddToClassList(cls);

            if (item.IsStaticReminder)
                card.AddToClassList("card--reminder");

            var hasRun = item.IsStaticReminder && item.OnAction != null && !string.IsNullOrEmpty(item.LastResult);

            ApplyCardTag(item, hasRun, card, tagLabel);
            ApplyCardAction(item, hasRun, actionButton, actionHint);
            ApplyCardResult(item, resultLabel);

            ApplyStatusIcon(icon, hasRun ? ChecklistStatus.Ok : item.Status);

            return card;
        }

        private static void ApplyCardTag(ChecklistItem item, bool hasRun, VisualElement card, Label tagLabel)
        {
            if (!item.IsStaticReminder)
            {
                tagLabel.text = StatusTagText(item.Status);
                return;
            }

            if (!hasRun)
                return;

            card.AddToClassList("status-ok");
            tagLabel.text = "Done";
        }

        private void ApplyCardAction(ChecklistItem item, bool hasRun, Button actionButton, Label actionHint)
        {
            var showAction = item.OnAction != null && !hasRun &&
                             (item.IsStaticReminder || item.Status is ChecklistStatus.Warning or ChecklistStatus.Error);

            if (!showAction)
            {
                actionButton.style.display = DisplayStyle.None;
                actionHint.style.display = DisplayStyle.None;
                return;
            }

            actionButton.style.display = DisplayStyle.Flex;
            actionButton.text = item.ActionLabel;
            actionButton.clicked += () =>
            {
                item.LastResult = item.OnAction.Invoke();
                RefreshAndRedraw();
            };

            actionHint.style.display = DisplayStyle.None;
        }

        private static void ApplyCardResult(ChecklistItem item, Label resultLabel)
        {
            if (string.IsNullOrEmpty(item.LastResult))
            {
                resultLabel.style.display = DisplayStyle.None;
                return;
            }

            resultLabel.text = item.LastResult;
            resultLabel.style.display = DisplayStyle.Flex;
        }

        private static IEnumerable<string> StatusClasses(ChecklistStatus status)
        {
            switch (status)
            {
                case ChecklistStatus.Ok:
                    yield return "status-ok";
                    break;
                case ChecklistStatus.Warning:
                    yield return "status-fix";
                    yield return "status-warning";
                    break;
                case ChecklistStatus.Error:
                    yield return "status-fix";
                    yield return "status-error";
                    break;
                case ChecklistStatus.Manual:
                    yield return "status-manual";
                    break;
                default:
                    yield return "status-info";
                    break;
            }
        }

        private static string StatusTagText(ChecklistStatus status) => status switch
        {
            ChecklistStatus.Ok => "Done",
            ChecklistStatus.Manual => "Manual Only",
            _ => string.Empty
        };


        private static Texture2D LoadStatusIcon(ChecklistStatus status)
        {
            if (!StatusIconCache.TryGetValue(status, out var texture) || !texture)
            {
                var fileName = status switch
                {
                    ChecklistStatus.Ok => "T_Icon_StatusOk.png",
                    ChecklistStatus.Warning => "T_Icon_StatusWarning.png",
                    ChecklistStatus.Error => "T_Icon_StatusError.png",
                    ChecklistStatus.Manual => "T_Icon_StatusManual.png",
                    _ => "T_Icon_StatusInfo.png"
                };

                texture = AssetDatabase.LoadAssetAtPath<Texture2D>(IconsFolder + fileName);

                if (texture)
                    StatusIconCache[status] = texture;
            }

            if (!texture)
                Debug.LogWarning($"[AssistantWindow] Missing status icon for {status} at {IconsFolder}.");

            return texture;
        }

        private static void ApplyStatusIcon(VisualElement iconElement, ChecklistStatus status)
        {
            var texture = LoadStatusIcon(status);
            if (texture)
                iconElement.style.backgroundImage = new StyleBackground(texture);
        }

        protected static bool HasPackage(string packageName)
        {
            var listRequest = UnityEditor.PackageManager.Client.List(true);
            var start = EditorApplication.timeSinceStartup;
            while (!listRequest.IsCompleted)
            {
                if (EditorApplication.timeSinceStartup - start > 5.0)
                {
                    Debug.LogWarning("[AssistantWindow] Package Manager request timed out after 5s while checking " +
                                     $"for '{packageName}'. Treating as not installed.");
                    return false;
                }
            }

            return listRequest.Status == UnityEditor.PackageManager.StatusCode.Success &&
                   listRequest.Result.Any(pkg => pkg.name == packageName);
        }
    }
}
