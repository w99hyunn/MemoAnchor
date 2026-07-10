using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace MemoAnchor.UI
{
    public static class InputValidationFeedback
    {
        private const string ERROR_CLASS = "is-error";
        private const int SHAKE_FRAME_COUNT = 12;
        private const float SHAKE_OFFSET = 40f;

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
            for (int i = 0; i < SHAKE_FRAME_COUNT; i++)
            {
                element.style.translate = new Translate(i % 2 == 0 ? -SHAKE_OFFSET : SHAKE_OFFSET, 0, 0);
                await Awaitable.NextFrameAsync();
            }

            element.style.translate = new Translate(0, 0, 0);
        }

        public static async Awaitable ShakeAsync(IReadOnlyList<VisualElement> elements)
        {
            for (int i = 0; i < SHAKE_FRAME_COUNT; i++)
            {
                float offset = i % 2 == 0 ? -SHAKE_OFFSET : SHAKE_OFFSET;
                for (int index = 0; index < elements.Count; index++)
                {
                    elements[index].style.translate = new Translate(offset, 0, 0);
                }

                await Awaitable.NextFrameAsync();
            }

            for (int i = 0; i < elements.Count; i++)
            {
                elements[i].style.translate = new Translate(0, 0, 0);
            }
        }
    }
}
