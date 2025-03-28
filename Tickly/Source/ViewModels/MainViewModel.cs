// File: ViewModels\MainViewModel.cs
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
using Microsoft.Maui.Controls; // Needed for Shell
using Tickly.Messages;
using Tickly.Models;
using Tickly.Views;
using Tickly.Utils; // Needed for DateUtils

namespace Tickly.ViewModels;

// Ensure the class is partial for source generators to work
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
        // Subscribe AFTER initial load to avoid unnecessary saves during load
        // _tasks.CollectionChanged += Tasks_CollectionChanged; // Moved to end of LoadTasksAsync

        // Load initial data
        _ = LoadTasksAsync(); // Use async void pattern cautiously; Task is returned but not awaited here

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
        try
        {
            // Ensure IsEditMode is false in the target ViewModel if navigating for Add
            await Shell.Current.GoToAsync(nameof(AddTaskPopupPage), true,
                new Dictionary<string, object> { { "TaskToEdit", null } }); // Explicitly pass null
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error navigating to add page: {ex.Message}");
            // Optionally show user error
        }
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
        try
        {
            Debug.WriteLine($"Navigating to edit task: {taskToEdit.Title} ({taskToEdit.Id})");
            var navigationParameter = new Dictionary<string, object> { { "TaskToEdit", taskToEdit } };
            await Shell.Current.GoToAsync(nameof(AddTaskPopupPage), true, navigationParameter);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error navigating to edit page for task {taskToEdit.Id}: {ex.Message}");
            // Optionally show user error
        }
    }

    /// <summary>
    /// Loads tasks from the JSON file into the ObservableCollection.
    /// Renamed to LoadTasksAsync and returns Task.
    /// </summary>
    [RelayCommand] // Generates LoadTasksAsyncCommand for potential external use
    private async Task LoadTasksAsync()
    {
        // Prevent loading during save
        lock (_saveLock) { if (_isSaving) { Debug.WriteLine("LoadTasksAsync skipped, save in progress."); return; } }

        Debug.WriteLine($"LoadTasksAsync: Attempting to load tasks from: {_filePath}");

        bool wasSubscribed = false;
        // Check if already subscribed before unsubscribing
        try
        {
            // Temporarily unsubscribe to prevent CollectionChanged firing during load
            Tasks.CollectionChanged -= Tasks_CollectionChanged;
            wasSubscribed = true; // Assume it was, handle potential exception if not
            Debug.WriteLine("LoadTasksAsync: Unsubscribed from CollectionChanged.");
        }
        catch
        {
            Debug.WriteLine("LoadTasksAsync: Was not subscribed to CollectionChanged.");
            /* Ignore if not subscribed */
        }


        try
        {
            List<TaskItem> tasksToAdd = new List<TaskItem>();
            if (File.Exists(_filePath))
            {
                string json = await File.ReadAllTextAsync(_filePath);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    try
                    {
                        var loadedTasks = JsonSerializer.Deserialize<List<TaskItem>>(json);
                        // Ensure OrderBy happens even if loadedTasks is null/empty
                        tasksToAdd = loadedTasks?.OrderBy(t => t.Order).ToList() ?? new List<TaskItem>();
                    }
                    catch (JsonException jsonEx)
                    {
                        Debug.WriteLine($"LoadTasksAsync: Error deserializing tasks JSON: {jsonEx.Message}");
                        // Prevent adding corrupted data, keep tasksToAdd empty
                    }
                }
                else
                {
                    Debug.WriteLine("LoadTasksAsync: Task file is empty.");
                }
            }
            else
            {
                Debug.WriteLine("LoadTasksAsync: Task file not found.");
            }

            // Update the UI collection on the main thread
            await MainThread.InvokeOnMainThreadAsync(() => // Use awaitable version
            {
                Tasks.Clear(); // Clear existing items
                foreach (var task in tasksToAdd)
                {
                    task.IsFadingOut = false; // Reset animation state on load
                    Tasks.Add(task);
                }
                Debug.WriteLine($"LoadTasksAsync: Cleared and added {Tasks.Count} tasks to collection.");
                // Update Order property based on loaded order (or initial add)
                UpdateTaskOrderProperty(); // Needs to run on MainThread after collection update
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"LoadTasksAsync: Error loading tasks: {ex.GetType().Name} - {ex.Message}");
            // Optionally clear tasks on error, ensuring it's on MainThread
            if (Tasks.Any())
            {
                await MainThread.InvokeOnMainThreadAsync(Tasks.Clear);
            }
        }
        finally
        {
            // Always re-subscribe if it was originally subscribed or if tasks now exist
            if (wasSubscribed || Tasks.Any())
            {
                // Ensure not doubly subscribed
                Tasks.CollectionChanged -= Tasks_CollectionChanged;
                Tasks.CollectionChanged += Tasks_CollectionChanged;
                Debug.WriteLine("LoadTasksAsync: Re-subscribed to CollectionChanged.");
            }
            else
            {
                Debug.WriteLine("LoadTasksAsync: No tasks loaded and was not subscribed, staying unsubscribed.");
            }
            Debug.WriteLine("LoadTasksAsync finished.");
        }
    }

    /// <summary>
    /// Handles the action when a task's check circle is tapped.
    /// Removes one-time tasks or updates repeating tasks.
    /// </summary>
    [RelayCommand]
    private async Task MarkTaskDone(TaskItem? task)
    {
        if (task == null || task.IsFadingOut)
        {
            Debug.WriteLine($"MarkTaskDone: Skipped - Task is null or already fading out (Task: {task?.Title}).");
            return;
        }

        Debug.WriteLine($"MarkTaskDone: Processing task '{task.Title}', TimeType: {task.TimeType}");

        if (task.TimeType == TaskTimeType.None || task.TimeType == TaskTimeType.SpecificDate)
        {
            Debug.WriteLine($"MarkTaskDone: Task '{task.Title}' is one-time/specific. Fading out.");
            task.IsFadingOut = true;

            await Task.Delay(350);

            // Use awaitable version for MainThread
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                bool removed = Tasks.Remove(task);
                if (removed)
                {
                    Debug.WriteLine($"MarkTaskDone: Removed task '{task.Title}'. Updating order and triggering save.");
                    UpdateTaskOrderProperty(); // Update order after removal (runs on MainThread)
                    await TriggerSave(); // Trigger save now requires await
                }
                else
                {
                    Debug.WriteLine($"MarkTaskDone: Failed to remove task '{task.Title}' after fade.");
                    task.IsFadingOut = false; // Reset state if removal failed
                }
            });
        }
        else if (task.TimeType == TaskTimeType.Repeating)
        {
            Debug.WriteLine($"MarkTaskDone: Task '{task.Title}' is repeating.");
            DateTime? nextDueDate = DateUtils.CalculateNextDueDate(task);

            if (nextDueDate.HasValue)
            {
                Debug.WriteLine($"MarkTaskDone: Next due date calculated: {nextDueDate.Value:d}");
                // Update DueDate directly - ObservableObject handles notification
                task.DueDate = nextDueDate;

                // Use awaitable version for MainThread
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    if (Tasks.Remove(task)) // Move to end visually
                    {
                        Tasks.Add(task);
                        Debug.WriteLine($"MarkTaskDone: Moved repeating task '{task.Title}' to end.");
                        UpdateTaskOrderProperty(); // Update order after move (runs on MainThread)
                        await TriggerSave(); // Trigger save now requires await
                    }
                    else
                    {
                        Debug.WriteLine($"MarkTaskDone: Failed to remove repeating task '{task.Title}' before moving.");
                    }
                });
            }
            else
            {
                Debug.WriteLine($"MarkTaskDone: Could not calculate next due date for '{task.Title}'. Not moving or saving.");
            }
        }
    }


    // *** NEW SORT COMMAND ***
    /// <summary>
    /// Sorts tasks by Priority (High->Low) then alphabetically by Title.
    /// </summary>
    [RelayCommand]
    private async Task SortTasks()
    {
        Debug.WriteLine("SortTasks: Sorting tasks by Priority then Title...");

        // Get current tasks and sort them in a new list
        // Ensure access to Tasks is on the main thread if needed, though reading might be safe
        List<TaskItem> currentTasks = new List<TaskItem>();
        await MainThread.InvokeOnMainThreadAsync(() => {
            currentTasks = new List<TaskItem>(Tasks);
        });

        List<TaskItem> sortedTasks = currentTasks
            .OrderBy(t => t.Priority) // Enum order High=0, Medium=1, Low=2 works correctly
            .ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase) // Case-insensitive title sort
            .ToList();

        // Unsubscribe to prevent saves during batch update
        Tasks.CollectionChanged -= Tasks_CollectionChanged;
        Debug.WriteLine("SortTasks: Unsubscribed from CollectionChanged.");

        try
        {
            // Update the collection on the main thread
            await MainThread.InvokeOnMainThreadAsync(() => // Use awaitable version
            {
                Tasks.Clear();
                foreach (var task in sortedTasks)
                {
                    Tasks.Add(task);
                }
                Debug.WriteLine($"SortTasks: Collection updated with {Tasks.Count} sorted tasks.");
                UpdateTaskOrderProperty(); // Ensure Order property reflects the new sort (runs on MainThread)
            });

            // Save the new sorted order
            await TriggerSave(); // TriggerSave already updates order again, but it's okay
            Debug.WriteLine("SortTasks: Save triggered.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SortTasks: Error during sorting or saving: {ex.Message}");
            // Handle error appropriately (e.g., display message to user)
        }
        finally
        {
            // Always re-subscribe if tasks exist
            if (Tasks.Any())
            {
                Tasks.CollectionChanged -= Tasks_CollectionChanged; // Prevent double subscription
                Tasks.CollectionChanged += Tasks_CollectionChanged;
                Debug.WriteLine("SortTasks: Re-subscribed to CollectionChanged.");
            }
            else
            {
                Debug.WriteLine("SortTasks: No tasks after sort, staying unsubscribed.");
            }
        }
    }
    // *** END NEW SORT COMMAND ***


    // --- Message Handlers ---

    // *** WORKAROUND: Call LoadTasksAsync() directly instead of LoadTasksAsyncCommand ***
    private async void HandleCalendarSettingChanged() // Use async void for handlers awaiting async work
    {
        Debug.WriteLine("MainViewModel: Received CalendarSettingChangedMessage. Triggering LoadTasksAsync directly.");
        // Reload tasks to update date formatting

        try
        {
            // Directly await the private async method instead of the generated command property
            await LoadTasksAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error executing LoadTasksAsync directly: {ex.Message}");
            // Handle execution error if necessary
        }
    }

    private async void HandleAddTask(TaskItem? newTask)
    {
        if (newTask == null) { Debug.WriteLine("Received AddTaskMessage with null task."); return; }
        Debug.WriteLine($"Received AddTaskMessage for: {newTask.Title}");

        // Add on UI thread using awaitable version
        await MainThread.InvokeOnMainThreadAsync(async () => // Make lambda async
        {
            // Assign order based on current count BEFORE adding
            newTask.Order = Tasks.Count;
            Tasks.Add(newTask);
            Debug.WriteLine($"HandleAddTask: Added '{newTask.Title}', new count: {Tasks.Count}.");
            // UpdateTaskOrderProperty(); // Not needed here, order assigned correctly
            await TriggerSave(); // Await TriggerSave within the MainThread lambda
        });
    }

    private async void HandleUpdateTask(TaskItem? updatedTask)
    {
        if (updatedTask == null) { Debug.WriteLine("Received UpdateTaskMessage with null task."); return; }
        Debug.WriteLine($"Received UpdateTaskMessage for: {updatedTask.Title} ({updatedTask.Id})");

        // Find and replace on UI thread using awaitable version
        await MainThread.InvokeOnMainThreadAsync(async () => // Make lambda async
        {
            int index = -1;
            for (int i = 0; i < Tasks.Count; i++) { if (Tasks[i].Id == updatedTask.Id) { index = i; break; } }

            if (index != -1)
            {
                // Ensure Order property matches its position before replacing
                updatedTask.Order = index;
                Tasks[index] = updatedTask; // Replace item (triggers CollectionChanged -> Replace)
                Debug.WriteLine($"HandleUpdateTask: Replaced task at index {index}.");
                // UpdateTaskOrderProperty(); // Not needed here, order preserved/updated
                await TriggerSave(); // Await TriggerSave AFTER replacement within MainThread lambda
            }
            else
            {
                Debug.WriteLine($"HandleUpdateTask: Update failed: Task with ID {updatedTask.Id} not found.");
            }
        });
    }


    private async void HandleDeleteTask(Guid taskId)
    {
        Debug.WriteLine($"Received DeleteTaskMessage for Task ID: {taskId}");
        // Find and remove on UI thread using awaitable version
        await MainThread.InvokeOnMainThreadAsync(async () => // Make lambda async
        {
            var taskToRemove = Tasks.FirstOrDefault(t => t.Id == taskId);
            if (taskToRemove != null)
            {
                if (Tasks.Remove(taskToRemove))
                {
                    Debug.WriteLine($"HandleDeleteTask: Removed '{taskToRemove.Title}'. Updating order.");
                    UpdateTaskOrderProperty(); // Update order after removal (runs on MainThread)
                    await TriggerSave(); // Await TriggerSave AFTER removal and order update within MainThread lambda
                }
                else
                {
                    Debug.WriteLine($"HandleDeleteTask: Failed to remove task '{taskToRemove.Title}' from collection.");
                }
            }
            else
            {
                Debug.WriteLine($"Delete failed: Task with ID {taskId} not found.");
            }
        });
    }

    // --- Saving Logic ---

    private async void Tasks_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // This handler now primarily deals with 'Move' actions explicitly
        // Other actions (Add, Remove, Replace) trigger saves from their respective handlers
        // Sort triggers its own save
        // Reset doesn't require an explicit save unless desired

        if (e.Action == NotifyCollectionChangedAction.Move)
        {
            Debug.WriteLine($"CollectionChanged: Action=Move detected. Updating order and triggering save.");
            // Ensure order update happens on MainThread, then trigger save
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                UpdateTaskOrderProperty();
                await TriggerSave();
            });
        }
        else
        {
            // Log other actions, but saves are handled elsewhere
            Debug.WriteLine($"CollectionChanged: Action={e.Action}. Save handled by initiating action.");
        }
    }

    /// <summary>
    /// Updates the Order property of each TaskItem based on its current index in the Tasks collection.
    /// MUST be called on the Main thread as it accesses the Tasks collection directly.
    /// </summary>
    private void UpdateTaskOrderProperty()
    {
        // This method is now always called from within a MainThread context (InvokeOnMainThreadAsync/BeginInvokeOnMainThread)
        // or dispatches itself if called incorrectly.
        if (!MainThread.IsMainThread)
        {
            Debug.WriteLine("WARNING: UpdateTaskOrderProperty called from non-UI thread. Dispatching.");
            MainThread.BeginInvokeOnMainThread(() => UpdateTaskOrderPropertyInternal());
        }
        else
        {
            UpdateTaskOrderPropertyInternal();
        }
    }

    private void UpdateTaskOrderPropertyInternal()
    {
        // Actual implementation assumes it's on the Main thread
        for (int i = 0; i < Tasks.Count; i++)
        {
            // Check if the task exists at the index, safety for potential race conditions (though unlikely here)
            if (i < Tasks.Count && Tasks[i] != null && Tasks[i].Order != i)
            {
                Tasks[i].Order = i;
                // No need to manually raise PropertyChanged for Order unless something external binds to it directly
            }
        }
        Debug.WriteLine($"UpdateTaskOrderPropertyInternal: Order property updated for {Tasks.Count} tasks.");
    }


    /// <summary>
    /// Serializes the current task list and writes it to the JSON file.
    /// Assumes Order property is up-to-date (caller responsibility).
    /// Manages the _isSaving flag internally. Should be awaited.
    /// </summary>
    private async Task SaveTasks()
    {
        bool acquiredLock = false;
        // Initialize tasksToSave
        List<TaskItem> tasksToSave = new List<TaskItem>();

        try
        {
            // Acquire lock and set saving flag
            lock (_saveLock)
            {
                if (_isSaving) { Debug.WriteLine("SaveTasks: Save already in progress. Exiting."); return; }
                _isSaving = true;
                acquiredLock = true;
                Debug.WriteLine("SaveTasks: Lock acquired, _isSaving set to true.");
            }

            // Read task list snapshot quickly, assuming order is correct
            // Needs to be on MainThread to safely access Tasks collection
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                // Assign the snapshot to the initialized list
                tasksToSave = new List<TaskItem>(Tasks);
            });


            Debug.WriteLine($"SaveTasks: Attempting to save {tasksToSave.Count} tasks...");

            // Check if tasksToSave has actually been populated (safety)
            if (tasksToSave != null)
            {
                string json = JsonSerializer.Serialize(tasksToSave, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_filePath, json);
                Debug.WriteLine($"SaveTasks: Tasks saved successfully to {_filePath}");
            }
            else
            {
                Debug.WriteLine($"SaveTasks: tasksToSave list was unexpectedly null. Save aborted.");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SaveTasks: Error saving tasks: {ex.Message}");
        }
        finally
        {
            if (acquiredLock)
            {
                lock (_saveLock)
                {
                    _isSaving = false;
                    Debug.WriteLine("SaveTasks: Lock released, _isSaving set to false.");
                }
            }
        }
    }

    /// <summary>
    /// Initiates a debounced save operation if one isn't already running.
    /// Ensures the Order property is updated before saving. Should be awaited.
    /// </summary>
    private async Task TriggerSave()
    {
        // Update Order properties on the MainThread before checking the save lock
        // Use BeginInvoke to avoid potential deadlocks if TriggerSave is called from MainThread handler
        MainThread.BeginInvokeOnMainThread(UpdateTaskOrderProperty);

        // Check if a save is already in progress or pending
        lock (_saveLock) { if (_isSaving) { Debug.WriteLine("TriggerSave: Skipped, save already in progress/pending."); return; } }

        Debug.WriteLine("TriggerSave: Initiating save cycle...");
        try
        {
            await Task.Delay(300); // Debounce delay
            await SaveTasks(); // Await the actual save operation
            Debug.WriteLine("TriggerSave: Save cycle complete.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TriggerSave: Error during debounce or SaveTasks call: {ex.Message}");
        }
    }

} // End of MainViewModel class