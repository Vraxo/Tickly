namespace Tickly.Services;

using Tickly.Models;

public sealed class TaskStateCalculator : ITaskStateCalculator
{
    private readonly Color _startColor;
    // private readonly Color _midColor; // MidColor isn't explicitly used in the latest gradient logic, but keep if needed
    // private readonly Color _endColor; // EndColor isn't explicitly used in the latest gradient logic, but keep if needed

    // Constructor to receive the gradient colors
    public TaskStateCalculator(Color startColor, Color midColor, Color endColor)
    {
        // Store the necessary colors. The current logic primarily uses StartColor for alpha and the gradient calc.
        _startColor = startColor;
        // _midColor = midColor; // Uncomment if needed by logic
        // _endColor = endColor; // Uncomment if needed by logic
    }

    public void UpdateTaskIndicesAndPositionColors(IEnumerable<TaskItem> tasks)
    {
        var taskList = tasks.ToList(); // Work with a concrete list
        var totalCount = taskList.Count;

        for (var i = 0; i < totalCount; i++)
        {
            var currentTask = taskList[i];
            if (currentTask != null)
            {
                // Update Order and Index properties based on current position
                if (currentTask.Order != i) currentTask.Order = i;
                if (currentTask.Index != i) currentTask.Index = i;

                // Calculate position color (Red -> Yellow -> Green gradient)
                Color newColor;
                if (totalCount <= 1)
                {
                    newColor = _startColor; // Use the injected start color
                }
                else
                {
                    // Factor from 0 (first item) to 1 (last item)
                    var factor = (float)i / (totalCount - 1);
                    factor = float.Clamp(factor, 0.0f, 1.0f);

                    float r, g, b;
                    if (factor < 0.5f) // Red (1,0,0) to Yellow (1,1,0)
                    {
                        r = 1.0f;
                        g = factor * 2.0f; // Interpolate Green from 0 to 1
                        b = 0.0f;
                    }
                    else // Yellow (1,1,0) to Green (0,1,0)
                    {
                        r = 1.0f - (factor - 0.5f) * 2.0f; // Interpolate Red from 1 to 0
                        g = 1.0f;
                        b = 0.0f; // Blue stays 0
                    }

                    // Clamp RGB values just in case
                    r = float.Clamp(r, 0.0f, 1.0f);
                    g = float.Clamp(g, 0.0f, 1.0f);
                    b = float.Clamp(b, 0.0f, 1.0f);
                    var a = _startColor.Alpha; // Use alpha from the injected start color
                    newColor = new Color(r, g, b, a);
                }
                // Update the task's color only if it changed
                if (currentTask.PositionColor != newColor) currentTask.PositionColor = newColor;
            }
        }
    }

    public (double Progress, Color ProgressColor) CalculateOverallProgressState(IEnumerable<TaskItem> tasks)
    {
        var taskList = tasks.ToList(); // Work with a concrete list
        if (taskList is null) // Should not happen if called correctly, but safeguard
        {
            return (0.0, _startColor);
        }

        var totalTasks = (double)taskList.Count;
        var tasksDueToday = (double)taskList.Count(t => t.DueDate.HasValue && t.DueDate.Value.Date == DateTime.Today);
        double progressValue;

        if (totalTasks == 0)
        {
            progressValue = 1.0; // Considered "complete" when no tasks exist
        }
        else
        {
            // Progress represents the fraction of tasks NOT due today
            progressValue = (totalTasks - tasksDueToday) / totalTasks;
        }

        // Clamp progress just in case, although calculation should be within 0-1
        progressValue = (double)float.Clamp((float)progressValue, 0.0f, 1.0f);

        // Calculate color based on progress (Red at 0.0, Yellow at 0.5, Green at 1.0)
        float r, g, b;
        var factor = (float)progressValue;

        if (factor < 0.5f) // Red (1,0,0) to Yellow (1,1,0)
        {
            r = 1.0f;
            g = factor * 2.0f;
            b = 0.0f;
        }
        else // Yellow (1,1,0) to Green (0,1,0)
        {
            r = 1.0f - (factor - 0.5f) * 2.0f;
            g = 1.0f;
            b = 0.0f;
        }

        // Clamp final RGB values
        r = float.Clamp(r, 0.0f, 1.0f);
        g = float.Clamp(g, 0.0f, 1.0f);
        b = float.Clamp(b, 0.0f, 1.0f);
        var progressColor = new Color(r, g, b); // Use default alpha (1.0f) or derive if needed

        return (progressValue, progressColor);
    }
}