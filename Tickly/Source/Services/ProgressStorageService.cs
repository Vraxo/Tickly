using System.Diagnostics;
using System.Text.Json;
using Tickly.Models;

namespace Tickly.Services;

public sealed class ProgressStorageService
{
    private readonly string _progressFilePath;
    private readonly Lock _progressSaveLock = new();
    private bool _isSavingProgress = false;

    public ProgressStorageService()
    {
        _progressFilePath = Path.Combine(FileSystem.AppDataDirectory, "dailyProgress.json");
        Debug.WriteLine($"ProgressStorageService: Initialized with progress file path: {_progressFilePath}");
    }

    public async Task<List<DailyProgress>> LoadDailyProgressAsync()
    {
        Debug.WriteLine($"ProgressStorageService.LoadDailyProgressAsync: Attempting to load from: {_progressFilePath}");
        if (!File.Exists(_progressFilePath))
        {
            Debug.WriteLine("ProgressStorageService.LoadDailyProgressAsync: File not found, returning empty list.");
            return [];
        }

        try
        {
            string json = await File.ReadAllTextAsync(_progressFilePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.WriteLine("ProgressStorageService.LoadDailyProgressAsync: File exists but is empty, returning empty list.");
                return [];
            }

            List<DailyProgress>? loadedProgress = JsonSerializer.Deserialize<List<DailyProgress>>(json);
            Debug.WriteLine($"ProgressStorageService.LoadDailyProgressAsync: Successfully deserialized {(loadedProgress?.Count ?? 0)} progress entries.");
            return loadedProgress ?? [];
        }
        catch (JsonException jsonEx)
        {
            Debug.WriteLine($"ProgressStorageService.LoadDailyProgressAsync: Error deserializing JSON: {jsonEx.Message}. Returning empty list.");
            return [];
        }
        catch (IOException ioEx)
        {
            Debug.WriteLine($"ProgressStorageService.LoadDailyProgressAsync: IO Error reading file: {ioEx.Message}. Returning empty list.");
            return [];
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ProgressStorageService.LoadDailyProgressAsync: Unexpected error: {ex.GetType().Name} - {ex.Message}. Returning empty list.");
            return [];
        }
    }

    // Made internal for DataImportExportService
    internal async Task SaveDailyProgressListAsync(List<DailyProgress> progressList)
    {
        bool acquiredLock = false;

        lock (_progressSaveLock)
        {
            if (_isSavingProgress)
            {
                Debug.WriteLine("ProgressStorageService.SaveDailyProgressListAsync: Save already in progress, skipping.");
                return;
            }
            _isSavingProgress = true;
            acquiredLock = true;
        }

        if (!acquiredLock)
        {
            return;
        }

        Debug.WriteLine("ProgressStorageService.SaveDailyProgressListAsync: Starting save operation.");
        try
        {
            List<DailyProgress> progressToSave = progressList?.ToList() ?? [];
            JsonSerializerOptions options = new() { WriteIndented = true };
            string json = JsonSerializer.Serialize(progressToSave, options);

            Debug.WriteLine($"ProgressStorageService.SaveDailyProgressListAsync: Writing JSON ({json.Length} chars) to {_progressFilePath}");
            await File.WriteAllTextAsync(_progressFilePath, json);
            Debug.WriteLine("ProgressStorageService.SaveDailyProgressListAsync: Write operation completed.");
        }
        catch (IOException ioEx)
        {
            Debug.WriteLine($"ProgressStorageService.SaveDailyProgressListAsync: IO Error writing file: {ioEx.Message}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ProgressStorageService.SaveDailyProgressListAsync: Unexpected error: {ex.GetType().Name} - {ex.Message}");
        }
        finally
        {
            lock (_progressSaveLock)
            {
                _isSavingProgress = false;
                Debug.WriteLine("ProgressStorageService.SaveDailyProgressListAsync: Save lock released.");
            }
        }
    }

    public async Task AddOrUpdateDailyProgressEntryAsync(DailyProgress newEntry)
    {
        Debug.WriteLine($"ProgressStorageService.AddOrUpdateDailyProgressEntryAsync: Processing entry for date: {newEntry.Date}");
        List<DailyProgress> currentProgress = await LoadDailyProgressAsync();

        DailyProgress? existingEntry = currentProgress.FirstOrDefault(p => p.Date.Date == newEntry.Date.Date);

        if (existingEntry != null)
        {
            Debug.WriteLine($"ProgressStorageService.AddOrUpdateDailyProgressEntryAsync: Updating existing entry for date: {newEntry.Date}");
            existingEntry.PercentageCompleted = newEntry.PercentageCompleted;
        }
        else
        {
            Debug.WriteLine($"ProgressStorageService.AddOrUpdateDailyProgressEntryAsync: Adding as new entry for date: {newEntry.Date}");
            currentProgress.Add(newEntry);
        }

        await SaveDailyProgressListAsync(currentProgress);
        Debug.WriteLine($"ProgressStorageService.AddOrUpdateDailyProgressEntryAsync: Successfully added/updated and saved entry for date: {newEntry.Date}");
    }
}