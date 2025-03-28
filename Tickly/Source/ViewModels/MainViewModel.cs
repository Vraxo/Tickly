// ViewModels/MainViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging; // Ensure this is present
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
using Tickly.Messages; // Ensure this is present
using Tickly.Models;
using Tickly.Views; // Required for nameof(AddTaskPopupPage)

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

        // Load tasks when ViewModel is created
        LoadTasksCommand.Execute(null);

        // Register message listeners
        WeakReferenceMessenger.Default.Register<AddTaskMessage>(this, (r, m) => HandleAddTask(m.Value));
        WeakReferenceMessenger.Default.Register<UpdateTaskMessage>(this, (r, m) => HandleUpdateTask(m.Value));
        WeakReferenceMessenger.Default.Register<DeleteTaskMessage>(this, (r, m) => HandleDeleteTask(m.Value));
        // Register handler for calendar setting changes
        WeakReferenceMessenger.Default.Register<CalendarSettingChangedMessage>(this, (r, m) => HandleCalendarSettingChanged());
    }

    // Handles CollectionChanged events (specifically Move for drag/drop saving)
    private async void Tasks_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Move)
        {
            Debug.WriteLine($"CollectionChanged: Action={e.Action}, OldIndex={e.OldStartingIndex}, NewIndex={e.NewStartingIndex}");
            await TriggerSave();
        }
        else { Debug.WriteLine($"CollectionChanged: Action={e.Action}"); }
    }

    // Command to navigate to the Add Task page
    [RelayCommand]
    private async Task NavigateToAddPage()
    {
        await Shell.Current.GoToAsync(nameof(AddTaskPopupPage), true);
    }

    // Command to navigate to the Edit Task page
    [RelayCommand]
    private async Task NavigateToEditPage(TaskItem taskToEdit)
    {
        if (taskToEdit == null) { Debug.WriteLine("NavigateToEditPage: taskToEdit is null."); return; }
        Debug.WriteLine($"Navigating to edit task: {taskToEdit.Title} ({taskToEdit.Id})");
        var navigationParameter = new Dictionary<string, object> { { "TaskToEdit", taskToEdit } };
        await Shell.Current.GoToAsync(nameof(AddTaskPopupPage), true, navigationParameter);
    }

    // Command to load tasks from the JSON file
    [RelayCommand]
    private async Task LoadTasks()
    {
        // Ensure LoadTasks doesn't run concurrently with itself or SaveTasks
        // (The RelayCommand's CanExecute helps, but explicit lock adds safety)
        lock (_saveLock) { if (_isSaving) { Debug.WriteLine("LoadTasks skipped, save in progress."); return; } }

        Debug.WriteLine($"LoadTasks: Attempting to load tasks from: {_filePath}");

        // Temporarily detach event handler to prevent triggers during bulk clear/add
        Tasks.CollectionChanged -= Tasks_CollectionChanged;
        try
        {
            if (!File.Exists(_filePath))
            {
                Debug.WriteLine("LoadTasks: Task file not found. Clearing tasks.");
                if (Tasks.Any()) Tasks.Clear(); // Clear if collection has items
                return;
            }

            string json = await File.ReadAllTextAsync(_filePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.WriteLine("LoadTasks: Task file is empty. Clearing tasks.");
                if (Tasks.Any()) Tasks.Clear(); // Clear if collection has items
                return;
            }

            var loadedTasks = JsonSerializer.Deserialize<List<TaskItem>>(json);

            // Efficiently clear and repopulate using a temporary list
            var tasksToAdd = loadedTasks?.OrderBy(t => t.Order).ToList() ?? new List<TaskItem>();

            // Ensure UI updates happen on the main thread if LoadTasks is called from background
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Tasks.Clear(); // Clear existing items
                foreach (var task in tasksToAdd)
                {
                    Debug.WriteLine($"LoadTasks: Loading Task='{task.Title}', TimeType='{task.TimeType}'");
                    Tasks.Add(task);
                }
                Debug.WriteLine($"LoadTasks: Successfully loaded and added {Tasks.Count} tasks to collection.");
                // Notify that the entire collection property has effectively changed
                // Even though we cleared/added, this signals bindings dependent on 'Tasks' itself
                OnPropertyChanged(nameof(Tasks));
            });

        }
        catch (JsonException jsonEx)
        {
            Debug.WriteLine($"LoadTasks: Error deserializing tasks JSON: {jsonEx.Message}");
            MainThread.BeginInvokeOnMainThread(() => Tasks.Clear()); // Clear on error (UI thread)
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"LoadTasks: Error loading tasks: {ex.GetType().Name} - {ex.Message}");
            MainThread.BeginInvokeOnMainThread(() => Tasks.Clear()); // Clear on error (UI thread)
        }
        finally
        {
            // Always re-attach the event handler
            Tasks.CollectionChanged += Tasks_CollectionChanged;
            Debug.WriteLine("LoadTasks finished.");
        }
    }


    // Handler for AddTaskMessage
    private async void HandleAddTask(TaskItem newTask)
    {
        if (newTask == null) { Debug.WriteLine("Received AddTaskMessage with null task."); return; }
        Debug.WriteLine($"Received AddTaskMessage for: {newTask.Title}, TimeType: {newTask.TimeType}");
        newTask.Order = Tasks.Count;
        Tasks.Add(newTask);
        await TriggerSave();
    }

    // Handler for UpdateTaskMessage (Uses Item Replacement)
    private async void HandleUpdateTask(TaskItem updatedTask)
    {
        if (updatedTask == null) { Debug.WriteLine("Received UpdateTaskMessage with null task."); return; }

        int index = -1;
        for (int i = 0; i < Tasks.Count; i++) { if (Tasks[i].Id == updatedTask.Id) { index = i; break; } }

        if (index != -1)
        {
            Debug.WriteLine($"HandleUpdateTask: Found task '{updatedTask.Title}' at index {index}. Replacing item.");
            updatedTask.Order = index; // Ensure order property matches index
            Tasks[index] = updatedTask; // Replace item (triggers CollectionChanged -> Replace)
            await TriggerSave(); // Save the change
        }
        else { Debug.WriteLine($"HandleUpdateTask: Update failed: Task with ID {updatedTask.Id} not found."); }
    }

    // Handler for DeleteTaskMessage
    private async void HandleDeleteTask(Guid taskId)
    {
        var taskToRemove = Tasks.FirstOrDefault(t => t.Id == taskId);
        if (taskToRemove != null)
        {
            Debug.WriteLine($"Received DeleteTaskMessage for: {taskToRemove.Title} ({taskId})");
            Tasks.Remove(taskToRemove); // Remove triggers CollectionChanged -> Remove
            await TriggerSave(); // Save the change
        }
        else { Debug.WriteLine($"Delete failed: Task with ID {taskId} not found."); }
    }

    // *** MODIFIED: Handles the calendar setting change message ***
    private void HandleCalendarSettingChanged()
    {
        Debug.WriteLine("MainViewModel: Received CalendarSettingChangedMessage. Triggering LoadTasksCommand.");

        // --- Force Reload ---
        // Execute the LoadTasks command. This will clear the collection
        // and repopulate it, causing the CollectionView to regenerate items
        // and re-run the converters with the updated AppSettings value.
        // Ensure LoadTasks handles UI updates on the main thread if necessary.
        if (LoadTasksCommand.CanExecute(null))
        {
            LoadTasksCommand.Execute(null);
        }
        else
        {
            Debug.WriteLine("MainViewModel: LoadTasksCommand cannot execute (possibly still running).");
            // Consider queuing the refresh or using Task.Run if LoadTasksCommand handles threading correctly.
            // Task.Run(async () => await LoadTasks());
        }
    }


    // Saves the current state of the Tasks collection to the JSON file
    private async Task SaveTasks()
    {
        // Prevent concurrent saves
        bool acquiredLock = false;
        try
        {
            lock (_saveLock)
            {
                if (_isSaving)
                {
                    Debug.WriteLine("SaveTasks: Save already in progress. Exiting.");
                    return;
                }
                _isSaving = true;
                acquiredLock = true;
            }

            List<TaskItem> tasksToSave;
            // Lock collection only while reading/setting order
            lock (Tasks)
            {
                for (int i = 0; i < Tasks.Count; i++) { if (Tasks[i].Order != i) { Tasks[i].Order = i; } }
                tasksToSave = new List<TaskItem>(Tasks);
            }

            Debug.WriteLine($"SaveTasks: Attempting to save {tasksToSave.Count} tasks...");

            string json = JsonSerializer.Serialize(tasksToSave, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_filePath, json);
            Debug.WriteLine($"SaveTasks: Tasks saved successfully to {_filePath}");

#if WINDOWS && DEBUG
            // Optional: Open containing folder logic
#endif
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SaveTasks: Error saving tasks: {ex.Message}");
        }
        finally
        {
            // Ensure save flag is reset *only if lock was acquired*
            if (acquiredLock)
            {
                lock (_saveLock) { _isSaving = false; }
                Debug.WriteLine("SaveTasks finished, _isSaving reset.");
            }
        }
    }

    // Helper method to manage the _isSaving flag and debounce saves
    private async Task TriggerSave()
    {
        // Don't queue saves if one is already pending/running
        lock (_saveLock) { if (_isSaving) { Debug.WriteLine("TriggerSave: Skipped, save already in progress/pending."); return; } }

        Debug.WriteLine("TriggerSave: Initiating save cycle...");
        await Task.Delay(300); // Increased debounce delay slightly
        await SaveTasks(); // Performs the actual save (SaveTasks handles the _isSaving flag internally)
    }

} // End of MainViewModel class