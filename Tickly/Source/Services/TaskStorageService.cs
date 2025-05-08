using System.Diagnostics;
using System.Text.Json;
using Tickly.Models;

namespace Tickly.Services;

public sealed class TaskStorageService
{
    private readonly string _tasksFilePath;
    private readonly Lock _tasksSaveLock = new();
    private bool _isSavingTasks = false;

    public TaskStorageService()
    {
        _tasksFilePath = Path.Combine(FileSystem.AppDataDirectory, "tasks.json");
        Debug.WriteLine($"TaskStorageService: Initialized with tasks file path: {_tasksFilePath}");
    }

    public async Task<List<TaskItem>> LoadTasksAsync()
    {
        Debug.WriteLine($"TaskStorageService.LoadTasksAsync: Attempting to load from: {_tasksFilePath}");

        if (!File.Exists(_tasksFilePath))
        {
            Debug.WriteLine("TaskStorageService.LoadTasksAsync: File not found, returning empty list.");
            return [];
        }

        try
        {
            string json = await File.ReadAllTextAsync(_tasksFilePath);

            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.WriteLine("TaskStorageService.LoadTasksAsync: File exists but is empty, returning empty list.");
                return [];
            }

            List<TaskItem>? loadedTasks = JsonSerializer.Deserialize<List<TaskItem>>(json);
            Debug.WriteLine($"TaskStorageService.LoadTasksAsync: Successfully deserialized {(loadedTasks?.Count ?? 0)} tasks.");
            return loadedTasks ?? [];
        }
        catch (JsonException jsonEx)
        {
            Debug.WriteLine($"TaskStorageService.LoadTasksAsync: Error deserializing JSON: {jsonEx.Message}. Returning empty list.");
            return [];
        }
        catch (IOException ioEx)
        {
            Debug.WriteLine($"TaskStorageService.LoadTasksAsync: IO Error reading file: {ioEx.Message}. Returning empty list.");
            return [];
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TaskStorageService.LoadTasksAsync: Unexpected error: {ex.GetType().Name} - {ex.Message}. Returning empty list.");
            return [];
        }
    }

    public async Task SaveTasksAsync(IEnumerable<TaskItem> tasks)
    {
        bool acquiredLock = false;

        lock (_tasksSaveLock)
        {
            if (_isSavingTasks)
            {
                Debug.WriteLine("TaskStorageService.SaveTasksAsync: Save already in progress, skipping.");
                return;
            }
            _isSavingTasks = true;
            acquiredLock = true;
        }

        if (!acquiredLock)
        {
            return;
        }

        Debug.WriteLine("TaskStorageService.SaveTasksAsync: Starting save operation.");
        try
        {
            List<TaskItem> tasksToSave = tasks?.ToList() ?? [];
            if (tasksToSave.Count > 0)
            {
                Debug.WriteLine($"TaskStorageService.SaveTasksAsync: Serializing {tasksToSave.Count} tasks.");
                JsonSerializerOptions options = new() { WriteIndented = true };
                string json = JsonSerializer.Serialize(tasksToSave, options);

                Debug.WriteLine($"TaskStorageService.SaveTasksAsync: Writing JSON ({json.Length} chars) to {_tasksFilePath}");
                await File.WriteAllTextAsync(_tasksFilePath, json);
                Debug.WriteLine("TaskStorageService.SaveTasksAsync: Write operation completed.");
            }
            else
            {
                if (File.Exists(_tasksFilePath))
                {
                    Debug.WriteLine($"TaskStorageService.SaveTasksAsync: Task list is empty, deleting file: {_tasksFilePath}");
                    File.Delete(_tasksFilePath);
                    Debug.WriteLine("TaskStorageService.SaveTasksAsync: File deleted.");
                }
                else
                {
                    Debug.WriteLine("TaskStorageService.SaveTasksAsync: Task list is empty, file already doesn't exist.");
                }
            }
        }
        catch (IOException ioEx)
        {
            Debug.WriteLine($"TaskStorageService.SaveTasksAsync: IO Error writing file: {ioEx.Message}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TaskStorageService.SaveTasksAsync: Unexpected error: {ex.GetType().Name} - {ex.Message}");
        }
        finally
        {
            lock (_tasksSaveLock)
            {
                _isSavingTasks = false;
                Debug.WriteLine("TaskStorageService.SaveTasksAsync: Save lock released.");
            }
        }
    }
}