// -----------------------------------------------------------------------------
// ChecklistItem.cs
//
// Copyright (c) [2026] Skulmovski Studio. All rights reserved.
// [Skulmovski Studio / https://skulmovski.studio/]
//
// Redistribution or resale outside of its original distribution channel is
// not permitted without written permission from Skulmovski Studio.
// -----------------------------------------------------------------------------

using System;

namespace SkulmovskiStudio.VFX_Fire.PointCache.Editor.Welcome
{
    public enum ChecklistStatus
    {
        Ok,
        Warning,
        Error,
        Manual,
        Info
    }

    public class ChecklistItem
    {
        public static string Version = "1.0.4";
        public readonly string Title;
        public readonly string Description;
        public Func<ChecklistStatus> Evaluate;
        public string ActionLabel = "Fix";
        public Func<string> OnAction;
        public string LastResult;

        public ChecklistStatus Status { get; private set; } = ChecklistStatus.Info;

        public bool IsStaticReminder => Evaluate == null;

        public ChecklistItem(string title, string description)
        {
            Title = title;
            Description = description;
        }

        public static ChecklistItem Reminder(string title, string description) => new ChecklistItem(title, description);

        public static ChecklistItem Check(
            string title,
            string description,
            Func<ChecklistStatus> evaluate,
            Func<string> onAction,
            string actionLabel = "Fix")
        {
            return new ChecklistItem(title, description)
            {
                Evaluate = evaluate,
                OnAction = onAction,
                ActionLabel = actionLabel,
            };
        }

        public static ChecklistItem ManualCheck(
            string title,
            string description,
            Func<ChecklistStatus> evaluate)
        {
            return new ChecklistItem(title, description) { Evaluate = evaluate };
        }

        public static ChecklistItem QuickAction(
            string title,
            string description,
            Func<string> onAction,
            string actionLabel = "Run")
        {
            return new ChecklistItem(title, description)
            {
                OnAction = onAction,
                ActionLabel = actionLabel
            };
        }

        public void Refresh()
        {
            if (Evaluate != null)
                Status = Evaluate.Invoke();
        }
    }
}
