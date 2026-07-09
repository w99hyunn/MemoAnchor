using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace MemoAnchor.UI
{
    public static class PopupPresentation
    {
        public const string MODAL_ROOT_NAME = "popup-modal-root";
        public const string MODAL_PANEL_NAME = "popup-modal-panel";

        public static float OpenDuration { get; set; } = 0.26f;
        public static float CloseDuration { get; set; } = 0.2f;

        private static readonly HashSet<VisualElement> ClosingRoots = new();
        private static readonly object ClosingLock = new();

        public static VisualElement ResolveNamedInSubtree(VisualElement root, string elementName)
        {
            if (root.name == elementName)
            {
                return root;
            }

            return root.Q<VisualElement>(elementName);
        }

        public static VisualElement ResolveModalPanel(VisualElement root)
        {
            VisualElement panel = ResolveNamedInSubtree(root, MODAL_PANEL_NAME);
            if (panel != null)
            {
                return panel;
            }

            return root.childCount > 0 ? root[0] : root;
        }

        public static void ScheduleOpen(VisualElement root)
        {
            if (root != null)
            {
                _ = OpenAsync(root);
            }
        }

        public static async Awaitable OpenAsync(VisualElement root, float? duration = null)
        {
            float d = duration ?? OpenDuration;
            VisualElement panel = ResolveModalPanel(root);

            root.pickingMode = PickingMode.Position;
            root.style.opacity = 0f;
            CenterTransformOrigin(panel);
            SetUniformScale(panel, 0.92f);

            float t = 0f;
            while (t < d)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / d);
                root.style.opacity = EaseOutCubic(u);
                SetUniformScale(panel, Mathf.LerpUnclamped(0.92f, 1f, EaseOutBack(u)));
                await Awaitable.NextFrameAsync();
            }

            root.style.opacity = 1f;
            SetUniformScale(panel, 1f);
        }

        public static void ScheduleClose(
            VisualElement root,
            Func<bool> stillOwned,
            Action teardownIfStillOwned = null,
            Action whenFinished = null)
        {
            if (root == null || !TryBeginClose(root))
            {
                return;
            }

            _ = CloseRoutineAsync(root, stillOwned, teardownIfStillOwned, whenFinished);
        }

        public static async Awaitable CloseAsync(
            VisualElement root,
            Func<bool> stillOwned,
            Action teardownIfStillOwned = null,
            Action whenFinished = null)
        {
            if (root == null || !TryBeginClose(root))
            {
                return;
            }

            await CloseRoutineAsync(root, stillOwned, teardownIfStillOwned, whenFinished);
        }

        private static bool TryBeginClose(VisualElement root)
        {
            lock (ClosingLock)
            {
                if (ClosingRoots.Contains(root))
                {
                    return false;
                }

                ClosingRoots.Add(root);
                return true;
            }
        }

        private static void EndClose(VisualElement root)
        {
            lock (ClosingLock)
            {
                ClosingRoots.Remove(root);
            }
        }

        private static async Awaitable CloseRoutineAsync(
            VisualElement root,
            Func<bool> stillOwned,
            Action teardownIfStillOwned,
            Action whenFinished)
        {
            try
            {
                root.pickingMode = PickingMode.Ignore;
                await PlayCloseAnimAsync(root);
                if (stillOwned())
                {
                    teardownIfStillOwned?.Invoke();
                }
            }
            finally
            {
                try
                {
                    whenFinished?.Invoke();
                }
                finally
                {
                    EndClose(root);
                }
            }
        }

        private static async Awaitable PlayCloseAnimAsync(VisualElement root, float? duration = null)
        {
            float d = duration ?? CloseDuration;
            VisualElement panel = ResolveModalPanel(root);

            CenterTransformOrigin(panel);

            float t = 0f;
            while (t < d)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / d);
                float easeIn = EaseInCubic(u);
                root.style.opacity = 1f - easeIn;
                SetUniformScale(panel, Mathf.Lerp(1f, 0.94f, easeIn));
                await Awaitable.NextFrameAsync();
            }
        }

        private static void CenterTransformOrigin(VisualElement ve)
        {
            ve.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50), 0);
        }

        private static void SetUniformScale(VisualElement ve, float scale)
        {
            ve.style.scale = new StyleScale(new Scale(new Vector3(scale, scale, 1f)));
        }

        private static float EaseOutCubic(float t)
        {
            float u = 1f - t;
            return 1f - u * u * u;
        }

        private static float EaseInCubic(float t)
        {
            return t * t * t;
        }

        private static float EaseOutBack(float t)
        {
            const float C1 = 1.525f;
            const float C3 = C1 + 1f;
            return 1f + C3 * Mathf.Pow(t - 1f, 3f) + C1 * Mathf.Pow(t - 1f, 2f);
        }
    }
}
