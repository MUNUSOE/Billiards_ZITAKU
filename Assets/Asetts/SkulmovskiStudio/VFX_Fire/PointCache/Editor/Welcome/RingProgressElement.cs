// -----------------------------------------------------------------------------
// RingProgressElement.cs
//
// Copyright (c) [2026] Skulmovski Studio. All rights reserved.
// [Skulmovski Studio / https://skulmovski.studio/]
//
// Redistribution or resale outside of its original distribution channel is
// not permitted without written permission from Skulmovski Studio.
// -----------------------------------------------------------------------------

using UnityEngine;
using UnityEngine.UIElements;

namespace SkulmovskiStudio.VFX_Fire.PointCache.Editor.Welcome
{
    [UxmlElement]
    public partial class RingProgressElement : VisualElement
    {
        public static string Version = "1.0.4";
        private const float Thickness = 4f;

        private readonly Label _countLabel;
        private readonly VisualElement _completeIcon;
        private readonly VisualElement _accentColorProbe;
        private readonly VisualElement _trackColorProbe;
        private readonly VisualElement _completeColorProbe;

        private float _progress01;
        private bool _isComplete;

        public Texture2D CompleteIcon
        {
            set => _completeIcon.style.backgroundImage = value
                ? new StyleBackground(value)
                : new StyleBackground(StyleKeyword.None);
        }

        public RingProgressElement()
        {
            AddToClassList("ring");
            generateVisualContent += OnGenerateVisualContent;

            _accentColorProbe = new VisualElement();
            _accentColorProbe.AddToClassList("ring-accent-color");
            _accentColorProbe.style.width = 0;
            _accentColorProbe.style.height = 0;
            _accentColorProbe.style.position = Position.Absolute;
            _accentColorProbe.style.visibility = Visibility.Hidden;
            _accentColorProbe.pickingMode = PickingMode.Ignore;
            Add(_accentColorProbe);

            _trackColorProbe = new VisualElement();
            _trackColorProbe.AddToClassList("ring-track-color");
            _trackColorProbe.style.width = 0;
            _trackColorProbe.style.height = 0;
            _trackColorProbe.style.position = Position.Absolute;
            _trackColorProbe.style.visibility = Visibility.Hidden;
            _trackColorProbe.pickingMode = PickingMode.Ignore;
            Add(_trackColorProbe);

            _completeColorProbe = new VisualElement();
            _completeColorProbe.AddToClassList("ring-complete-color");
            _completeColorProbe.style.width = 0;
            _completeColorProbe.style.height = 0;
            _completeColorProbe.style.position = Position.Absolute;
            _completeColorProbe.style.visibility = Visibility.Hidden;
            _completeColorProbe.pickingMode = PickingMode.Ignore;
            Add(_completeColorProbe);

            _countLabel = new Label();
            _countLabel.AddToClassList("ring-label");
            _countLabel.pickingMode = PickingMode.Ignore;
            Add(_countLabel);

            _completeIcon = new VisualElement();
            _completeIcon.AddToClassList("ring-icon");
            _completeIcon.style.display = DisplayStyle.None;
            _completeIcon.pickingMode = PickingMode.Ignore;
            Add(_completeIcon);
        }

        public void SetProgress(int done, int total)
        {
            _progress01 = total <= 0 ? 0f : Mathf.Clamp01((float)done / total);
            _isComplete = total > 0 && done == total;

            _countLabel.style.display = _isComplete ? DisplayStyle.None : DisplayStyle.Flex;
            _completeIcon.style.display = _isComplete ? DisplayStyle.Flex : DisplayStyle.None;

            if (!_isComplete)
                _countLabel.text = $"{done}/{total}";

            MarkDirtyRepaint();
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            var rect = contentRect;
            if (rect.width < 4 || rect.height < 4)
                return;

            var radius = Mathf.Min(rect.width, rect.height) / 2f - Thickness / 2f - 1f;
            var center = new Vector2(rect.width / 2f, rect.height / 2f);

            var painter = ctx.painter2D;
            painter.lineWidth = Thickness;
            painter.lineCap = LineCap.Round;

            painter.strokeColor = _trackColorProbe.resolvedStyle.backgroundColor;
            painter.BeginPath();
            painter.Arc(center, radius, 0f, 360f);
            painter.Stroke();

            if (_progress01 <= 0f)
                return;

            painter.strokeColor = _isComplete
                ? _completeColorProbe.resolvedStyle.backgroundColor
                : _accentColorProbe.resolvedStyle.backgroundColor;
            painter.BeginPath();
            painter.Arc(center, radius, -90f, -90f + 360f * _progress01);
            painter.Stroke();
        }
    }
}
