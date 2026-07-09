using UnityEngine;
using UnityEngine.UIElements;

namespace MemoAnchor.UI
{
    public partial class MainView
    {
        private void RegisterMemoDeleteRow(VisualElement row)
        {
            Vector3 pointerDownPosition = Vector3.zero;
            IVisualElementScheduledItem longPressItem = null;

            row.RegisterCallback<PointerDownEvent>(evt =>
            {
                pointerDownPosition = evt.position;
                CloseOtherMemoDeleteRows(row);
                row.RemoveFromClassList(MEMO_DELETE_PRESS_CLASS);
                longPressItem?.Pause();
                longPressItem = row.schedule.Execute(() =>
                {
                    row.AddToClassList(MEMO_DELETE_OPEN_CLASS);
                    row.AddToClassList(MEMO_DELETE_PRESS_CLASS);
                });
                longPressItem.ExecuteLater(MEMO_DELETE_LONG_PRESS_MS);
            });

            row.RegisterCallback<PointerMoveEvent>(evt =>
            {
                Vector3 delta = evt.position - pointerDownPosition;
                if (delta.sqrMagnitude > MEMO_DELETE_LONG_PRESS_MOVE_TOLERANCE * MEMO_DELETE_LONG_PRESS_MOVE_TOLERANCE)
                {
                    longPressItem?.Pause();
                    row.RemoveFromClassList(MEMO_DELETE_PRESS_CLASS);
                }
            });

            row.RegisterCallback<PointerUpEvent>(_ =>
            {
                longPressItem?.Pause();
            });

            row.RegisterCallback<PointerCancelEvent>(_ =>
            {
                longPressItem?.Pause();
                row.RemoveFromClassList(MEMO_DELETE_PRESS_CLASS);
            });

            row.Q<Button>("memo-list-delete-button").clicked += () =>
            {
                RemoveMemoFilterRow(row);
                row.parent.RemoveFromHierarchy();
            };
        }

        private void CloseOtherMemoDeleteRows(VisualElement currentRow)
        {
            _root.Query<VisualElement>(className: "memo-list-swipe-row").ForEach(row =>
            {
                if (row != currentRow)
                {
                    row.RemoveFromClassList(MEMO_DELETE_OPEN_CLASS);
                }
            });
        }
    }
}
