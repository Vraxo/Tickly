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
using Tickly.Utils; // Needed for DateUtils

namespace Tickly.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<TaskItem> _tasks;

    private readonly string _filePath;
    private bool _isSaving = false;
    private readonly object _saveLock = new object(); // Used for coordinating SaveTasks/LoadTasks

    public MainViewModel()
    {
        _filePath = Path.Combine(FileSystem.AppDataDirectory, "tasks.json");
        _tasks = new ObservableCollection<TaskItem>();
        _tasks.CollectionChanged += Tasks_CollectionChanged;

        // Load initial data
        LoadTasksCommand.Execute(null);

        // Register message listeners for CRUD operations and settings changes
        WeakReferenceMessenger.Default.Register<AddTaskMessage>(this, (r, m) => HandleAddTask(m.Value));
        WeakReferenceMessenger.Default.Register<UpdateTaskMessage>(this, (r, m) => HandleUpdateTask(m.Value));
        WeakReferenceMessenger.Default.Register<DeleteTaskMessage>(this, (r, m) => HandleDeleteTask(m.Value));
        WeakReferenceMessenger.Default.Register<CalendarSettingChangedMessage>(this, (r, m) => HandleCalendarSettingChanged());
    }

    // --- Commands ---

    /// <summary>
    /// Navigates to the Add Task page.
    /// </summary>
    [RelayCommand]
    private async Task NavigateToAddPage()
    {
        await Shell.Current.GoToAsync(nameof(AddTaskPopupPage), true); // Modal navigation
    }

    /// <summary>
    /// Navigates to the Edit Task page, passing the selected task.
    /// </summary>
    [RelayCommand]
    private async Task NavigateToEditPage(TaskItem? taskToEdit) // Allow nullable TaskItem
    {
        if (taskToEdit == null)
        {
            Debug.WriteLine("NavigateToEditPage: taskToEdit is null.");
            return;
        }
        Debug.WriteLine($"Navigating to edit task: {taskToEdit.Title} ({taskToEdit.Id})");
        var navigationParameter = new Dictionary<string, object> { { "TaskToEdit", taskToEdit } };
        await Shell.Current.GoToAsync(nameof(AddTaskPopupPage), true, navigationParameter);
    }

    /// <summary>
    /// Loads tasks from the JSON file into the ObservableCollection.
    /// </summary>
    [RelayCommand]
    private async Task LoadTasks()
    {
        // Prevent loading during save
        lock (_saveLock) { if (_isSaving) { Debug.WriteLine("LoadTasks skipped, save in progress."); return; } }

        Debug.WriteLine($"LoadTasks: Attempting to load tasks from: {_filePath}");

        Tasks.CollectionChanged -= Tasks_CollectionChanged; // Unsubscribe during bulk load
        try
        {
            if (!File.Exists(_filePath))
            {
                Debug.WriteLine("LoadTasks: Task file not found. Clearing tasks.");
                if (Tasks.Any()) MainThread.BeginInvokeOnMainThread(Tasks.Clear); // Clear on UI thread
                return;
            }

            string json = await File.ReadAllTextAsync(_filePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.WriteLine("LoadTasks: Task file is empty. Clearing tasks.");
                if (Tasks.Any()) MainThread.BeginInvokeOnMainThread(Tasks.Clear); // Clear on UI thread
                return;
            }

            var loadedTasks = JsonSerializer.Deserialize<List<TaskItem>>(json);
            var tasksToAdd = loadedTasks?.OrderBy(t => t.Order).ToList() ?? new List<TaskItem>();

            // Update the UI collection on the main thread
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Tasks.Clear(); // Clear existing items
                foreach (var task in tasksToAdd)
                {
                    task.IsFadingOut = false; // Reset animation state on load
                    // Debug.WriteLine($"LoadTasks: Loading Task='{task.Title}', TimeType='{task.TimeType}'"); // Optional detailed log
                    Tasks.Add(task);
                }
                Debug.WriteLine($"LoadTasks: Successfully loaded and added {Tasks.Count} tasks.");
                // Optionally notify if bindings depend on the 'Tasks' property itself changing (though Clear/Add usually suffices)
                // OnPropertyChanged(nameof(Tasks));
            });
        }
        catch (JsonException jsonEx)
        {
            Debug.WriteLine($"LoadTasks: Error deserializing tasks JSON: {jsonEx.Message}");
            MainThread.BeginInvokeOnMainThread(Tasks.Clear); // Clear on error (UI thread)
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"LoadTasks: Error loading tasks: {ex.GetType().Name} - {ex.Message}");
            MainThread.BeginInvokeOnMainThread(Tasks.Clear); // Clear on error (UI thread)
        }
        finally
        {
            // Always re-subscribe
            Tasks.CollectionChanged += Tasks_CollectionChanged;
            Debug.WriteLine("LoadTasks finished.");
        }
    }

    /// <summary>
    /// Handles the action when a task's check circle is tapped.
    /// Removes one-time tasks or updates repeating tasks.
    /// </summary>
    [RelayCommand]
    private async Task MarkTaskDone(TaskItem? task)
    {
        if (task == null || task.IsFadingOut) // Prevent action if null or already animating out
        {
            Debug.WriteLine($"MarkTaskDone: Skipped - Task is null or already fading out (Task: {task?.Title}).");
            return;
        }

        Debug.WriteLine($"MarkTaskDone: Processing task '{task.Title}', TimeType: {task.TimeType}");

        // --- Logic for One-Time / Specific Date Tasks ---
        if (task.TimeType == TaskTimeType.None || task.TimeType == TaskTimeType.SpecificDate)
        {
            Debug.WriteLine($"MarkTaskDone: Task '{task.Title}' is one-time/specific. Fading out.");
            task.IsFadingOut = true; // Trigger fade-out animation via binding

            await Task.Delay(350); // Wait for animation (adjust duration as needed)

            // Ensure removal happens on the UI thread
            MainThread.BeginInvokeOnMainThread(() =>
            {
                bool removed = Tasks.Remove(task); // Remove from the collection
                if (removed)
                {
                    Debug.WriteLine($"MarkTaskDone: Removed task '{task.Title}'. Triggering save.");
                    _ = TriggerSave(); // Trigger save asynchronously
                }
                else
                {
                    // Should not happen if task was valid, but reset state if removal fails
                    Debug.WriteLine($"MarkTaskDone: Failed to remove task '{task.Title}' after fade.");
                    task.IsFadingOut = false;
                }
            });
        }
        // --- Logic for Repeating Tasks ---
        else if (task.TimeType == TaskTimeType.Repeating)
        {
            Debug.WriteLine($"MarkTaskDone: Task '{task.Title}' is repeating.");
            // Calculate the next due date using the utility function
            DateTime? nextDueDate = DateUtils.CalculateNextDueDate(task);

            if (nextDueDate.HasValue)
            {
                Debug.WriteLine($"MarkTaskDone: Next due date calculated: {nextDueDate.Value:d}");
                task.DueDate = nextDueDate; // Update the task's due date property

                // Move task to the end of the list on the UI thread
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    // Check if remove succeeds before adding back to prevent issues
                    if (Tasks.Remove(task))
                    {
                        Tasks.Add(task); // Add to the end
                        Debug.WriteLine($"MarkTaskDone: Moved task '{task.Title}' to end.");
                        _ = TriggerSave(); // Trigger save asynchronously
                    }
                    else
                    {
                        Debug.WriteLine($"MarkTaskDone: Failed to remove repeating task '{task.Title}' before moving.");
                        // If removal fails, we might need error handling or state reset
                    }
                });
            }
            else
            {
                // Log if next date calculation fails (e.g., invalid repetition type)
                Debug.WriteLine($"MarkTaskDone: Could not calculate next due date for '{task.Title}'. Not moving or saving.");
            }
        }
    }

    // --- Message Handlers ---

    /// <summary>
    /// Handles the message when the calendar setting is changed in SettingsViewModel.
    /// Triggers a reload of tasks to update date formatting via converters.
    /// </summary>
    private void HandleCalendarSettingChanged()
    {
        Debug.WriteLine("MainViewModel: Received CalendarSettingChangedMessage. Triggering LoadTasksCommand.");
        // Force reload of tasks to apply new date formatting
        if (LoadTasksCommand.CanExecute(null))
        {
            LoadTasksCommand.Execute(null);
        }
        else
        {
            Debug.WriteLine("MainViewModel: LoadTasksCommand cannot execute (possibly still running).");
            // Consider queuing or alternative refresh mechanism if needed
        }
    }

    /// <summary>
    /// Adds a new task received via message to the collection and triggers save.
    /// </summary>
    private async void HandleAddTask(TaskItem? newTask) // Allow nullable
    {
        if (newTask == null) { Debug.WriteLine("Received AddTaskMessage with null task."); return; }
        Debug.WriteLine($"Received AddTaskMessage for: {newTask.Title}, TimeType: {newTask.TimeType}");
        newTask.Order = Tasks.Count; // Assign order based on current count
        Tasks.Add(newTask); // Add to collection (triggers CollectionChanged -> Add)
        await TriggerSave(); // Save the new state
    }

    /// <summary>
    /// Replaces an existing task with updated data received via message and triggers save.
    /// Uses item replacement to ensure immediate UI update for converter-based bindings.
    /// </summary>
    private async void HandleUpdateTask(TaskItem? updatedTask) // Allow nullable
    {
        if (updatedTask == null) { Debug.WriteLine("Received UpdateTaskMessage with null task."); return; }

        // Find the index of the task to update
        int index = -1;
        for (int i = 0; i < Tasks.Count; i++) { if (Tasks[i].Id == updatedTask.Id) { index = i; break; } }

        if (index != -1)
        {
            Debug.WriteLine($"HandleUpdateTask: Found task '{updatedTask.Title}' at index {index}. Replacing item.");
            updatedTask.Order = index; // Ensure Order property matches its position
            Tasks[index] = updatedTask; // Replace item (triggers CollectionChanged -> Replace)
            await TriggerSave(); // Save the updated state
        }
        else { Debug.WriteLine($"HandleUpdateTask: Update failed: Task with ID {updatedTask.Id} not found."); }
    }

    /// <summary>
    /// Removes a task identified by ID received via message and triggers save.
    /// </summary>
    private async void HandleDeleteTask(Guid taskId)
    {
        var taskToRemove = Tasks.FirstOrDefault(t => t.Id == taskId);
        if (taskToRemove != null)
        {
            Debug.WriteLine($"Received DeleteTaskMessage for: {taskToRemove.Title} ({taskId})");
            Tasks.Remove(taskToRemove); // Remove from collection (triggers CollectionChanged -> Remove)
            await TriggerSave(); // Save the new state
        }
        else { Debug.WriteLine($"Delete failed: Task with ID {taskId} not found."); }
    }

    // --- Saving Logic ---

    /// <summary>
    /// Handles changes to the Tasks collection, specifically saving after Move operations.
    /// </summary>
    private async void Tasks_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        // Only explicitly trigger save on Move, as other actions (Add, Replace, Remove)
        // trigger saves via their respective message handlers.
        if (e.Action == NotifyCollectionChangedAction.Move)
        {
            Debug.WriteLine($"CollectionChanged: Action=Move. Triggering save.");
            await TriggerSave();
        }
        else { Debug.WriteLine($"CollectionChanged: Action={e.Action}"); }
    }

    /// <summary>
    /// Serializes the current task list (after updating order) and writes it to the JSON file.
    /// Manages the _isSaving flag internally.
    /// </summary>
    private async Task SaveTasks()
    {
        bool acquiredLock = false;
        try
        {
            // Acquire lock and set saving flag
            lock (_saveLock)
            {
                if (_isSaving) { Debug.WriteLine("SaveTasks: Save already in progress. Exiting."); return; }
                _isSaving = true;
                acquiredLock = true;
            }

            List<TaskItem> tasksToSave;
            // Lock collection only while reading/setting order
            lock (Tasks)
            {
                // Ensure Order property is correct based on current position
                for (int i = 0; i < Tasks.Count; i++) { if (Tasks[i].Order != i) { Tasks[i].Order = i; } }
                // Create a snapshot for saving
                tasksToSave = new List<TaskItem>(Tasks);
            }

            Debug.WriteLine($"SaveTasks: Attempting to save {tasksToSave.Count} tasks...");

            // Serialize the snapshot
            string json = JsonSerializer.Serialize(tasksToSave, new JsonSerializerOptions { WriteIndented = true });
            // Write to file
            await File.WriteAllTextAsync(_filePath, json);
            Debug.WriteLine($"SaveTasks: Tasks saved successfully to {_filePath}");

#if WINDOWS && DEBUG
            // Optional: Open containing folder logic
#endif
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SaveTasks: Error saving tasks: {ex.Message}");
            // Consider notifying user of save failure
        }
        finally
        {
            // Ensure save flag is reset *only if lock was acquired by this call*
            if (acquiredLock)
            {
                lock (_saveLock) { _isSaving = false; }
                Debug.WriteLine("SaveTasks finished, _isSaving reset.");
            }
        }
    }

    /// <summary>
    /// Initiates a debounced save operation if one isn't already running.
    /// </summary>
    private async Task TriggerSave()
    {
        // Check if a save is already in progress or pending without acquiring the main save lock yet
        lock (_saveLock) { if (_isSaving) { Debug.WriteLine("TriggerSave: Skipped, save already in progress/pending."); return; } }

        Debug.WriteLine("TriggerSave: Initiating save cycle...");
        await Task.Delay(300); // Debounce delay
        // SaveTasks handles setting/resetting _isSaving flag internally now
        await SaveTasks();
    }

} // End of MainViewModel class