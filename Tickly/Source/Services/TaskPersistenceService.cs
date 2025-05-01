using System.Diagnostics;
using System.Text.Json;
using Tickly.Models;

namespace Tickly.Services;

public sealed class TaskPersistenceService
{
    private readonly string _filePath;
    private readonly object _saveLock = new();
    private bool _isSaving = false; // Internal flag to prevent concurrent saves

    public TaskPersistenceService()
    {
        _filePath = Path.Combine(FileSystem.AppDataDirectory, "tasks.json");
        Debug.WriteLine($"TaskPersistenceService: Initialized with file path: {_filePath}");
    }

    public async Task<List<TaskItem>> LoadTasksAsync()
    {
        Debug.WriteLine($"TaskPersistenceService.LoadTasksAsync: Attempting to load from: {_filePath}");
        if (!File.Exists(_filePath))
        {
            Debug.WriteLine("TaskPersistenceService.LoadTasksAsync: File not found, returning empty list.");
            return [];
        }

        try
        {
            string json = await File.ReadAllTextAsync(_filePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.WriteLine("TaskPersistenceService.LoadTasksAsync: File exists but is empty, returning empty list.");
                return [];
            }

            List<TaskItem>? loadedTasks = JsonSerializer.Deserialize<List<TaskItem>>(json);
            Debug.WriteLine($"TaskPersistenceService.LoadTasksAsync: Successfully deserialized {(loadedTasks?.Count ?? 0)} tasks.");
            return loadedTasks ?? [];
        }
        catch (JsonException jsonEx)
        {
            Debug.WriteLine($"TaskPersistenceService.LoadTasksAsync: Error deserializing JSON: {jsonEx.Message}. Returning empty list.");
            return []; // Return empty list on error
        }
        catch (IOException ioEx)
        {
            Debug.WriteLine($"TaskPersistenceService.LoadTasksAsync: IO Error reading file: {ioEx.Message}. Returning empty list.");
            return []; // Return empty list on error
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TaskPersistenceService.LoadTasksAsync: Unexpected error: {ex.GetType().Name} - {ex.Message}. Returning empty list.");
            return []; // Return empty list on other errors
        }
    }

    public async Task SaveTasksAsync(IEnumerable<TaskItem> tasks)
    {
        bool acquiredLock = false;
        lock (_saveLock)
        {
            if (_isSaving)
            {
                Debug.WriteLine("TaskPersistenceService.SaveTasksAsync: Save already in progress, skipping.");
                return; // Prevent concurrent saves
            }
            _isSaving = true;
            acquiredLock = true;
        }

        if (!acquiredLock) return;

        Debug.WriteLine("TaskPersistenceService.SaveTasksAsync: Starting save operation.");
        try
        {
            List<TaskItem> tasksToSave = tasks?.ToList() ?? [];
            if (tasksToSave.Count > 0)
            {
                Debug.WriteLine($"TaskPersistenceService.SaveTasksAsync: Serializing {tasksToSave.Count} tasks.");
                JsonSerializerOptions options = new() { WriteIndented = true };
                string json = JsonSerializer.Serialize(tasksToSave, options);

                Debug.WriteLine($"TaskPersistenceService.SaveTasksAsync: Writing JSON ({json.Length} chars) to {_filePath}");
                await File.WriteAllTextAsync(_filePath, json);
                Debug.WriteLine("TaskPersistenceService.SaveTasksAsync: Write operation completed.");
            }
            else
            {
                if (File.Exists(_filePath))
                {
                    Debug.WriteLine($"TaskPersistenceService.SaveTasksAsync: Task list is empty, deleting file: {_filePath}");
                    File.Delete(_filePath);
                    Debug.WriteLine("TaskPersistenceService.SaveTasksAsync: File deleted.");
                }
                else
                {
                    Debug.WriteLine("TaskPersistenceService.SaveTasksAsync: Task list is empty, file already doesn't exist.");
                }
            }
        }
        catch (IOException ioEx)
        {
            Debug.WriteLine($"TaskPersistenceService.SaveTasksAsync: IO Error writing file: {ioEx.Message}");
            // Consider how to handle save errors (e.g., retry, notify user)
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TaskPersistenceService.SaveTasksAsync: Unexpected error: {ex.GetType().Name} - {ex.Message}");
            // Consider how to handle save errors
        }
        finally
        {
            lock (_saveLock)
            {
                _isSaving = false; // Release the lock
                Debug.WriteLine("TaskPersistenceService.SaveTasksAsync: Save lock released.");
            }
        }
    }
}