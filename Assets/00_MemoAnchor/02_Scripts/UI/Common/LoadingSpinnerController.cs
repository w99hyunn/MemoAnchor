using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace MemoAnchor.UI
{
    public static class LoadingSpinnerController
    {
        private static readonly Dictionary<VisualElement, SpinnerState> States = new();

        public static void Start(VisualElement spinner, float cycleDurationMs = 1250f, long intervalMs = 16)
        {
            Stop(spinner);

            var state = new SpinnerState
            {
                StartedAt = Time.realtimeSinceStartup,
                CycleDurationMs = cycleDurationMs,
                Draw = context => DrawSpinner(context, spinner)
            };

            States[spinner] = state;
            spinner.generateVisualContent += state.Draw;
            state.Schedule = spinner.schedule.Execute(() =>
            {
                float elapsedMs = (Time.realtimeSinceStartup - state.StartedAt) * 1000f;
                state.Progress = (elapsedMs % state.CycleDurationMs) / state.CycleDurationMs;
                state.Rotation = (elapsedMs * 0.9f) % 360f;
                spinner.MarkDirtyRepaint();
            }).Every(intervalMs);
        }

        public static void Stop(VisualElement spinner)
        {
            if (States.Remove(spinner, out SpinnerState state))
            {
                state.Schedule.Pause();
                spinner.generateVisualContent -= state.Draw;
            }

            spinner.style.rotate = new Rotate(new Angle(0f));
            spinner.MarkDirtyRepaint();
        }

        private static void DrawSpinner(MeshGenerationContext context, VisualElement spinner)
        {
            if (!States.TryGetValue(spinner, out SpinnerState state))
            {
                return;
            }

            Rect rect = spinner.contentRect;
            float size = Mathf.Min(rect.width, rect.height);
            float lineWidth = Mathf.Max(6f, size * 0.105f);
            float radius = (size - lineWidth) * 0.5f;
            Vector2 center = rect.center;
            float pulse = 0.5f - Mathf.Cos(state.Progress * Mathf.PI * 2f) * 0.5f;
            float sweep = Mathf.Lerp(48f, 264f, pulse);
            float start = state.Rotation;
            float end = start + sweep;

            Painter2D painter = context.painter2D;
            painter.lineWidth = lineWidth;
            painter.lineCap = LineCap.Round;
            painter.strokeColor = new Color(0.631f, 0.122f, 0.247f, 1f);
            painter.BeginPath();
            painter.Arc(center, radius, new Angle(start), new Angle(end), ArcDirection.Clockwise);
            painter.Stroke();
        }

        private sealed class SpinnerState
        {
            public IVisualElementScheduledItem Schedule { get; set; }
            public float StartedAt { get; set; }
            public float CycleDurationMs { get; set; }
            public float Progress { get; set; }
            public float Rotation { get; set; }
            public System.Action<MeshGenerationContext> Draw { get; set; }
        }
    }
}
