using System.Collections.ObjectModel;
using System.Diagnostics;
using Microsoft.Maui.Graphics; // Required for Color
using Tickly.Models;

namespace Tickly.Services;

public sealed class TaskVisualStateService
{
    private static readonly Color StartColor = Colors.Red;
    private static readonly Color MidColor = Colors.Yellow;
    private static readonly Color EndColor = Colors.LimeGreen;

    public TaskProgressResult CalculateProgress(ObservableCollection<TaskItem> tasks)
    {
        if (tasks is null)
        {
            return new TaskProgressResult(0.0, StartColor);
        }

        var totalTasks = (double)tasks.Count;
        var tasksDueToday = (double)tasks.Count(t => t.DueDate.HasValue && t.DueDate.Value.Date == DateTime.Today);

        var progressValue = (totalTasks == 0) ? 1.0 : (totalTasks - tasksDueToday) / totalTasks;
        progressValue = Math.Clamp(progressValue, 0.0, 1.0);

        float r, g, b;
        var factor = (float)progressValue;

        if (factor < 0.5f)
        {
            r = 1.0f;
            g = factor * 2.0f;
            b = 0.0f;
        }
        else
        {
            r = 1.0f - (factor - 0.5f) * 2.0f;
            g = 1.0f;
            b = 0.0f;
        }

        r = float.Clamp(r, 0.0f, 1.0f);
        g = float.Clamp(g, 0.0f, 1.0f);
        b = float.Clamp(b, 0.0f, 1.0f);

        var progressColor = new Color(r, g, b);

        return new TaskProgressResult(progressValue, progressColor);
    }

    public void UpdateTaskIndicesAndColors(ObservableCollection<TaskItem> tasks)
    {
        var totalCount = tasks?.Count ?? 0;
        Debug.WriteLine($"TaskVisualStateService.UpdateTaskIndicesAndColors: Updating for {totalCount} tasks.");

        if (tasks == null) return;

        for (var i = 0; i < totalCount; i++)
        {
            TaskItem? currentTask = tasks[i];
            if (currentTask is not null)
            {
                if (currentTask.Order != i) currentTask.Order = i;
                if (currentTask.Index != i) currentTask.Index = i;

                var factor = totalCount <= 1 ? 0.0 : (double)i / (totalCount - 1);
                Color newColor = InterpolateColor(factor);

                if (currentTask.PositionColor != newColor)
                {
                    currentTask.PositionColor = newColor;
                }
            }
        }
    }

    private static Color InterpolateColor(double factor)
    {
        factor = Math.Clamp(factor, 0.0, 1.0);
        float r, g, b, a;
        var currentFactor = (float)factor;

        if (currentFactor < 0.5f)
        {
            var localFactor = currentFactor * 2.0f;
            r = StartColor.Red + localFactor * (MidColor.Red - StartColor.Red);
            g = StartColor.Green + localFactor * (MidColor.Green - StartColor.Green);
            b = StartColor.Blue + localFactor * (MidColor.Blue - StartColor.Blue);
            a = StartColor.Alpha + localFactor * (MidColor.Alpha - StartColor.Alpha);
        }
        else
        {
            var localFactor = (currentFactor - 0.5f) * 2.0f;
            r = MidColor.Red + localFactor * (EndColor.Red - MidColor.Red);
            g = MidColor.Green + localFactor * (EndColor.Green - MidColor.Green);
            b = MidColor.Blue + localFactor * (EndColor.Blue - MidColor.Blue);
            a = MidColor.Alpha + localFactor * (EndColor.Alpha - MidColor.Alpha);
        }

        r = float.Clamp(r, 0.0f, 1.0f);
        g = float.Clamp(g, 0.0f, 1.0f);
        b = float.Clamp(b, 0.0f, 1.0f);
        a = float.Clamp(a, 0.0f, 1.0f);

        return new(r, g, b, a);
    }
}

public readonly record struct TaskProgressResult(double Progress, Color ProgressColor);