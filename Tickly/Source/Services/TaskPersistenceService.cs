namespace Tickly.Services;

using System.Diagnostics;
using System.Text.Json;
using Tickly.Models;

public sealed class TaskPersistenceService : ITaskPersistenceService
{
    private readonly string _filePath;
    private bool _isSaving = false;
    private readonly object _saveLock = new();
    private Timer? _debounceTimer;
    private const int DebounceTimeMs = 500;

    public TaskPersistenceService()
    {
        _filePath = Path.Combine(FileSystem.AppDataDirectory, "tasks.json");
    }

    public async Task<List<TaskItem>> LoadTasksAsync()
    {
        Debug.WriteLine($"TaskPersistenceService: Attempting to load tasks from: {_filePath}");
        if (!File.Exists(_filePath))
        {
            return [];
        }

        try
        {
            var json = await File.ReadAllTextAsync(_filePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return [];
            }

            var loadedTasks = JsonSerializer.Deserialize<List<TaskItem>>(json) ?? [];
            Debug.WriteLine($"TaskPersistenceService: Successfully loaded {loadedTasks.Count} tasks.");
            return loadedTasks;
        }
        catch (JsonException jsonException)
        {
            Debug.WriteLine($"TaskPersistenceService: Error deserializing tasks JSON: {jsonException.Message}");
            return [];
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TaskPersistenceService: Error loading tasks: {ex.GetType().Name} - {ex.Message}");
            return [];
        }
    }

    public void TriggerSave(IEnumerable<TaskItem> tasks)
    {
        var tasksToSave = tasks.ToList(); // Capture list state at the time of trigger

        _debounceTimer?.Dispose();
        _debounceTimer = new Timer(async _ =>
        {
            await SaveTasksInternalAsync(tasksToSave);
            _debounceTimer?.Dispose();
            _debounceTimer = null;
        },
        null,
        TimeSpan.FromMilliseconds(DebounceTimeMs),
        Timeout.InfiniteTimeSpan);
    }

    private async Task SaveTasksInternalAsync(List<TaskItem> tasksToSave)
    {
        bool acquiredLock = false;

        try
        {
            lock (_saveLock)
            {
                if (_isSaving)
                {
                    Debug.WriteLine("TaskPersistenceService: Save already in progress, skipping.");
                    return; // Don't queue another save if one is running
                }
                _isSaving = true;
                acquiredLock = true;
            }

            Debug.WriteLine($"TaskPersistenceService: Starting save operation for {tasksToSave.Count} tasks.");
            if (tasksToSave.Count > 0)
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(tasksToSave, options);
                await File.WriteAllTextAsync(_filePath, json);
                Debug.WriteLine($"TaskPersistenceService: Successfully saved {tasksToSave.Count} tasks to {_filePath}");
            }
            else
            {
                if (File.Exists(_filePath))
                {
                    File.Delete(_filePath);
                    Debug.WriteLine($"TaskPersistenceService: Deleted empty task file: {_filePath}");
                }
                else
                {
                    Debug.WriteLine($"TaskPersistenceService: No tasks to save and file does not exist.");
                }
            }
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"TaskPersistenceService: Error saving tasks: {exception.Message}");
        }
        finally
        {
            if (acquiredLock)
            {
                lock (_saveLock)
                {
                    _isSaving = false;
                    Debug.WriteLine("TaskPersistenceService: Save operation finished.");
                }
            }
        }
    }
}