using System.Diagnostics;
using System.Text.Json;
using Tickly.Models;

namespace Tickly.Services;

public interface ITaskPersistenceService
{
    Task<List<TaskItem>> LoadTasksAsync();
    Task SaveTasksAsync(IEnumerable<TaskItem> tasks);
}

public class TaskPersistenceService : ITaskPersistenceService
{
    private readonly string filePath;

    public TaskPersistenceService()
    {
        filePath = Path.Combine(FileSystem.AppDataDirectory, "tasks.json");
        Debug.WriteLine($"TaskPersistenceService: Using data file at {filePath}");
    }

    public async Task<List<TaskItem>> LoadTasksAsync()
    {
        if (!File.Exists(filePath))
        {
            Debug.WriteLine("LoadTasksAsync: Data file does not exist. Returning empty list.");
            return [];
        }

        try
        {
            string json = await File.ReadAllTextAsync(filePath);

            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.WriteLine("LoadTasksAsync: Data file is empty. Returning empty list.");
                return [];
            }

            List<TaskItem>? loadedTasks = JsonSerializer.Deserialize<List<TaskItem>>(json);
            return loadedTasks ?? [];
        }
        catch (JsonException jsonEx)
        {
            Debug.WriteLine($"LoadTasksAsync: Error deserializing tasks JSON: {jsonEx.Message}. Returning empty list.");
            // Consider renaming or backing up the corrupt file here
            return [];
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"LoadTasksAsync: Error loading tasks: {ex.GetType().Name} - {ex.Message}. Returning empty list.");
            // Rethrow or handle more gracefully depending on requirements
            // For now, return empty to prevent crash, but log the error.
            return [];
        }
    }

    public async Task SaveTasksAsync(IEnumerable<TaskItem> tasks)
    {
        try
        {
            List<TaskItem> tasksToSave = [.. tasks]; // Create a list copy

            if (tasksToSave.Count > 0)
            {
                JsonSerializerOptions options = new() { WriteIndented = true };
                string json = JsonSerializer.Serialize(tasksToSave, options);
                await File.WriteAllTextAsync(filePath, json);
                // Debug.WriteLine($"SaveTasksAsync: Successfully saved {tasksToSave.Count} tasks.");
            }
            else
            {
                // If the list is empty, delete the file if it exists
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    Debug.WriteLine("SaveTasksAsync: Task list is empty. Deleted data file.");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SaveTasksAsync: Error saving tasks: {ex.GetType().Name} - {ex.Message}");
            // Consider notifying the user or implementing more robust error handling
        }
    }
}