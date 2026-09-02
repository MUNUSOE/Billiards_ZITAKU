// -----------------------------------------------------------------------------
// ChecklistSection.cs
//
// Copyright (c) [2026] Skulmovski Studio. All rights reserved.
// [Skulmovski Studio / https://skulmovski.studio/]
//
// Redistribution or resale outside of its original distribution channel is
// not permitted without written permission from Skulmovski Studio.
// -----------------------------------------------------------------------------

using System.Collections.Generic;

namespace SkulmovskiStudio.VFX_Fire.PointCache.Editor.Welcome
{
    public class ChecklistSection
    {
        public readonly string Title;
        public List<ChecklistItem> Items;
        public readonly bool ShowProgress;
        public bool IsExpanded = true;
        public bool HasAutoCollapsedOnce;

        public ChecklistSection(string title, List<ChecklistItem> items, bool showProgress = false)
        {
            Title = title;
            Items = items;
            ShowProgress = showProgress;
        }
    }
}
