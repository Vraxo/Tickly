using Tickly.Models;

namespace Tickly.Services;

public interface ITaskPersistenceService
{
    Task<List<TaskItem>> LoadTasksAsync();
    void TriggerSave(IEnumerable<TaskItem> tasks);
}