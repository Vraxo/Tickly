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
using System.Text.Json; // Ensure this is present
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel; // Needed for MainThread
using Tickly.Messages;
using Tickly.Models;
using Tickly.Views;

namespace Tickly.ViewModels;

public partial class MainViewModel : ObservableObject
{
    // The main collection of tasks bound to the UI.
    [ObservableProperty]
    private ObservableCollection<TaskItem> _tasks;

    // Path to the JSON file used for storing tasks.
    private readonly string _filePath;
    // Flag to prevent concurrent save operations.
    private bool _isSaving = false;
    // Lock object for synchronizing access to the _isSaving flag.
    private readonly object _saveLock = new object();
    // Stores the date when the ViewModel was initialized or tasks were last loaded, used for resetting completion state.
    private DateTime _currentDate = DateTime.Today;
    // Flag to prevent concurrent execution of LoadTasks. (CommunityToolkit alternative: Use [RelayCommand(IncludeCancelCommand = true)])
    private bool _isLoadingTasks = false;


    public MainViewModel()
    {
        _filePath = Path.Combine(FileSystem.AppDataDirectory, "tasks.json");
        // Initialize the collection directly to ensure it's never null.
        _tasks = new ObservableCollection<TaskItem>();
        // Subscribe to collection changes (primarily for Move events).
        _tasks.CollectionChanged += Tasks_CollectionChanged;

        Debug.WriteLine("MainViewModel: Constructor - Initializing.");
        // Execute the LoadTasks command when the ViewModel is created.
        LoadTasksCommand.Execute(null);

        // Register message handlers for task operations and settings changes.
        WeakReferenceMessenger.Default.Register<AddTaskMessage>(this, (r, m) => HandleAddTask(m.Value));
        WeakReferenceMessenger.Default.Register<UpdateTaskMessage>(this, (r, m) => HandleUpdateTask(m.Value));
        WeakReferenceMessenger.Default.Register<DeleteTaskMessage>(this, (r, m) => HandleDeleteTask(m.Value));
        WeakReferenceMessenger.Default.Register<CalendarSettingChangedMessage>(this, (r, m) => HandleCalendarSettingChanged());
        Debug.WriteLine("MainViewModel: Constructor - Message handlers registered.");
    }

    /// <summary>
    /// Handles changes to the Tasks collection, specifically triggering saves on Move actions (drag/drop).
    /// </summary>
    private async void Tasks_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Move)
        {
            Debug.WriteLine($"CollectionChanged: Action={e.Action}, OldIndex={e.OldStartingIndex}, NewIndex={e.NewStartingIndex}");
            // Save needed after reordering.
            await TriggerSave();
        }
        else
        {
            // Log other collection changes (Add, Remove, Replace).
            Debug.WriteLine($"CollectionChanged: Action={e.Action}");
        }
    }

    /// <summary>
    /// Command to navigate modally to the AddTaskPopupPage for creating a new task.
    /// </summary>
    [RelayCommand]
    private async Task NavigateToAddPage()
    {
        try
        {
            await Shell.Current.GoToAsync(nameof(AddTaskPopupPage), true); // Use modal navigation
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error navigating to Add Page: {ex.Message}");
            // Optionally display an error to the user
        }
    }

    /// <summary>
    /// Command to navigate modally to the AddTaskPopupPage for editing an existing task.
    /// </summary>
    /// <param name="taskToEdit">The TaskItem object to edit.</param>
    [RelayCommand]
    private async Task NavigateToEditPage(TaskItem? taskToEdit) // Made parameter nullable
    {
        if (taskToEdit == null)
        {
            Debug.WriteLine("NavigateToEditPage: taskToEdit parameter was null.");
            return;
        }
        Debug.WriteLine($"Navigating to edit task: {taskToEdit.Title} ({taskToEdit.Id})");
        try
        {
            var navigationParameter = new Dictionary<string, object> { { "TaskToEdit", taskToEdit } };
            await Shell.Current.GoToAsync(nameof(AddTaskPopupPage), true, navigationParameter); // Use modal navigation with parameter
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error navigating to Edit Page: {ex.Message}");
            // Optionally display an error to the user
        }
    }

    /// <summary>
    /// Command to load tasks from the JSON file, process them (resetting completion state),
    /// and update the UI collection safely on the main thread. Includes detailed logging.
    /// </summary>
    [RelayCommand]
    private async Task LoadTasks()
    {
        // Prevent concurrent execution of LoadTasks
        if (_isLoadingTasks)
        {
            Debug.WriteLine("LoadTasks: Already running. Exiting.");
            return;
        }
        // Prevent execution if saving is in progress
        lock (_saveLock) { if (_isSaving) { Debug.WriteLine("LoadTasks skipped, save in progress."); return; } }

        _isLoadingTasks = true; // Mark as busy

        Debug.WriteLine("LoadTasks: Started.");
        _currentDate = DateTime.Today; // Update date check threshold

        List<TaskItem> loadedTasks = new List<TaskItem>(); // Holds tasks read from file
        bool needsSaveAfterLoad = false; // Tracks if modifications require saving

        try
        {
            // --- Step 1: Read File ---
            if (!File.Exists(_filePath))
            {
                Debug.WriteLine("LoadTasks: Task file not found. Ensuring UI collection is empty.");
                if (Tasks.Any()) { await MainThread.InvokeOnMainThreadAsync(() => Tasks.Clear()); }
                return; // Exit early
            }
            string json = await File.ReadAllTextAsync(_filePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.WriteLine("LoadTasks: Task file is empty. Ensuring UI collection is empty.");
                if (Tasks.Any()) { await MainThread.InvokeOnMainThreadAsync(() => Tasks.Clear()); }
                return; // Exit early
            }
            Debug.WriteLine($"LoadTasks: Read {json.Length} characters from file.");

            // --- Step 2: Deserialize ---
            try
            {
                loadedTasks = JsonSerializer.Deserialize<List<TaskItem>>(json); // Allow null temporarily
                if (loadedTasks == null)
                {
                    Debug.WriteLine("LoadTasks: JSON Deserialization resulted in NULL list.");
                    loadedTasks = new List<TaskItem>(); // Prevent null reference later
                }
                else
                {
                    Debug.WriteLine($"LoadTasks: Deserialized {loadedTasks.Count} tasks initially.");
                }
            }
            catch (JsonException jsonEx)
            {
                // Provide detailed error info if deserialization fails
                Debug.WriteLine($"LoadTasks: JSON DESERIALIZATION FAILED: {jsonEx.Message} (Path: {jsonEx.Path}, Line: {jsonEx.LineNumber}, Pos: {jsonEx.BytePositionInLine})");
                loadedTasks = new List<TaskItem>(); // Ensure list is empty on failure
                                                    // Consider clearing UI here too or letting the process continue to clear later
                if (Tasks.Any()) { await MainThread.InvokeOnMainThreadAsync(() => Tasks.Clear()); }
                return; // Exit if deserialization failed critically
            }


            // --- Step 3: Process Loaded Tasks (Reset IsCompleted for past repeating tasks) ---
            Debug.WriteLine($"LoadTasks: Processing {loadedTasks.Count} loaded tasks for completion reset...");
            foreach (var task in loadedTasks)
            {
                // Reset IsCompleted if it's a repeating task, marked complete, has a due date, and that date is before today
                if (task.TimeType == TaskTimeType.Repeating && task.IsCompleted && task.DueDate.HasValue && task.DueDate.Value.Date < _currentDate)
                {
                    Debug.WriteLine($"LoadTasks: Resetting IsCompleted for past repeating task '{task.Title}' (DueDate: {task.DueDate.Value:yyyy-MM-dd})");
                    task.IsCompleted = false;
                    needsSaveAfterLoad = true; // We modified data, so save needed later
                }
            }
            Debug.WriteLine($"LoadTasks: Finished processing tasks. NeedsSaveAfterLoad = {needsSaveAfterLoad}");


            // --- Step 4: Update UI Collection (Safely on Main Thread) ---
            var tasksToAdd = loadedTasks.OrderBy(t => t.Order).ToList(); // Ensure tasks are ordered correctly
            Debug.WriteLine($"LoadTasks: Prepared {tasksToAdd.Count} tasks to add to UI collection.");

            await MainThread.InvokeOnMainThreadAsync(() => // Ensure UI updates happen on the correct thread
            {
                Debug.WriteLine($"LoadTasks: Now on MainThread. Current Tasks count: {Tasks.Count}");
                Tasks.CollectionChanged -= Tasks_CollectionChanged; // Unsubscribe to prevent triggers during bulk update
                try
                {
                    Debug.WriteLine("LoadTasks: Clearing UI collection...");
                    Tasks.Clear(); // Clear existing items
                    Debug.WriteLine($"LoadTasks: UI collection cleared. Count: {Tasks.Count}. Adding {tasksToAdd.Count} items...");
                    foreach (var task in tasksToAdd)
                    {
                        Tasks.Add(task); // Add processed items
                    }
                    // Log the final count after adding items
                    Debug.WriteLine($"LoadTasks: Finished adding items to UI collection. FINAL COUNT: {Tasks.Count}.");
                    // Raising PropertyChanged for 'Tasks' might help some UI frameworks, but Clear/Add usually suffices for CollectionView
                    // OnPropertyChanged(nameof(Tasks));
                }
                catch (Exception uiEx)
                {
                    Debug.WriteLine($"LoadTasks: CRITICAL ERROR updating UI collection: {uiEx.Message}");
                    // Attempt to leave collection empty if UI update fails
                    Tasks.Clear();
                }
                finally
                {
                    Tasks.CollectionChanged += Tasks_CollectionChanged; // Always re-subscribe
                    Debug.WriteLine("LoadTasks: Re-subscribed to CollectionChanged.");
                }
            });


            // --- Step 5: Save if Resets Occurred ---
            if (needsSaveAfterLoad)
            {
                Debug.WriteLine("LoadTasks: Triggering save because completion states were reset.");
                // Run save in background to avoid blocking LoadTasks completion
                _ = Task.Run(async () => await TriggerSave());
            }

        }
        catch (Exception ex) // Catch any other unexpected errors (e.g., file access issues)
        {
            Debug.WriteLine($"LoadTasks: UNEXPECTED ERROR during load/process: {ex.GetType().Name} - {ex.Message}\n{ex.StackTrace}");
            // Attempt to clear UI collection on error
            if (MainThread.IsMainThread) { Tasks.Clear(); }
            else { MainThread.BeginInvokeOnMainThread(() => Tasks.Clear()); }
        }
        finally
        {
            _isLoadingTasks = false; // Mark as not busy
            Debug.WriteLine("LoadTasks finished execution.");
        }
    }


    /// <summary>
    /// Handles receiving a new TaskItem to add to the collection.
    /// </summary>
    private async void HandleAddTask(TaskItem? newTask) // Allow null check
    {
        if (newTask == null) { Debug.WriteLine("Received AddTaskMessage with null task."); return; }
        Debug.WriteLine($"Received AddTaskMessage for: {newTask.Title}, TimeType: {newTask.TimeType}");
        // Assign order based on current count before adding
        newTask.Order = Tasks.Count;
        // Add to UI collection (ensure this happens on UI thread if needed, though messages usually arrive there)
        if (MainThread.IsMainThread) { Tasks.Add(newTask); } else { MainThread.BeginInvokeOnMainThread(() => Tasks.Add(newTask)); }
        await TriggerSave(); // Save the new state
    }

    /// <summary>
    /// Handles receiving an updated TaskItem, replacing the existing one in the collection.
    /// </summary>
    private async void HandleUpdateTask(TaskItem? updatedTask) // Allow null check
    {
        if (updatedTask == null) { Debug.WriteLine("Received UpdateTaskMessage with null task."); return; }

        // Find index on UI thread if modifications happen there
        int index = -1;
        if (MainThread.IsMainThread)
        {
            for (int i = 0; i < Tasks.Count; i++) { if (Tasks[i].Id == updatedTask.Id) { index = i; break; } }
        }
        else
        {
            await MainThread.InvokeOnMainThreadAsync(() => {
                for (int i = 0; i < Tasks.Count; i++) { if (Tasks[i].Id == updatedTask.Id) { index = i; break; } }
            });
        }


        if (index != -1)
        {
            Debug.WriteLine($"HandleUpdateTask: Found task '{updatedTask.Title}' at index {index}. Replacing item.");
            updatedTask.Order = index; // Ensure order property matches its position

            // Perform replacement on UI thread
            if (MainThread.IsMainThread) { Tasks[index] = updatedTask; } else { await MainThread.InvokeOnMainThreadAsync(() => Tasks[index] = updatedTask); }

            await TriggerSave(); // Save the change
        }
        else { Debug.WriteLine($"HandleUpdateTask: Update failed: Task with ID {updatedTask.Id} not found."); }
    }

    /// <summary>
    /// Handles receiving the ID of a task to delete.
    /// </summary>
    private async void HandleDeleteTask(Guid taskId)
    {
        // Find task (safer to do on UI thread if collection is modified there)
        TaskItem? taskToRemove = null;
        if (MainThread.IsMainThread) { taskToRemove = Tasks.FirstOrDefault(t => t.Id == taskId); }
        else { await MainThread.InvokeOnMainThreadAsync(() => taskToRemove = Tasks.FirstOrDefault(t => t.Id == taskId)); }


        if (taskToRemove != null)
        {
            Debug.WriteLine($"Received DeleteTaskMessage for: {taskToRemove.Title} ({taskId})");
            // Perform removal on UI thread
            if (MainThread.IsMainThread) { Tasks.Remove(taskToRemove); } else { await MainThread.InvokeOnMainThreadAsync(() => Tasks.Remove(taskToRemove)); }

            await TriggerSave(); // Save the change
        }
        else { Debug.WriteLine($"Delete failed: Task with ID {taskId} not found."); }
    }

    /// <summary>
    /// Handles notification that calendar settings changed, triggering a reload of tasks.
    /// </summary>
    private void HandleCalendarSettingChanged()
    {
        Debug.WriteLine("MainViewModel: Received CalendarSettingChangedMessage. Triggering LoadTasksCommand.");
        // Force reload to update all converter outputs
        if (LoadTasksCommand.CanExecute(null))
        {
            // Execute might block if called from UI thread and command is long running
            // Consider Task.Run if LoadTasks needs significant background work
            LoadTasksCommand.Execute(null);
        }
        else
        {
            Debug.WriteLine("MainViewModel: LoadTasksCommand cannot execute (possibly still running or condition not met).");
        }
    }

    /// <summary>
    /// Command logic for marking a task as complete. Handles repeating vs non-repeating.
    /// </summary>
    [RelayCommand]
    private async Task MarkTaskComplete(TaskItem? task)
    {
        if (task == null) return;

        Debug.WriteLine($"MarkTaskComplete executing for: '{task.Title}', TimeType: {task.TimeType}, Current IsCompleted: {task.IsCompleted}");

        if (task.TimeType == TaskTimeType.Repeating && task.DueDate.HasValue)
        {
            // --- Repeating Task Logic ---
            if (!task.IsCompleted) // Only act if not already completed in this cycle
            {
                DateTime nextDueDate = CalculateNextDueDate(task);
                Debug.WriteLine($"Repeating task '{task.Title}' marked complete. Next due date: {nextDueDate:yyyy-MM-dd}");

                task.DueDate = nextDueDate; // Update to next occurrence date
                task.IsCompleted = true;    // Mark as visually completed for now

                // Move to bottom by removing and re-adding (ensures UI update)
                if (MainThread.IsMainThread) { Tasks.Remove(task); Tasks.Add(task); }
                else { await MainThread.InvokeOnMainThreadAsync(() => { Tasks.Remove(task); Tasks.Add(task); }); }


                await TriggerSave(); // Save changes
            }
            else { Debug.WriteLine($"Repeating task '{task.Title}' is already marked complete for this cycle."); }
        }
        else
        {
            // --- Non-Repeating Task Logic ---
            Debug.WriteLine($"Non-repeating task '{task.Title}' marked complete. Removing.");
            if (MainThread.IsMainThread) { Tasks.Remove(task); } else { await MainThread.InvokeOnMainThreadAsync(() => Tasks.Remove(task)); }
            await TriggerSave(); // Save the removal
        }
    }

    /// <summary>
    /// Calculates the next due date for a repeating task based on its current due date and repetition rules.
    /// </summary>
    private DateTime CalculateNextDueDate(TaskItem task)
    {
        // Use today + 1 day as fallback if DueDate is somehow null
        if (!task.DueDate.HasValue) { return DateTime.Today.AddDays(1); }

        DateTime currentDueDate = task.DueDate.Value.Date; // Use only the Date part for reliable calculations

        switch (task.RepetitionType)
        {
            case TaskRepetitionType.Daily:
                return currentDueDate.AddDays(1);
            case TaskRepetitionType.AlternateDay:
                return currentDueDate.AddDays(2);
            case TaskRepetitionType.Weekly:
                if (task.RepetitionDayOfWeek.HasValue)
                {
                    DateTime nextDate = currentDueDate;
                    // Calculate days until the next target DayOfWeek
                    int daysToAdd = ((int)task.RepetitionDayOfWeek.Value - (int)nextDate.DayOfWeek + 7) % 7;
                    // If today IS the target day, ensure we move to the *next* week's occurrence
                    if (daysToAdd == 0) daysToAdd = 7;
                    return nextDate.AddDays(daysToAdd);
                }
                else { return currentDueDate.AddDays(7); } // Fallback for Weekly if day is missing
            default:
                return currentDueDate.AddDays(1); // Default fallback (e.g., if RepetitionType is null)
        }
    }


    /// <summary>
    /// Saves the current list of tasks (including order) to the JSON file.
    /// Handles locking to prevent concurrent saves.
    /// </summary>
    private async Task SaveTasks()
    {
        bool acquiredLock = false;
        try
        {
            // Attempt to acquire the save lock
            lock (_saveLock)
            {
                if (_isSaving) { Debug.WriteLine("SaveTasks: Save already in progress. Exiting."); return; }
                _isSaving = true; // Mark as saving
                acquiredLock = true; // Indicate lock was acquired
            }

            List<TaskItem> tasksToSave;
            // Lock the collection only while reading it and setting order
            lock (Tasks)
            {
                // Ensure Order property is up-to-date based on current list position
                for (int i = 0; i < Tasks.Count; i++) { if (Tasks[i].Order != i) { Tasks[i].Order = i; } }
                // Create a snapshot for serialization
                tasksToSave = new List<TaskItem>(Tasks);
            }

            Debug.WriteLine($"SaveTasks: Attempting to save {tasksToSave.Count} tasks...");

            // Serialize the snapshot
            string json = JsonSerializer.Serialize(tasksToSave, new JsonSerializerOptions { WriteIndented = true });
            // Write to file asynchronously
            await File.WriteAllTextAsync(_filePath, json);
            Debug.WriteLine($"SaveTasks: Tasks saved successfully to {_filePath}");

#if WINDOWS && DEBUG // Optional: Open folder logic
            // string directory = Path.GetDirectoryName(_filePath); if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory)) { try { Process.Start(new ProcessStartInfo() { FileName = directory, UseShellExecute = true, Verb = "open" }); } catch (Exception ex) { Debug.WriteLine($"Failed to open directory: {ex.Message}"); } } else { Debug.WriteLine($"Directory not found or invalid: {directory}"); }
#endif
        }
        catch (IOException ioEx) { Debug.WriteLine($"SaveTasks: IO Error saving tasks: {ioEx.Message}"); }
        catch (UnauthorizedAccessException authEx) { Debug.WriteLine($"SaveTasks: Access Error saving tasks: {authEx.Message}"); }
        catch (Exception ex) { Debug.WriteLine($"SaveTasks: Unexpected Error saving tasks: {ex.Message}"); }
        finally
        {
            // Release the save lock *only if it was acquired* by this execution
            if (acquiredLock)
            {
                lock (_saveLock) { _isSaving = false; }
                Debug.WriteLine("SaveTasks finished, _isSaving reset.");
            }
        }
    }

    /// <summary>
    /// Helper method to debounce save requests and ensure only one save runs at a time.
    /// </summary>
    private async Task TriggerSave()
    {
        // Check if a save is already running or pending; if so, do nothing.
        lock (_saveLock) { if (_isSaving) { Debug.WriteLine("TriggerSave: Skipped, save already in progress/pending."); return; } }

        // If no save is running, initiate one after a short delay (debounce)
        Debug.WriteLine("TriggerSave: Initiating save cycle...");
        await Task.Delay(300); // Wait briefly to coalesce multiple rapid triggers
        await SaveTasks(); // Call the actual save method (it handles the _isSaving flag)
    }

} // End of MainViewModel class