// ViewModels/MainViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System; // Required for Guid, Exception
using System.Collections.Generic; // Required for Dictionary, List
using System.Collections.ObjectModel;
using System.Collections.Specialized; // Required for NotifyCollectionChangedEventArgs
using System.Diagnostics;
using System.IO; // Required for Path, File, Directory
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Tickly.Messages; // Ensure this namespace containing your messages is included
using Tickly.Models;
using Tickly.Views; // Required for nameof(AddTaskPopupPage)

namespace Tickly.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<TaskItem> _tasks;

    private readonly string _filePath;
    private bool _isSaving = false; // Flag to prevent concurrent saves
    private readonly object _saveLock = new object(); // Lock object for thread safety on _isSaving

    public MainViewModel()
    {
        _filePath = Path.Combine(FileSystem.AppDataDirectory, "tasks.json");
        _tasks = new ObservableCollection<TaskItem>();
        _tasks.CollectionChanged += Tasks_CollectionChanged; // Subscribe to collection changes

        // Load tasks asynchronously, but be cautious with async void in constructors
        // Consider triggering LoadTasks from the View's OnAppearing event for robustness
        LoadTasksCommand.Execute(null); // Or await LoadTasks(); if LoadTasksCommand were async Task

        // Register message listeners
        WeakReferenceMessenger.Default.Register<AddTaskMessage>(this, (r, m) => HandleAddTask(m.Value));
        WeakReferenceMessenger.Default.Register<UpdateTaskMessage>(this, (r, m) => HandleUpdateTask(m.Value));
        WeakReferenceMessenger.Default.Register<DeleteTaskMessage>(this, (r, m) => HandleDeleteTask(m.Value)); // Value is Guid
    }

    // Handles CollectionChanged events, specifically for item reordering (Move)
    private async void Tasks_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        // Only trigger save explicitly on Move action (drag/drop)
        if (e.Action == NotifyCollectionChangedAction.Move)
        {
            Debug.WriteLine($"CollectionChanged: Action={e.Action}, OldIndex={e.OldStartingIndex}, NewIndex={e.NewStartingIndex}");
            await TriggerSave(); // Use helper to manage save flag and debounce
        }
        // Add/Remove/Replace changes are saved via their respective message handlers calling TriggerSave()
    }

    // Command to navigate to the Add Task page (without parameters)
    [RelayCommand]
    private async Task NavigateToAddPage()
    {
        await Shell.Current.GoToAsync(nameof(AddTaskPopupPage), true); // Modal navigation
    }

    // Command to navigate to the Edit Task page (passing the selected TaskItem)
    [RelayCommand]
    private async Task NavigateToEditPage(TaskItem taskToEdit)
    {
        if (taskToEdit == null)
        {
            Debug.WriteLine("NavigateToEditPage: taskToEdit is null.");
            return;
        }

        Debug.WriteLine($"Navigating to edit task: {taskToEdit.Title} ({taskToEdit.Id})");
        // Pass the task object using Shell navigation parameters
        var navigationParameter = new Dictionary<string, object>
        {
            { "TaskToEdit", taskToEdit } // Key must match QueryProperty name in AddTaskPopupPage
        };
        await Shell.Current.GoToAsync(nameof(AddTaskPopupPage), true, navigationParameter); // Modal navigation with parameters
    }

    // Command to load tasks from the JSON file
    [RelayCommand]
    private async Task LoadTasks()
    {
        // Prevent loading if a save is in progress
        lock (_saveLock)
        {
            if (_isSaving)
            {
                Debug.WriteLine("LoadTasks skipped, save in progress.");
                return;
            }
            // Optional: Could set an _isLoading flag here if needed elsewhere
        }

        Debug.WriteLine($"Attempting to load tasks from: {_filePath}");

        // Unsubscribe temporarily to prevent CollectionChanged events during bulk load/clear
        Tasks.CollectionChanged -= Tasks_CollectionChanged;
        try
        {
            if (!File.Exists(_filePath))
            {
                Debug.WriteLine("Task file not found. Initializing empty task list.");
                Tasks.Clear(); // Ensure list is empty if file doesn't exist
                return;
            }

            string json = await File.ReadAllTextAsync(_filePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.WriteLine("Task file is empty. Initializing empty task list.");
                Tasks.Clear();
                return;
            }

            var loadedTasks = JsonSerializer.Deserialize<List<TaskItem>>(json);

            Tasks.Clear(); // Clear existing items before adding loaded ones

            if (loadedTasks != null && loadedTasks.Any())
            {
                // Sort tasks by their saved Order property
                var sortedTasks = loadedTasks.OrderBy(t => t.Order).ToList();
                foreach (var task in sortedTasks)
                {
                    Tasks.Add(task);
                }
                Debug.WriteLine($"Successfully loaded and added {Tasks.Count} tasks.");
            }
            else
            {
                Debug.WriteLine("Deserialization resulted in null or empty list.");
            }
        }
        catch (JsonException jsonEx)
        {
            Debug.WriteLine($"Error deserializing tasks JSON: {jsonEx.Message}");
            Tasks.Clear(); // Clear potentially partially loaded data on error
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading tasks: {ex.Message}");
            // Consider displaying an error to the user
            Tasks.Clear(); // Clear potentially partially loaded data on error
        }
        finally
        {
            // Always re-subscribe to CollectionChanged
            Tasks.CollectionChanged += Tasks_CollectionChanged;
            Debug.WriteLine("LoadTasks finished.");
            // Reset _isLoading flag if used
        }
    }

    // Saves the current state of the Tasks collection to the JSON file
    private async Task SaveTasks()
    {
        Debug.WriteLine("SaveTasks entered...");
        try
        {
            // Update the Order property for all tasks based on their current index
            for (int i = 0; i < Tasks.Count; i++)
            {
                if (Tasks[i].Order != i)
                {
                    Tasks[i].Order = i;
                    // Debug.WriteLine($"Updating order for '{Tasks[i].Title}' to {i}"); // Can be verbose
                }
            }

            // Create a copy for serialization to prevent issues if the collection is modified during serialization
            var tasksToSave = new List<TaskItem>(Tasks);

            string json = JsonSerializer.Serialize(tasksToSave, new JsonSerializerOptions { WriteIndented = true });

            await File.WriteAllTextAsync(_filePath, json);
            Debug.WriteLine($"Tasks saved successfully to {_filePath} ({tasksToSave.Count} items).");

            // Optional: Open containing folder in DEBUG mode on Windows
#if WINDOWS && DEBUG
            string directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            {
                try
                {
                     // Debug.WriteLine($"Opening directory: {directory}"); // Less verbose
                     Process.Start(new ProcessStartInfo()
                     {
                         FileName = directory,
                         UseShellExecute = true,
                         Verb = "open"
                     });
                }
                catch (Exception ex) { Debug.WriteLine($"Failed to open directory: {ex.Message}"); }
            }
            else { Debug.WriteLine($"Directory not found or invalid: {directory}"); }
#endif
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error saving tasks: {ex.Message}");
            // Consider notifying the user of the save failure
        }
        finally
        {
            // CRITICAL: Reset the _isSaving flag within the lock
            lock (_saveLock)
            {
                _isSaving = false;
            }
            Debug.WriteLine("SaveTasks finished, _isSaving reset.");
        }
    }

    // Handler for AddTaskMessage
    private async void HandleAddTask(TaskItem newTask)
    {
        if (newTask != null)
        {
            Debug.WriteLine($"Received AddTaskMessage for: {newTask.Title}");
            // Assign the correct order *before* adding to the collection
            newTask.Order = Tasks.Count;
            Tasks.Add(newTask); // Add the new task to the observable collection
            await TriggerSave(); // Trigger save operation
        }
        else
        {
            Debug.WriteLine("Received AddTaskMessage with null task.");
        }
    }

    // Handler for UpdateTaskMessage
    private async void HandleUpdateTask(TaskItem updatedTask)
    {
        if (updatedTask == null)
        {
            Debug.WriteLine("Received UpdateTaskMessage with null task.");
            return;
        }

        // Find the existing task in the collection by its unique ID
        var existingTask = Tasks.FirstOrDefault(t => t.Id == updatedTask.Id);
        if (existingTask != null)
        {
            Debug.WriteLine($"Received UpdateTaskMessage for: {updatedTask.Title} ({updatedTask.Id})");

            // Update properties of the existing task object.
            // Since TaskItem is ObservableObject, changes to its properties
            // should automatically update the UI if bindings are set up correctly.
            bool changed = false;
            if (existingTask.Title != updatedTask.Title) { existingTask.Title = updatedTask.Title; changed = true; }
            if (existingTask.Priority != updatedTask.Priority) { existingTask.Priority = updatedTask.Priority; changed = true; }
            if (existingTask.TimeType != updatedTask.TimeType) { existingTask.TimeType = updatedTask.TimeType; changed = true; }
            if (existingTask.DueDate != updatedTask.DueDate) { existingTask.DueDate = updatedTask.DueDate; changed = true; }
            if (existingTask.RepetitionType != updatedTask.RepetitionType) { existingTask.RepetitionType = updatedTask.RepetitionType; changed = true; }
            if (existingTask.RepetitionDayOfWeek != updatedTask.RepetitionDayOfWeek) { existingTask.RepetitionDayOfWeek = updatedTask.RepetitionDayOfWeek; changed = true; }
            // Order property will be updated during SaveTasks if needed

            // Optional: Explicitly trigger save only if something actually changed.
            if (changed)
            {
                Debug.WriteLine($"Properties updated for task: {existingTask.Title}");
                await TriggerSave(); // Trigger save operation
            }
            else
            {
                Debug.WriteLine($"No properties changed for task: {existingTask.Title}. Save not triggered.");
            }
        }
        else
        {
            Debug.WriteLine($"Update failed: Task with ID {updatedTask.Id} not found in the collection.");
        }
    }

    // Handler for DeleteTaskMessage
    private async void HandleDeleteTask(Guid taskId)
    {
        // Find the task to remove by its ID
        var taskToRemove = Tasks.FirstOrDefault(t => t.Id == taskId);
        if (taskToRemove != null)
        {
            Debug.WriteLine($"Received DeleteTaskMessage for: {taskToRemove.Title} ({taskId})");
            Tasks.Remove(taskToRemove); // Remove the task from the observable collection
            // Removing item will trigger CollectionChanged, but we explicitly save after the operation.
            await TriggerSave(); // Trigger save operation (this will also update order of subsequent items)
        }
        else
        {
            Debug.WriteLine($"Delete failed: Task with ID {taskId} not found in the collection.");
        }
    }

    // Helper method to manage the _isSaving flag and potentially debounce saves
    private async Task TriggerSave()
    {
        bool shouldSave = false;
        lock (_saveLock)
        {
            if (!_isSaving)
            {
                _isSaving = true; // Set flag immediately within lock
                shouldSave = true;
            }
        }

        if (shouldSave)
        {
            Debug.WriteLine("TriggerSave: Initiating save...");
            // Optional debounce delay - useful if multiple events trigger save rapidly (like drag/drop)
            await Task.Delay(250); // Adjust delay as needed (e.g., 100-500ms)
            await SaveTasks(); // Perform the save operation (SaveTasks resets the flag in its finally block)
        }
        else
        {
            Debug.WriteLine("TriggerSave: Skipped, save already in progress.");
            // If a save was skipped, the latest state will be captured by the ongoing save operation
            // or potentially the *next* one if changes occur after the current save completes.
            // For absolute certainty, a more complex queueing or "save requested" flag could be used.
        }
    }

    // Optional: Implement IDisposable or similar if using resources that need cleanup,
    // especially if message subscriptions could cause leaks (WeakReferenceMessenger helps prevent this).
    // public void UnregisterMessages()
    // {
    //     WeakReferenceMessenger.Default.UnregisterAll(this);
    // }
}