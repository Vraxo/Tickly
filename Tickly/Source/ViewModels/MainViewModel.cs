// ViewModels/MainViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel; // Needed for MainThread
using Tickly.Messages;
using Tickly.Models;
using Tickly.Views;

namespace Tickly.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<TaskItem> _tasks;

    private readonly string _filePath;
    private bool _isSaving = false;
    private readonly object _saveLock = new object();

    public MainViewModel()
    {
        _filePath = Path.Combine(FileSystem.AppDataDirectory, "tasks.json");
        _tasks = new ObservableCollection<TaskItem>();
        _tasks.CollectionChanged += Tasks_CollectionChanged;

        LoadTasksCommand.Execute(null);

        // Register message listeners
        WeakReferenceMessenger.Default.Register<AddTaskMessage>(this, (r, m) => HandleAddTask(m.Value));
        WeakReferenceMessenger.Default.Register<UpdateTaskMessage>(this, (r, m) => HandleUpdateTask(m.Value));
        WeakReferenceMessenger.Default.Register<DeleteTaskMessage>(this, (r, m) => HandleDeleteTask(m.Value));
        WeakReferenceMessenger.Default.Register<CalendarSettingChangedMessage>(this, (r, m) => HandleCalendarSettingChanged());
    }

    // --- Commands ---

    [RelayCommand]
    private async Task NavigateToAddPage()
    {
        await Shell.Current.GoToAsync(nameof(AddTaskPopupPage), true);
    }

    [RelayCommand]
    private async Task NavigateToEditPage(TaskItem taskToEdit)
    {
        if (taskToEdit == null) return;
        Debug.WriteLine($"Navigating to edit task: {taskToEdit.Title} ({taskToEdit.Id})");
        var navigationParameter = new Dictionary<string, object> { { "TaskToEdit", taskToEdit } };
        await Shell.Current.GoToAsync(nameof(AddTaskPopupPage), true, navigationParameter);
    }

    [RelayCommand]
    private async Task LoadTasks()
    {
        lock (_saveLock) { if (_isSaving) { Debug.WriteLine("LoadTasks skipped, save in progress."); return; } }
        Debug.WriteLine($"LoadTasks: Attempting to load tasks from: {_filePath}");

        Tasks.CollectionChanged -= Tasks_CollectionChanged;
        try
        {
            if (!File.Exists(_filePath)) { Debug.WriteLine("LoadTasks: Task file not found."); if (Tasks.Any()) Tasks.Clear(); return; }
            string json = await File.ReadAllTextAsync(_filePath);
            if (string.IsNullOrWhiteSpace(json)) { Debug.WriteLine("LoadTasks: Task file is empty."); if (Tasks.Any()) Tasks.Clear(); return; }

            var loadedTasks = JsonSerializer.Deserialize<List<TaskItem>>(json);
            var tasksToAdd = loadedTasks?.OrderBy(t => t.Order).ToList() ?? new List<TaskItem>();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                Tasks.Clear();
                foreach (var task in tasksToAdd)
                {
                    task.IsFadingOut = false; // Ensure animation state is reset on load
                    Debug.WriteLine($"LoadTasks: Loading Task='{task.Title}', TimeType='{task.TimeType}'");
                    Tasks.Add(task);
                }
                Debug.WriteLine($"LoadTasks: Successfully loaded {Tasks.Count} tasks.");
                OnPropertyChanged(nameof(Tasks));
            });
        }
        catch (Exception ex) { Debug.WriteLine($"LoadTasks: Error - {ex.GetType().Name}: {ex.Message}"); MainThread.BeginInvokeOnMainThread(Tasks.Clear); }
        finally { Tasks.CollectionChanged += Tasks_CollectionChanged; Debug.WriteLine("LoadTasks finished."); }
    }

    // --- NEW Command to Mark Task Done ---
    [RelayCommand]
    private async Task MarkTaskDone(TaskItem? task)
    {
        if (task == null || task.IsFadingOut) // Prevent double clicks during fade
        {
            Debug.WriteLine("MarkTaskDone: Task is null or already fading out.");
            return;
        }

        Debug.WriteLine($"MarkTaskDone: Processing task '{task.Title}', TimeType: {task.TimeType}");

        // --- Logic for One-Time / Specific Date Tasks ---
        if (task.TimeType == TaskTimeType.None || task.TimeType == TaskTimeType.SpecificDate)
        {
            Debug.WriteLine($"MarkTaskDone: Task '{task.Title}' is one-time/specific. Fading out and removing.");
            // 1. Trigger Animation (set property, UI binding handles opacity change)
            task.IsFadingOut = true;

            // 2. Wait for animation (adjust duration as needed)
            await Task.Delay(350); // e.g., 350 milliseconds

            // 3. Remove from collection (ensure this happens on UI thread if Task.Delay switched context)
            MainThread.BeginInvokeOnMainThread(() =>
            {
                bool removed = Tasks.Remove(task);
                Debug.WriteLine($"MarkTaskDone: Removed task '{task.Title}' from collection: {removed}");
                if (removed)
                {
                    // 4. Trigger save (can be awaited or run async void)
                    _ = TriggerSave(); // Fire and forget save after removal
                }
                else
                {
                    // If removal failed, reset fade state (though this shouldn't happen if task was valid)
                    task.IsFadingOut = false;
                }
            });
        }
        // --- Logic for Repeating Tasks ---
        else if (task.TimeType == TaskTimeType.Repeating)
        {
            Debug.WriteLine($"MarkTaskDone: Task '{task.Title}' is repeating. Calculating next date and moving.");

            // 1. Calculate Next Due Date
            DateTime? nextDueDate = CalculateNextDueDate(task);
            if (nextDueDate.HasValue)
            {
                Debug.WriteLine($"MarkTaskDone: Next due date calculated: {nextDueDate.Value:O}");
                task.DueDate = nextDueDate; // Update the task's date

                // 2. Move to Bottom (Remove and Add)
                // Ensure operations on ObservableCollection are on the UI thread
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (Tasks.Remove(task)) // Remove from current position
                    {
                        Tasks.Add(task); // Add to the end
                        Debug.WriteLine($"MarkTaskDone: Moved task '{task.Title}' to the end.");
                        // 3. Trigger save (can be awaited or run async void)
                        _ = TriggerSave(); // Fire and forget save after move
                    }
                    else
                    {
                        Debug.WriteLine($"MarkTaskDone: Failed to remove repeating task '{task.Title}' before moving.");
                    }
                });
            }
            else
            {
                Debug.WriteLine($"MarkTaskDone: Could not calculate next due date for task '{task.Title}'. Not moving or saving.");
                // Optionally handle this case - maybe treat as non-repeating?
            }
        }
    }

    // --- Helper Methods ---

    private DateTime? CalculateNextDueDate(TaskItem task)
    {
        // Use current DueDate as base, fallback to Today if null (shouldn't happen for valid repeating tasks)
        DateTime baseDate = task.DueDate ?? DateTime.Today;

        switch (task.RepetitionType)
        {
            case TaskRepetitionType.Daily:
                return baseDate.AddDays(1);

            case TaskRepetitionType.AlternateDay:
                return baseDate.AddDays(2);

            case TaskRepetitionType.Weekly:
                if (task.RepetitionDayOfWeek.HasValue)
                {
                    // Find the next occurrence of the specified DayOfWeek strictly *after* the baseDate
                    DateTime nextDate = baseDate.AddDays(1); // Start checking from the day *after* the base date
                    while (nextDate.DayOfWeek != task.RepetitionDayOfWeek.Value)
                    {
                        nextDate = nextDate.AddDays(1);
                    }
                    return nextDate;
                }
                else
                {
                    // Fallback for weekly if no specific day is set (shouldn't happen with current UI)
                    return baseDate.AddDays(7);
                }

            default:
                return null; // Unknown repetition type
        }
    }


    // --- Message Handlers ---

    private void HandleCalendarSettingChanged()
    {
        Debug.WriteLine("MainViewModel: Received CalendarSettingChangedMessage. Triggering LoadTasksCommand.");
        if (LoadTasksCommand.CanExecute(null)) { LoadTasksCommand.Execute(null); }
        else { Debug.WriteLine("MainViewModel: LoadTasksCommand cannot execute."); }
    }

    private async void HandleAddTask(TaskItem newTask)
    {
        if (newTask == null) return;
        Debug.WriteLine($"Received AddTaskMessage for: {newTask.Title}, TimeType: {newTask.TimeType}");
        newTask.Order = Tasks.Count;
        Tasks.Add(newTask);
        await TriggerSave();
    }

    private async void HandleUpdateTask(TaskItem updatedTask)
    {
        // Uses Item Replacement for UI Refresh
        if (updatedTask == null) return;
        int index = -1;
        for (int i = 0; i < Tasks.Count; i++) { if (Tasks[i].Id == updatedTask.Id) { index = i; break; } }
        if (index != -1)
        {
            Debug.WriteLine($"HandleUpdateTask: Found task '{updatedTask.Title}' at index {index}. Replacing item.");
            updatedTask.Order = index;
            Tasks[index] = updatedTask;
            await TriggerSave();
        }
        else { Debug.WriteLine($"HandleUpdateTask: Update failed: Task with ID {updatedTask.Id} not found."); }
    }

    private async void HandleDeleteTask(Guid taskId)
    {
        var taskToRemove = Tasks.FirstOrDefault(t => t.Id == taskId);
        if (taskToRemove != null)
        {
            Debug.WriteLine($"Received DeleteTaskMessage for: {taskToRemove.Title} ({taskId})");
            Tasks.Remove(taskToRemove);
            await TriggerSave();
        }
        else { Debug.WriteLine($"Delete failed: Task with ID {taskId} not found."); }
    }

    // --- Saving Logic ---

    private async void Tasks_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        // Only need to explicitly save on Move, other actions trigger save via handlers
        if (e.Action == NotifyCollectionChangedAction.Move)
        {
            Debug.WriteLine($"CollectionChanged: Action=Move. Triggering save.");
            await TriggerSave();
        }
        else { Debug.WriteLine($"CollectionChanged: Action={e.Action}"); }
    }

    private async Task SaveTasks()
    {
        bool acquiredLock = false;
        try
        {
            lock (_saveLock) { if (_isSaving) { Debug.WriteLine("SaveTasks: Save already in progress. Exiting."); return; } _isSaving = true; acquiredLock = true; }

            List<TaskItem> tasksToSave;
            lock (Tasks)
            {
                for (int i = 0; i < Tasks.Count; i++) { if (Tasks[i].Order != i) { Tasks[i].Order = i; } }
                tasksToSave = new List<TaskItem>(Tasks);
            }
            Debug.WriteLine($"SaveTasks: Attempting to save {tasksToSave.Count} tasks...");

            string json = JsonSerializer.Serialize(tasksToSave, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_filePath, json);
            Debug.WriteLine($"SaveTasks: Tasks saved successfully.");
        }
        catch (Exception ex) { Debug.WriteLine($"SaveTasks: Error saving tasks: {ex.Message}"); }
        finally { if (acquiredLock) { lock (_saveLock) { _isSaving = false; } Debug.WriteLine("SaveTasks finished, _isSaving reset."); } }
    }

    private async Task TriggerSave()
    {
        lock (_saveLock) { if (_isSaving) { Debug.WriteLine("TriggerSave: Skipped, save already in progress/pending."); return; } }
        Debug.WriteLine("TriggerSave: Initiating save cycle...");
        await Task.Delay(300);
        await SaveTasks();
    }

} // End of MainViewModel class