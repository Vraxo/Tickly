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
    }

    public async Task<List<DailyProgress>> LoadDailyProgressAsync()
    {
        if (!File.Exists(_progressFilePath))
        {
            return [];
        }

        try
        {
            string json = await File.ReadAllTextAsync(_progressFilePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return [];
            }

            List<DailyProgress>? loadedProgress = JsonSerializer.Deserialize<List<DailyProgress>>(json);
            return loadedProgress ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
        catch (IOException)
        {
            return [];
        }
        catch (Exception)
        {
            return [];
        }
    }

    internal async Task SaveDailyProgressListAsync(List<DailyProgress> progressList)
    {
        bool acquiredLock = false;

        lock (_progressSaveLock)
        {
            if (_isSavingProgress)
            {
                return;
            }
            _isSavingProgress = true;
            acquiredLock = true;
        }

        if (!acquiredLock)
        {
            return;
        }

        try
        {
            List<DailyProgress> progressToSave = progressList?.ToList() ?? [];
            JsonSerializerOptions options = new() { WriteIndented = true };
            string json = JsonSerializer.Serialize(progressToSave, options);
            await File.WriteAllTextAsync(_progressFilePath, json);
        }
        catch (IOException)
        {
            // Log or handle
        }
        catch (Exception)
        {
            // Log or handle
        }
        finally
        {
            lock (_progressSaveLock)
            {
                _isSavingProgress = false;
            }
        }
    }

    public async Task AddOrUpdateDailyProgressEntryAsync(DailyProgress newEntry)
    {
        List<DailyProgress> currentProgress = await LoadDailyProgressAsync();
        DailyProgress? existingEntry = currentProgress.FirstOrDefault(p => p.Date.Date == newEntry.Date.Date);

        if (existingEntry != null)
        {
            existingEntry.PercentageCompleted = newEntry.PercentageCompleted;
        }
        else
        {
            currentProgress.Add(newEntry);
        }
        await SaveDailyProgressListAsync(currentProgress);
    }

    public async Task ClearAllProgressAsync()
    {
        bool acquiredLock = false;
        lock (_progressSaveLock)
        {
            if (_isSavingProgress)
            {
                return;
            }
            _isSavingProgress = true;
            acquiredLock = true;
        }

        if (!acquiredLock)
        {
            return;
        }

        try
        {
            if (File.Exists(_progressFilePath))
            {
                File.Delete(_progressFilePath);
            }
        }
        catch (IOException)
        {
            // Log or handle
        }
        catch (Exception)
        {
            // Log or handle
        }
        finally
        {
            lock (_progressSaveLock)
            {
                _isSavingProgress = false;
            }
        }
    }
}