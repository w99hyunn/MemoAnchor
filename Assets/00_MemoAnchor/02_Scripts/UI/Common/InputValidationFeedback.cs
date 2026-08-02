using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace MemoAnchor.UI
{
    public static class InputValidationFeedback
    {
        private const string ERROR_CLASS = "is-error";
        private const int SHAKE_STEP_COUNT = 10;
        private const float SHAKE_MAX_OFFSET = 52f;
        private const float SHAKE_STEP_DURATION_SECONDS = 0.05f;

        public static void ShowError(VisualElement element)
        {
            element.AddToClassList(ERROR_CLASS);
        }

        public static void ClearError(VisualElement element)
        {
            element.RemoveFromClassList(ERROR_CLASS);
            element.style.translate = new Translate(0, 0, 0);
        }

        public static void AddIfError(List<VisualElement> elements, VisualElement element)
        {
            if (element.ClassListContains(ERROR_CLASS))
            {
                elements.Add(element);
            }
        }

        public static async Awaitable ShakeAsync(VisualElement element)
        {
            float maxOffset = GetAvailableShakeOffset(element);

            for (int i = 0; i < SHAKE_STEP_COUNT; i++)
            {
                element.style.translate = new Translate(GetShakeOffset(i, maxOffset), 0, 0);
                await Awaitable.WaitForSecondsAsync(SHAKE_STEP_DURATION_SECONDS);
            }

            element.style.translate = new Translate(0, 0, 0);
        }

        public static async Awaitable ShakeAsync(IReadOnlyList<VisualElement> elements)
        {
            float[] maxOffsets = new float[elements.Count];
            for (int i = 0; i < elements.Count; i++)
            {
                maxOffsets[i] = GetAvailableShakeOffset(elements[i]);
            }

            for (int i = 0; i < SHAKE_STEP_COUNT; i++)
            {
                for (int index = 0; index < elements.Count; index++)
                {
                    float offset = GetShakeOffset(i, maxOffsets[index]);
                    elements[index].style.translate = new Translate(offset, 0, 0);
                }

                await Awaitable.WaitForSecondsAsync(SHAKE_STEP_DURATION_SECONDS);
            }

            for (int i = 0; i < elements.Count; i++)
            {
                elements[i].style.translate = new Translate(0, 0, 0);
            }
        }

        private static float GetAvailableShakeOffset(VisualElement element)
        {
            Rect elementBounds = element.worldBound;
            Rect screenBounds = element.panel.visualTree.worldBound;
            float leftSpace = elementBounds.xMin - screenBounds.xMin;
            float rightSpace = screenBounds.xMax - elementBounds.xMax;
            return Mathf.Clamp(Mathf.Min(leftSpace, rightSpace) - 6f, 0f, SHAKE_MAX_OFFSET);
        }

        private static float GetShakeOffset(int step, float maxOffset)
        {
            float strength = 1f - step / (float)SHAKE_STEP_COUNT;
            float direction = step % 2 == 0 ? -1f : 1f;
            return direction * maxOffset * strength;
        }
    }
}
