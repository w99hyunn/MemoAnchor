using System;
using UnityEngine.UIElements;

namespace MemoAnchor.UI
{
    public static class MemoCalendarUtility
    {
        public static void Rebuild(VisualElement grid, Label title, DateTime month, DateTime selectedDate, Action<DateTime> onDateSelected)
        {
            grid.Clear();
            title.text = month.ToString("yyyy년 M월");

            string[] weekdays = { "일", "월", "화", "수", "목", "금", "토" };
            for (int i = 0; i < weekdays.Length; i++)
            {
                Button weekday = new() { text = weekdays[i] };
                weekday.AddToClassList("memo-filter-calendar-cell");
                weekday.AddToClassList("is-weekday-header");
                AddWeekendClass(weekday, i);
                grid.Add(weekday);
            }

            int leadingBlankCount = (int)month.DayOfWeek;
            DateTime firstVisibleDate = month.AddDays(-leadingBlankCount);
            for (int i = 0; i < 42; i++)
            {
                DateTime date = firstVisibleDate.AddDays(i);
                int dayOfWeek = (int)date.DayOfWeek;
                Button dayButton = new() { text = date.Day.ToString() };
                dayButton.AddToClassList("memo-filter-calendar-cell");
                dayButton.EnableInClassList("is-muted", date.Month != month.Month);
                AddWeekendClass(dayButton, dayOfWeek);
                dayButton.EnableInClassList("is-selected", date.Date == selectedDate.Date);

                DateTime selected = date;
                dayButton.clicked += () => onDateSelected(selected);
                grid.Add(dayButton);
            }
        }

        private static void AddWeekendClass(VisualElement element, int dayOfWeek)
        {
            if (dayOfWeek == 0)
            {
                element.AddToClassList("is-sunday");
            }
            else if (dayOfWeek == 6)
            {
                element.AddToClassList("is-saturday");
            }
        }
    }
}
