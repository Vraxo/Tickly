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
    }

    // Handles CollectionChanged events, specifically for item reordering (Move)
    private async void Tasks_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        // Save only needed for Move action here, as Add/Update/Delete trigger saves explicitly
        if (e.Action == NotifyCollectionChangedAction.Move)
        {
            Debug.WriteLine($"CollectionChanged: Action={e.Action}, OldIndex={e.OldStartingIndex}, NewIndex={e.NewStartingIndex}");
            await TriggerSave();
        }
        else
        {
            Debug.WriteLine($"CollectionChanged: Action={e.Action}"); // Log other actions if interested
        }
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
        if (taskToEdit == null) return;
        Debug.WriteLine($"Navigating to edit task: {taskToEdit.Title} ({taskToEdit.Id})");
        var navigationParameter = new Dictionary<string, object> { { "TaskToEdit", taskToEdit } };
        await Shell.Current.GoToAsync(nameof(AddTaskPopupPage), true, navigationParameter);
    }

    // Command to load tasks from the JSON file
    [RelayCommand]
    private async Task LoadTasks()
    {
        lock (_saveLock) { if (_isSaving) { Debug.WriteLine("LoadTasks skipped, save in progress."); return; } }
        Debug.WriteLine($"Attempting to load tasks from: {_filePath}");

        Tasks.CollectionChanged -= Tasks_CollectionChanged;
        try
        {
            if (!File.Exists(_filePath)) { Debug.WriteLine("Task file not found."); Tasks.Clear(); return; }
            string json = await File.ReadAllTextAsync(_filePath);
            if (string.IsNullOrWhiteSpace(json)) { Debug.WriteLine("Task file is empty."); Tasks.Clear(); return; }

            var loadedTasks = JsonSerializer.Deserialize<List<TaskItem>>(json);
            Tasks.Clear();

            if (loadedTasks != null && loadedTasks.Any())
            {
                var sortedTasks = loadedTasks.OrderBy(t => t.Order).ToList();
                foreach (var task in sortedTasks) { Debug.WriteLine($"LoadTasks: Loading Task='{task.Title}', TimeType='{task.TimeType}'"); Tasks.Add(task); }
                Debug.WriteLine($"Successfully loaded and added {Tasks.Count} tasks.");
            }
            else { Debug.WriteLine("Deserialization resulted in null or empty list."); }
        }
        catch (Exception ex) { Debug.WriteLine($"Error loading tasks: {ex.GetType().Name} - {ex.Message}"); Tasks.Clear(); }
        finally { Tasks.CollectionChanged += Tasks_CollectionChanged; Debug.WriteLine("LoadTasks finished."); }
    }

    // Handler for AddTaskMessage
    private async void HandleAddTask(TaskItem newTask)
    {
        if (newTask == null) { Debug.WriteLine("Received AddTaskMessage with null task."); return; }
        Debug.WriteLine($"Received AddTaskMessage for: {newTask.Title}, TimeType: {newTask.TimeType}");
        newTask.Order = Tasks.Count; // Assign order before adding
        Tasks.Add(newTask); // Add triggers CollectionChanged
        await TriggerSave(); // Save the new state
    }

    // --- *** MODIFIED HandleUpdateTask *** ---
    private async void HandleUpdateTask(TaskItem updatedTask)
    {
        if (updatedTask == null)
        {
            Debug.WriteLine("Received UpdateTaskMessage with null task.");
            return;
        }

        // Find the index of the existing task
        int index = -1;
        for (int i = 0; i < Tasks.Count; i++)
        {
            if (Tasks[i].Id == updatedTask.Id)
            {
                index = i;
                break;
            }
        }

        if (index != -1)
        {
            Debug.WriteLine($"HandleUpdateTask: Found task '{updatedTask.Title}' at index {index}. Replacing item in collection.");

            // Preserve the correct order from the incoming item if it was potentially changed elsewhere,
            // otherwise, maintain the current index as the order.
            // Typically, order only changes on drag/drop (Move), so using 'index' is safe here.
            updatedTask.Order = index;

            // *** Replace the item in the ObservableCollection ***
            // This fires CollectionChanged with Replace action, forcing UI refresh for this item.
            Tasks[index] = updatedTask;

            // Although replacing the item updates UI, save the changes persistently.
            await TriggerSave();
        }
        else
        {
            Debug.WriteLine($"HandleUpdateTask: Update failed: Task with ID {updatedTask.Id} not found in the collection.");
        }
    }
    // --- *** END OF MODIFICATION *** ---

    // Handler for DeleteTaskMessage
    private async void HandleDeleteTask(Guid taskId)
    {
        var taskToRemove = Tasks.FirstOrDefault(t => t.Id == taskId);
        if (taskToRemove != null)
        {
            Debug.WriteLine($"Received DeleteTaskMessage for: {taskToRemove.Title} ({taskId})");
            Tasks.Remove(taskToRemove); // Remove triggers CollectionChanged
            await TriggerSave(); // Save the new state (order of subsequent items will be updated in SaveTasks)
        }
        else
        {
            Debug.WriteLine($"Delete failed: Task with ID {taskId} not found in the collection.");
        }
    }


    // Saves the current state of the Tasks collection to the JSON file
    private async Task SaveTasks()
    {
        List<TaskItem> tasksToSave;
        lock (Tasks) // Using Tasks collection itself as the lock object
        {
            for (int i = 0; i < Tasks.Count; i++)
            {
                if (Tasks[i].Order != i) { Tasks[i].Order = i; }
            }
            tasksToSave = new List<TaskItem>(Tasks);
        }

        Debug.WriteLine($"SaveTasks: Attempting to save {tasksToSave.Count} tasks...");
        // Optional detailed logging removed for brevity, can be re-added if needed
        // foreach(var task in tasksToSave) { Debug.WriteLine($"   SaveTasks: Task '{task.Title}' - TimeType: {task.TimeType}"); }

        try
        {
            string json = JsonSerializer.Serialize(tasksToSave, new JsonSerializerOptions { WriteIndented = true });
            // Optional detailed logging removed for brevity
            // Debug.WriteLine($"SaveTasks: Serialized JSON to be written:\n{json}");

            await File.WriteAllTextAsync(_filePath, json);
            Debug.WriteLine($"SaveTasks: Tasks saved successfully to {_filePath}");

#if WINDOWS && DEBUG
            // Optional: Open containing folder logic
#endif
        }
        catch (Exception ex) { Debug.WriteLine($"SaveTasks: Error saving tasks: {ex.Message}"); }
        finally
        {
            lock (_saveLock) { _isSaving = false; }
            Debug.WriteLine("SaveTasks finished, _isSaving reset.");
        }
    }

    // Helper method to manage the _isSaving flag and debounce saves
    private async Task TriggerSave()
    {
        bool shouldSave = false;
        lock (_saveLock) { if (!_isSaving) { _isSaving = true; shouldSave = true; } }

        if (shouldSave)
        {
            Debug.WriteLine("TriggerSave: Initiating save...");
            await Task.Delay(250); // Debounce
            await SaveTasks();
        }
        else { Debug.WriteLine("TriggerSave: Skipped, save already in progress."); }
    }
}