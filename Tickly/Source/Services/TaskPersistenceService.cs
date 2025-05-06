using System.Diagnostics;
using System.Text.Json;
using Tickly.Models;

namespace Tickly.Services;

public sealed class TaskPersistenceService
{
    private readonly string _tasksFilePath;
    private readonly string _progressFilePath;
    private readonly object _tasksSaveLock = new();
    private readonly object _progressSaveLock = new();
    private bool _isSavingTasks = false; // Internal flag to prevent concurrent saves for tasks
    private bool _isSavingProgress = false; // Internal flag to prevent concurrent saves for progress

    public TaskPersistenceService()
    {
        _tasksFilePath = Path.Combine(FileSystem.AppDataDirectory, "tasks.json");
        _progressFilePath = Path.Combine(FileSystem.AppDataDirectory, "dailyProgress.json");
        Debug.WriteLine($"TaskPersistenceService: Initialized with tasks file path: {_tasksFilePath}");
        Debug.WriteLine($"TaskPersistenceService: Initialized with progress file path: {_progressFilePath}");
    }

    public async Task<List<TaskItem>> LoadTasksAsync()
    {
        Debug.WriteLine($"TaskPersistenceService.LoadTasksAsync: Attempting to load from: {_tasksFilePath}");
        if (!File.Exists(_tasksFilePath))
        {
            Debug.WriteLine("TaskPersistenceService.LoadTasksAsync: File not found, returning empty list.");
            return [];
        }

        try
        {
            string json = await File.ReadAllTextAsync(_tasksFilePath);
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
        lock (_tasksSaveLock)
        {
            if (_isSavingTasks)
            {
                Debug.WriteLine("TaskPersistenceService.SaveTasksAsync: Save already in progress, skipping.");
                return; // Prevent concurrent saves
            }
            _isSavingTasks = true;
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

                Debug.WriteLine($"TaskPersistenceService.SaveTasksAsync: Writing JSON ({json.Length} chars) to {_tasksFilePath}");
                await File.WriteAllTextAsync(_tasksFilePath, json);
                Debug.WriteLine("TaskPersistenceService.SaveTasksAsync: Write operation completed.");
            }
            else
            {
                if (File.Exists(_tasksFilePath))
                {
                    Debug.WriteLine($"TaskPersistenceService.SaveTasksAsync: Task list is empty, deleting file: {_tasksFilePath}");
                    File.Delete(_tasksFilePath);
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
            lock (_tasksSaveLock)
            {
                _isSavingTasks = false; // Release the lock
                Debug.WriteLine("TaskPersistenceService.SaveTasksAsync: Save lock released.");
            }
        }
    }

    public async Task<List<DailyProgress>> LoadDailyProgressAsync()
    {
        Debug.WriteLine($"TaskPersistenceService.LoadDailyProgressAsync: Attempting to load from: {_progressFilePath}");
        if (!File.Exists(_progressFilePath))
        {
            Debug.WriteLine("TaskPersistenceService.LoadDailyProgressAsync: File not found, returning empty list.");
            return [];
        }

        try
        {
            string json = await File.ReadAllTextAsync(_progressFilePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.WriteLine("TaskPersistenceService.LoadDailyProgressAsync: File exists but is empty, returning empty list.");
                return [];
            }

            List<DailyProgress>? loadedProgress = JsonSerializer.Deserialize<List<DailyProgress>>(json);
            Debug.WriteLine($"TaskPersistenceService.LoadDailyProgressAsync: Successfully deserialized {(loadedProgress?.Count ?? 0)} progress entries.");
            return loadedProgress ?? [];
        }
        catch (JsonException jsonEx)
        {
            Debug.WriteLine($"TaskPersistenceService.LoadDailyProgressAsync: Error deserializing JSON: {jsonEx.Message}. Returning empty list.");
            return [];
        }
        catch (IOException ioEx)
        {
            Debug.WriteLine($"TaskPersistenceService.LoadDailyProgressAsync: IO Error reading file: {ioEx.Message}. Returning empty list.");
            return [];
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TaskPersistenceService.LoadDailyProgressAsync: Unexpected error: {ex.GetType().Name} - {ex.Message}. Returning empty list.");
            return [];
        }
    }

    private async Task SaveDailyProgressListAsync(List<DailyProgress> progressList)
    {
        bool acquiredLock = false;
        lock (_progressSaveLock)
        {
            if (_isSavingProgress)
            {
                Debug.WriteLine("TaskPersistenceService.SaveDailyProgressListAsync: Save already in progress, skipping.");
                return;
            }
            _isSavingProgress = true;
            acquiredLock = true;
        }

        if (!acquiredLock) return;

        Debug.WriteLine("TaskPersistenceService.SaveDailyProgressListAsync: Starting save operation.");
        try
        {
            List<DailyProgress> progressToSave = progressList?.ToList() ?? [];
            JsonSerializerOptions options = new() { WriteIndented = true };
            string json = JsonSerializer.Serialize(progressToSave, options);

            Debug.WriteLine($"TaskPersistenceService.SaveDailyProgressListAsync: Writing JSON ({json.Length} chars) to {_progressFilePath}");
            await File.WriteAllTextAsync(_progressFilePath, json);
            Debug.WriteLine("TaskPersistenceService.SaveDailyProgressListAsync: Write operation completed.");
        }
        catch (IOException ioEx)
        {
            Debug.WriteLine($"TaskPersistenceService.SaveDailyProgressListAsync: IO Error writing file: {ioEx.Message}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TaskPersistenceService.SaveDailyProgressListAsync: Unexpected error: {ex.GetType().Name} - {ex.Message}");
        }
        finally
        {
            lock (_progressSaveLock)
            {
                _isSavingProgress = false; // Release the lock
                Debug.WriteLine("TaskPersistenceService.SaveDailyProgressListAsync: Save lock released.");
            }
        }
    }
    
    public async Task AddDailyProgressEntryAsync(DailyProgress newEntry)
    {
        Debug.WriteLine($"TaskPersistenceService.AddDailyProgressEntryAsync: Adding new entry for date: {newEntry.Date}");
        List<DailyProgress> currentProgress = await LoadDailyProgressAsync();
        
        // Optional: Check if an entry for this date already exists and update it, or add new.
        // For simplicity, this example just adds. If multiple entries per day are not desired,
        // you might want to remove existing entries for the same date before adding the new one.
        var existingEntry = currentProgress.FirstOrDefault(p => p.Date.Date == newEntry.Date.Date);
        if (existingEntry != null)
        {
            Debug.WriteLine($"TaskPersistenceService.AddDailyProgressEntryAsync: Updating existing entry for date: {newEntry.Date}");
            existingEntry.PercentageCompleted = newEntry.PercentageCompleted;
        }
        else
        {
            Debug.WriteLine($"TaskPersistenceService.AddDailyProgressEntryAsync: Adding as new entry for date: {newEntry.Date}");
            currentProgress.Add(newEntry);
        }

        await SaveDailyProgressListAsync(currentProgress);
        Debug.WriteLine($"TaskPersistenceService.AddDailyProgressEntryAsync: Successfully added/updated and saved entry for date: {newEntry.Date}");
    }
}
