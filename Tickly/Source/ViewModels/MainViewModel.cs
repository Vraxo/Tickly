// File: Source\ViewModels\MainViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics; // Needed for Color
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Tickly.Messages;
using Tickly.Models;
using Tickly.Services;
using Tickly.Utils;
using Tickly.Views;

namespace Tickly.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private ObservableCollection<TaskItem> _tasks;
    public ObservableCollection<TaskItem> Tasks
    {
        get => _tasks;
        set => SetProperty(ref _tasks, value);
    }

    private readonly string _filePath;
    private bool _isSaving = false;
    private readonly object _saveLock = new();
    private Timer? _debounceTimer;

    [ObservableProperty]
    private double taskProgress;

    [ObservableProperty]
    private Color taskProgressColor;

    // --- State for Daily Progress Tracking ---
    private HashSet<Guid> _initialRelevantTaskIdsToday = new();
    private HashSet<Guid> _completedTaskIdsToday = new();
    private DateTime _lastProgressUpdateDay = DateTime.MinValue; // Initialize to force update on first load
    // --- End State ---


    // Define the start (top) and end (bottom) colors for the ITEM POSITION gradient
    private static readonly Color StartColor = Colors.Red;
    private static readonly Color EndColor = Colors.LimeGreen;

    // Define the start (bottom/0%) and end (top/100%) colors for the PROGRESS BAR gradient
    // We interpolate from Red (0% done) to Green (100% done)
    private static readonly Color ProgressStartColor = Colors.Red;    // Color for 0% completion
    private static readonly Color ProgressEndColor = Colors.LimeGreen; // Color for 100% completion


    public MainViewModel()
    {
        _filePath = Path.Combine(FileSystem.AppDataDirectory, "tasks.json");
        _tasks = new();

        _ = LoadTasksAsync();

        WeakReferenceMessenger.Default.Register<AddTaskMessage>(this, (recipient, message) => HandleAddTask(message.Value));
        WeakReferenceMessenger.Default.Register<UpdateTaskMessage>(this, (recipient, message) => HandleUpdateTask(message.Value));
        WeakReferenceMessenger.Default.Register<DeleteTaskMessage>(this, (recipient, message) => HandleDeleteTask(message.Value));
        WeakReferenceMessenger.Default.Register<CalendarSettingChangedMessage>(this, async (recipient, message) => await HandleCalendarSettingChanged());
        WeakReferenceMessenger.Default.Register<TasksReloadRequestedMessage>(this, async (recipient, message) => await HandleTasksReloadRequested());

    }

    // Method to check and reset daily progress counters
    private void CheckAndResetDailyProgress(List<TaskItem> tasksForBaseline)
    {
        if (DateTime.Today != _lastProgressUpdateDay)
        {
            Debug.WriteLine($"CheckAndResetDailyProgress: New day detected. Resetting progress counters for {DateTime.Today:yyyy-MM-dd}.");
            _initialRelevantTaskIdsToday.Clear();
            _completedTaskIdsToday.Clear();
            _lastProgressUpdateDay = DateTime.Today;

            // Establish the baseline for today based on the tasks *as they were loaded/passed in*
            foreach (var task in tasksForBaseline)
            {
                if (IsTaskRelevantForTodayBaseline(task))
                {
                    _initialRelevantTaskIdsToday.Add(task.Id);
                }
            }
            Debug.WriteLine($"CheckAndResetDailyProgress: Initial relevant tasks count for today: {_initialRelevantTaskIdsToday.Count}");
        }
    }

    // Helper to determine relevance for baseline calculation (using the date the check started)
    private bool IsTaskRelevantForTodayBaseline(TaskItem task)
    {
        DateTime today = _lastProgressUpdateDay; // Use the day we are calculating for
        if (task.TimeType == TaskTimeType.None) return true; // Anytime tasks are always relevant for the baseline
        if (task.DueDate.HasValue && task.DueDate.Value.Date == today) return true; // Due today (Specific or Repeating instance)
        return false;
    }


    [RelayCommand]
    public async Task LoadTasksAsync() // Made public for potential call on resume
    {
        bool acquiredLock = false;
        lock (_saveLock) { if (_isSaving) return; acquiredLock = true; }
        if (!acquiredLock) return;

        Debug.WriteLine($"LoadTasksAsync: Attempting to load tasks from: {_filePath}");
        List<TaskItem> loadedTasks = []; // Tasks loaded directly from file
        bool wasSubscribed = Tasks?.Count > 0; // Check if currently subscribed before clearing/reloading

        try
        {
            if (Tasks != null) { Tasks.CollectionChanged -= Tasks_CollectionChanged; } // Unsubscribe before modifying

            if (File.Exists(_filePath))
            {
                string json = await File.ReadAllTextAsync(_filePath);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    try { loadedTasks = JsonSerializer.Deserialize<List<TaskItem>>(json) ?? []; }
                    catch (JsonException jsonException) { Debug.WriteLine($"LoadTasksAsync: Error deserializing tasks JSON: {jsonException.Message}"); loadedTasks = []; }
                }
            }

            // --- Daily Progress Reset ---
            // Pass the raw loaded list BEFORE advancing repeating tasks
            CheckAndResetDailyProgress(new List<TaskItem>(loadedTasks)); // Pass a copy
            // --- End Daily Progress Reset ---

            DateTime today = DateTime.Today; // Use current today for advancing logic
            bool changesMade = false;
            // Advance repeating tasks *after* setting the baseline
            foreach (TaskItem task in loadedTasks)
            {
                if (task.TimeType == TaskTimeType.Repeating && task.DueDate.HasValue && task.DueDate.Value.Date < today)
                {
                    DateTime originalDueDate = task.DueDate.Value.Date; // Keep original for potential future use
                    DateTime nextValidDueDate = CalculateNextValidDueDateForRepeatingTask(task, today, originalDueDate);
                    if (task.DueDate.Value.Date != nextValidDueDate)
                    {
                        task.DueDate = nextValidDueDate;
                        changesMade = true;
                        Debug.WriteLine($"LoadTasksAsync: Advanced repeating task '{task.Title}' to {nextValidDueDate:yyyy-MM-dd}");
                    }
                }
                task.IsFadingOut = false;
                task.PositionColor = Colors.Transparent; // Reset color before recalculation
            }

            List<TaskItem> tasksToDisplay = loadedTasks.OrderBy(task => task.Order).ToList();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Tasks.Clear(); // Clear existing items
                foreach (TaskItem task in tasksToDisplay) { Tasks.Add(task); } // Add loaded and processed tasks
                UpdateTaskIndexAndColorProperty(); // Update order, index, and item colors
                UpdateTaskProgressAndColor(); // Calculate progress based on current state and daily counters
            });

            if (changesMade) { TriggerSave(); }
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"LoadTasksAsync: Error loading tasks: {exception.GetType().Name} - {exception.Message}");
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Tasks?.Clear(); // Use null conditional access
                UpdateTaskProgressAndColor();
                UpdateTaskIndexAndColorProperty();
            });
        }
        finally
        {
            // Re-subscribe only if Tasks is not null
            if (Tasks != null)
            {
                // Ensure we don't double-subscribe
                Tasks.CollectionChanged -= Tasks_CollectionChanged;
                Tasks.CollectionChanged += Tasks_CollectionChanged;
            }
            if (acquiredLock) { lock (_saveLock) { _isSaving = false; } } // Release lock
        }
    }

    private static DateTime CalculateNextValidDueDateForRepeatingTask(TaskItem task, DateTime today, DateTime originalDueDate)
    {
        DateTime nextValidDueDate = originalDueDate;
        switch (task.RepetitionType)
        {
            case TaskRepetitionType.Daily: nextValidDueDate = today; break;
            case TaskRepetitionType.AlternateDay:
                // Ensure the next date is today or later, preserving the even/odd day pattern
                if (originalDueDate < today)
                {
                    double daysDifference = (today - originalDueDate).TotalDays;
                    // If the difference is odd, we need to advance one more day to maintain the pattern relative to today
                    nextValidDueDate = (daysDifference % 2 != 0) ? today.AddDays(1) : today;
                }
                else // Original date is today or in the future, no change needed yet based on 'today'
                {
                    nextValidDueDate = originalDueDate;
                }
                break;
            case TaskRepetitionType.Weekly:
                if (task.RepetitionDayOfWeek.HasValue)
                {
                    // Find the next occurrence including or after today
                    nextValidDueDate = DateUtils.GetNextWeekday(today, task.RepetitionDayOfWeek.Value);
                }
                else // Fallback: if no specific day, just add 7 days (shouldn't happen with UI)
                {
                    while (nextValidDueDate < today) nextValidDueDate = nextValidDueDate.AddDays(7);
                }
                break;
        }
        return nextValidDueDate.Date; // Return only the date part
    }

    [RelayCommand]
    private async Task MarkTaskDone(TaskItem? task)
    {
        if (task is null || task.IsFadingOut) return;

        // --- Progress Tracking ---
        // Ensure counters are up-to-date for the current day
        CheckAndResetDailyProgress(Tasks?.ToList() ?? new List<TaskItem>());

        bool wasInitiallyRelevant = _initialRelevantTaskIdsToday.Contains(task.Id);
        bool alreadyCompleted = _completedTaskIdsToday.Contains(task.Id);

        if (wasInitiallyRelevant && !alreadyCompleted)
        {
            _completedTaskIdsToday.Add(task.Id);
            Debug.WriteLine($"MarkTaskDone: Marked task '{task.Title}' (ID: {task.Id}) as completed for today's progress.");
            // Update progress immediately *after* state change but *before* visual removal delay
            await MainThread.InvokeOnMainThreadAsync(UpdateTaskProgressAndColor);
        }
        else if (wasInitiallyRelevant) // Already completed
        {
            Debug.WriteLine($"MarkTaskDone: Task '{task.Title}' (ID: {task.Id}) was already marked completed for progress today.");
        }
        else // Not initially relevant
        {
            Debug.WriteLine($"MarkTaskDone: Task '{task.Title}' (ID: {task.Id}) was not in the initial relevant list for today, progress not affected.");
        }
        // --- End Progress Tracking ---


        if (task.TimeType == TaskTimeType.None || task.TimeType == TaskTimeType.SpecificDate)
        {
            task.IsFadingOut = true;
            await Task.Delay(350); // UI fade-out time
            bool removedSuccessfully = false;
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                removedSuccessfully = Tasks.Remove(task);
                if (removedSuccessfully)
                {
                    UpdateTaskIndexAndColorProperty(); // Update remaining task colors/indices
                    // Progress was already updated when completion was registered
                    TriggerSave();
                }
                else { task.IsFadingOut = false; } // Should not happen, but reset if removal failed
            });
        }
        else if (task.TimeType == TaskTimeType.Repeating)
        {
            DateTime? nextDueDate = DateUtils.CalculateNextDueDate(task);
            if (nextDueDate.HasValue)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    task.DueDate = nextDueDate;
                    UpdateTaskIndexAndColorProperty(); // Update position/color if needed
                    // Progress was already updated when completion was registered
                    TriggerSave();
                });
            }
            else // Handle case where repetition might end (though not currently implemented)
            {
                task.IsFadingOut = true;
                await Task.Delay(350);
                bool removedSuccessfully = false;
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    removedSuccessfully = Tasks.Remove(task);
                    if (removedSuccessfully)
                    {
                        UpdateTaskIndexAndColorProperty();
                        // Progress was already updated
                        TriggerSave();
                    }
                    else { task.IsFadingOut = false; }
                });
            }
        }
    }

    private async Task HandleCalendarSettingChanged()
    {
        // Reload tasks to ensure dates are formatted correctly
        await LoadTasksAsync();
    }

    // Handle Add - Do not modify the initial count for today, just update the UI
    private async void HandleAddTask(TaskItem? newTask)
    {
        if (newTask is null) return;
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            // Ensure daily counters are initialized if the app was just launched
            CheckAndResetDailyProgress(Tasks?.ToList() ?? new List<TaskItem>());

            newTask.Order = Tasks.Count;
            Tasks.Add(newTask);
            UpdateTaskIndexAndColorProperty();
            UpdateTaskProgressAndColor(); // Recalculate progress display, denominator is stable for the day
            TriggerSave();
        });
    }

    private async void HandleUpdateTask(TaskItem? updatedTask)
    {
        if (updatedTask is null) return;

        // Get the state *before* the update on the main thread
        bool wasInitiallyRelevant = _initialRelevantTaskIdsToday.Contains(updatedTask.Id);
        bool isNowRelevant = IsTaskRelevantForTodayBaseline(updatedTask); // Check if relevant *now*

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            // Ensure daily counters are initialized
            CheckAndResetDailyProgress(Tasks?.ToList() ?? new List<TaskItem>());

            int index = Tasks.ToList().FindIndex(task => task.Id == updatedTask.Id);
            if (index != -1)
            {
                // Preserve original order/index
                updatedTask.Order = Tasks[index].Order;
                updatedTask.Index = index;

                // --- Adjust Progress Counters if Relevance Changed ---
                if (wasInitiallyRelevant && !isNowRelevant)
                {
                    _initialRelevantTaskIdsToday.Remove(updatedTask.Id);
                    _completedTaskIdsToday.Remove(updatedTask.Id); // Also remove if it was completed
                    Debug.WriteLine($"HandleUpdateTask: Task {updatedTask.Id} became irrelevant, removed from progress tracking.");
                }
                else if (!wasInitiallyRelevant && isNowRelevant)
                {
                    _initialRelevantTaskIdsToday.Add(updatedTask.Id);
                    Debug.WriteLine($"HandleUpdateTask: Task {updatedTask.Id} became relevant, added to progress tracking baseline.");
                    // Don't mark as completed automatically
                }
                // --- End Adjust Progress Counters ---

                UpdateTaskIndexAndColorPropertyInternal(); // Update colors immediately
                Tasks[index] = updatedTask; // Replace item in the collection
                UpdateTaskProgressAndColor(); // Recalculate progress
                TriggerSave();
            }
            else Debug.WriteLine($"HandleUpdateTask: Task with ID {updatedTask.Id} not found.");
        });
    }

    private async void HandleDeleteTask(Guid taskId)
    {
        // Ensure daily counters are initialized
        CheckAndResetDailyProgress(Tasks?.ToList() ?? new List<TaskItem>());

        bool wasInitiallyRelevant = _initialRelevantTaskIdsToday.Contains(taskId);
        bool wasCompleted = _completedTaskIdsToday.Contains(taskId);
        bool removedSuccessfully = false;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            TaskItem? taskToRemove = Tasks.FirstOrDefault(task => task.Id == taskId);
            if (taskToRemove is not null)
            {
                removedSuccessfully = Tasks.Remove(taskToRemove);
                if (removedSuccessfully)
                {
                    // --- Adjust Progress Counters ---
                    if (wasInitiallyRelevant)
                    {
                        _initialRelevantTaskIdsToday.Remove(taskId);
                        Debug.WriteLine($"HandleDeleteTask: Removed task ID {taskId} from initial relevant list.");
                        if (wasCompleted)
                        {
                            _completedTaskIdsToday.Remove(taskId);
                            Debug.WriteLine($"HandleDeleteTask: Removed task ID {taskId} from completed list.");
                        }
                    }
                    // --- End Adjust Progress Counters ---

                    UpdateTaskIndexAndColorProperty(); // Update remaining task colors/indices
                    UpdateTaskProgressAndColor();      // Recalculate progress
                    TriggerSave();
                }
            }
        });
    }

    private void Tasks_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs eventArgs)
    {
        // Only update progress/color if the change wasn't handled internally already
        // This might be redundant now, but safer.
        if (eventArgs.Action != NotifyCollectionChangedAction.Move) // Moves are handled by UpdateTaskIndexAndColorProperty
        {
            UpdateTaskProgressAndColor();
        }
        UpdateTaskIndexAndColorProperty(); // Always update indices/colors on changes
        if (eventArgs.Action == NotifyCollectionChangedAction.Move) { TriggerSave(); } // Save order on move
    }


    private void UpdateTaskIndexAndColorProperty()
    {
        if (!MainThread.IsMainThread)
        {
            MainThread.BeginInvokeOnMainThread(UpdateTaskIndexAndColorPropertyInternal);
            return;
        }
        UpdateTaskIndexAndColorPropertyInternal();
    }


    private void UpdateTaskIndexAndColorPropertyInternal()
    {
        int totalCount = Tasks.Count;
        // Debug.WriteLine($"UpdateTaskIndexAndColorPropertyInternal: Updating for {totalCount} tasks.");

        for (int i = 0; i < totalCount; i++)
        {
            TaskItem currentTask = Tasks[i];
            if (currentTask != null)
            {
                // Update Order and Index
                if (currentTask.Order != i) currentTask.Order = i;
                if (currentTask.Index != i) currentTask.Index = i;

                // Calculate and Set Item Color (Task List Gradient)
                Color newItemColor;
                if (totalCount <= 1)
                {
                    newItemColor = StartColor; // Use StartColor for the item position gradient
                }
                else
                {
                    double factor = (double)i / (totalCount - 1);
                    factor = Math.Clamp(factor, 0.0, 1.0);

                    // Use StartColor and EndColor for the item position gradient
                    float r = (float)(StartColor.Red + factor * (EndColor.Red - StartColor.Red));
                    float g = (float)(StartColor.Green + factor * (EndColor.Green - StartColor.Green));
                    float b = (float)(StartColor.Blue + factor * (EndColor.Blue - StartColor.Blue));
                    float a = (float)(StartColor.Alpha + factor * (EndColor.Alpha - StartColor.Alpha));
                    newItemColor = new Color(r, g, b, a);
                }

                if (currentTask.PositionColor != newItemColor)
                {
                    currentTask.PositionColor = newItemColor;
                }
            }
        }
    }

    private async Task SaveTasks()
    {
        bool acquiredLock = false;
        List<TaskItem> tasksToSave = [];
        try
        {
            lock (_saveLock) { if (_isSaving) { return; } _isSaving = true; acquiredLock = true; }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                // Ensure order is correct before saving
                for (int i = 0; i < Tasks.Count; i++) { Tasks[i].Order = i; }
                tasksToSave = new List<TaskItem>(Tasks);
            });

            if (tasksToSave != null && tasksToSave.Count > 0)
            {
                JsonSerializerOptions options = new() { WriteIndented = true };
                string json = JsonSerializer.Serialize(tasksToSave, options);
                await File.WriteAllTextAsync(_filePath, json);
                // Debug.WriteLine($"SaveTasks: Saved {tasksToSave.Count} tasks.");
            }
            else if (tasksToSave != null && tasksToSave.Count == 0)
            {
                if (File.Exists(_filePath)) { File.Delete(_filePath); }
                // Debug.WriteLine($"SaveTasks: Task list empty, deleted file.");
            }
        }
        catch (Exception exception) { Debug.WriteLine($"SaveTasks: Error saving tasks: {exception.Message}"); }
        finally { if (acquiredLock) { lock (_saveLock) { _isSaving = false; } } }
    }

    private void TriggerSave()
    {
        _debounceTimer?.Dispose();
        _debounceTimer = new Timer(async (state) => { await SaveTasks(); _debounceTimer?.Dispose(); _debounceTimer = null; }, null, TimeSpan.FromMilliseconds(500), Timeout.InfiniteTimeSpan);
    }

    private async Task HandleTasksReloadRequested()
    {
        Debug.WriteLine("MainViewModel: Received TasksReloadRequestedMessage. Reloading tasks...");
        // Reset progress tracking for the new list
        _lastProgressUpdateDay = DateTime.MinValue;
        await LoadTasksAsync();
    }

    private void UpdateTaskProgressAndColor()
    {
        // Ensure daily counters are initialized if needed (e.g., app resume)
        // Pass the current Tasks list for baseline check if day changed
        CheckAndResetDailyProgress(Tasks?.ToList() ?? new List<TaskItem>());

        int totalInitialTasks = _initialRelevantTaskIdsToday.Count;
        int completedCount = _completedTaskIdsToday.Count;

        double progressValue;
        if (totalInitialTasks <= 0)
        {
            progressValue = 1.0; // 100% complete if there were no relevant tasks initially
                                 // Debug.WriteLine("UpdateTaskProgress: No initial tasks relevant for today. Progress set to 1.0 (100%).");
        }
        else
        {
            progressValue = Math.Clamp((double)completedCount / totalInitialTasks, 0.0, 1.0);
            // Debug.WriteLine($"UpdateTaskProgress: Completed={completedCount}, InitialTotal={totalInitialTasks}, Progress={progressValue:F2}");
        }

        // Set the progress value for the ProgressBar binding
        TaskProgress = progressValue;

        // Interpolate color from Red (0% complete - ProgressStartColor) to Green (100% complete - ProgressEndColor)
        float redComponent = (float)(ProgressStartColor.Red + progressValue * (ProgressEndColor.Red - ProgressStartColor.Red));
        float greenComponent = (float)(ProgressStartColor.Green + progressValue * (ProgressEndColor.Green - ProgressStartColor.Green));
        float blueComponent = (float)(ProgressStartColor.Blue + progressValue * (ProgressEndColor.Blue - ProgressStartColor.Blue));
        float alphaComponent = (float)(ProgressStartColor.Alpha + progressValue * (ProgressEndColor.Alpha - ProgressStartColor.Alpha));


        // Clamp color components just in case
        redComponent = Math.Clamp(redComponent, 0.0f, 1.0f);
        greenComponent = Math.Clamp(greenComponent, 0.0f, 1.0f);
        blueComponent = Math.Clamp(blueComponent, 0.0f, 1.0f);
        alphaComponent = Math.Clamp(alphaComponent, 0.0f, 1.0f);


        // Set the color for the ProgressBar binding
        TaskProgressColor = new Color(redComponent, greenComponent, blueComponent, alphaComponent);

        Debug.WriteLine($"UpdateTaskProgress: Final Progress={TaskProgress:F2}, Color=({redComponent:F2},{greenComponent:F2},{blueComponent:F2},{alphaComponent:F2})");
    }
}