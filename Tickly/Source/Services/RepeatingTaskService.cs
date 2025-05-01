using System.Diagnostics;
using Tickly.Models;
using Tickly.Utils;

namespace Tickly.Services;

public sealed class RepeatingTaskService
{
    public bool UpdateRepeatingTaskDueDate(TaskItem task)
    {
        if (task is null || task.TimeType != TaskTimeType.Repeating)
        {
            Debug.WriteLine("UpdateRepeatingTaskDueDate: Task is null or not repeating.");
            return false; // Indicate no update or removal needed based on this logic
        }

        DateTime? nextDueDate = DateUtils.CalculateNextDueDate(task);
        if (nextDueDate.HasValue)
        {
            task.DueDate = nextDueDate;
            Debug.WriteLine($"UpdateRepeatingTaskDueDate: Updated task '{task.Title}' next due date to {nextDueDate.Value:yyyy-MM-dd}");
            return true; // Indicate date was updated
        }
        else
        {
            Debug.WriteLine($"UpdateRepeatingTaskDueDate: Could not calculate next due date for task '{task.Title}'. Task might be removed.");
            return false; // Indicate date was not updated, potentially signal removal
        }
    }

    public bool ResetDailyTaskDueDate(TaskItem task)
    {
        if (task is null ||
            task.TimeType != TaskTimeType.Repeating ||
            task.RepetitionType != TaskRepetitionType.Daily ||
            !task.DueDate.HasValue ||
            task.DueDate.Value.Date != DateTime.Today.AddDays(1))
        {
            Debug.WriteLine($"ResetDailyTaskDueDate: Task '{task?.Title}' (ID: {task?.Id}) does not meet reset criteria.");
            return false;
        }

        task.DueDate = DateTime.Today;
        Debug.WriteLine($"ResetDailyTaskDueDate: Reset task '{task.Title}' (ID: {task.Id}) due date to today.");
        return true;
    }

    public bool EnsureCorrectDueDateOnLoad(TaskItem task, DateTime today)
    {
        if (task is null || task.TimeType != TaskTimeType.Repeating || !task.DueDate.HasValue || task.DueDate.Value.Date >= today)
        {
            return false; // No update needed
        }

        DateTime originalDueDate = task.DueDate.Value.Date;
        DateTime nextValidDueDate = CalculateNextValidDueDateForRepeatingTask(task, today, originalDueDate);

        if (originalDueDate != nextValidDueDate)
        {
            task.DueDate = nextValidDueDate;
            Debug.WriteLine($"EnsureCorrectDueDateOnLoad: Updated task '{task.Title}' due date from {originalDueDate:yyyy-MM-dd} to {nextValidDueDate:yyyy-MM-dd}");
            return true; // Date was changed
        }

        return false; // Date was already correct
    }

    private DateTime CalculateNextValidDueDateForRepeatingTask(TaskItem task, DateTime today, DateTime originalDueDate)
    {
        // This logic remains the same as it was in MainViewModel
        DateTime nextValidDueDate = originalDueDate;
        switch (task.RepetitionType)
        {
            case TaskRepetitionType.Daily:
                nextValidDueDate = today;
                break;
            case TaskRepetitionType.AlternateDay:
                // If the difference in days is even, the next due date is today.
                // If odd, it should be tomorrow relative to today.
                double daysDifference = (today - originalDueDate).TotalDays;
                nextValidDueDate = daysDifference % 2 == 0 ? today : today.AddDays(1);
                break;
            case TaskRepetitionType.Weekly:
                if (task.RepetitionDayOfWeek.HasValue)
                {
                    // Ensure we get the *next* occurrence including today if it matches
                    nextValidDueDate = DateUtils.GetNextWeekday(today, task.RepetitionDayOfWeek.Value);
                }
                else
                {
                    // Fallback if day is somehow missing (shouldn't happen with UI)
                    // Keep advancing by 7 days until it's today or later
                    while (nextValidDueDate < today)
                    {
                        nextValidDueDate = nextValidDueDate.AddDays(7);
                    }
                }
                break;
        }
        return nextValidDueDate.Date; // Ensure time component is stripped
    }
}