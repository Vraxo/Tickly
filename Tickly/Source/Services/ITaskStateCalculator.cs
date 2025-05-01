namespace Tickly.Services;

using Tickly.Models;

public interface ITaskStateCalculator
{
    void UpdateTaskIndicesAndPositionColors(IEnumerable<TaskItem> tasks);
    (double Progress, Color ProgressColor) CalculateOverallProgressState(IEnumerable<TaskItem> tasks);
}