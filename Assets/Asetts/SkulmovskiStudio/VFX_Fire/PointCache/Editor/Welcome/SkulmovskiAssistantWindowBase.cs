// -----------------------------------------------------------------------------
// SkulmovskiStudioAssistantWindowBase.cs
//
// Copyright (c) [2026] Skulmovski Studio. All rights reserved.
// [Skulmovski Studio / https://skulmovski.studio/]
//
// Redistribution or resale outside of its original distribution channel is
// not permitted without written permission from Skulmovski Studio.
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SkulmovskiStudio.VFX_Fire.PointCache.Editor.Welcome
{
    public abstract class SkulmovskiAssistantWindowBase : AssistantWindowBase
    {
        private const string LogoPath = CurrentAssistantFolder + "Textures/T_Logo.png";
        private static Texture2D _cachedLogo;

        protected override string StudioName => "Skulmovski Studio";
        protected override Texture2D StudioLogo
        {
            get
            {
                if (!_cachedLogo)
                    _cachedLogo = AssetDatabase.LoadAssetAtPath<Texture2D>(LogoPath);

                return _cachedLogo;
            }
        }

        private static Texture2D DiscordIcon =>
            AssetDatabase.LoadAssetAtPath<Texture2D>(CurrentAssistantFolder + "Textures/T_Icon_Discord.png");
        private static Texture2D WebsiteIcon =>
            AssetDatabase.LoadAssetAtPath<Texture2D>(CurrentAssistantFolder + "Textures/T_Icon_Website.png");

        protected override IEnumerable<SocialLink> SocialLinks => new[]
        {
            new SocialLink(DiscordIcon, "https://discord.gg/nEMsmMTRXb", "Discord"),
            new SocialLink(WebsiteIcon, "https://skulmovski.studio/", "Website"),
        };

        protected override string ThemeStylesheetPath => CurrentAssistantFolder + "Themes/StudioTheme.uss";
        protected override string LightThemeStylesheetPath => CurrentAssistantFolder + "Themes/StudioThemeLight.uss";
    }
}
