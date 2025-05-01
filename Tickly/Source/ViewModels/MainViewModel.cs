// File: Source/ViewModels/MainViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tickly.Messages;
using Tickly.Models;
using Tickly.Services;
using Tickly.Utils; // Keep for non-repeating date utils if any, or general utils
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

    private Timer? _debounceTimer;
    private readonly TaskVisualStateService _taskVisualStateService;
    private readonly TaskPersistenceService _taskPersistenceService;
    private readonly RepeatingTaskService _repeatingTaskService; // ADDED

    [ObservableProperty]
    private double taskProgress;

    [ObservableProperty]
    private Color taskProgressColor;

    public MainViewModel()
    {
        _tasks = new();
        _taskVisualStateService = new TaskVisualStateService();
        _taskPersistenceService = new TaskPersistenceService();
        _repeatingTaskService = new RepeatingTaskService(); // ADDED: Instantiate the service

        _ = LoadTasksAsync();

        WeakReferenceMessenger.Default.Register<AddTaskMessage>(this, (recipient, message) => HandleAddTask(message.Value));
        WeakReferenceMessenger.Default.Register<UpdateTaskMessage>(this, (recipient, message) => HandleUpdateTask(message.Value));
        WeakReferenceMessenger.Default.Register<DeleteTaskMessage>(this, (recipient, message) => HandleDeleteTask(message.Value));
        WeakReferenceMessenger.Default.Register<CalendarSettingChangedMessage>(this, async (recipient, message) => await HandleCalendarSettingChanged());
        WeakReferenceMessenger.Default.Register<TasksReloadRequestedMessage>(this, async (recipient, message) => await HandleTasksReloadRequested());

        UpdateUiVisualState();
    }

    [RelayCommand]
    private async Task NavigateToAddPage()
    {
        try
        {
            Dictionary<string, object> navigationParameter = new() { { "TaskToEdit", null! } };
            await Shell.Current.GoToAsync(nameof(AddTaskPopupPage), true, navigationParameter);
        }
        catch (Exception exception) { Debug.WriteLine($"Error navigating to add page: {exception.Message}"); }
    }

    [RelayCommand]
    private async Task NavigateToEditPage(TaskItem? taskToEdit)
    {
        if (taskToEdit is null) return;

        try
        {
            Dictionary<string, object> navigationParameter = new() { { "TaskToEdit", taskToEdit } };
            await Shell.Current.GoToAsync(nameof(AddTaskPopupPage), true, navigationParameter);
        }
        catch (Exception exception) { Debug.WriteLine($"Error navigating to edit page for task {taskToEdit.Id}: {exception.Message}"); }
    }

    [RelayCommand]
    private async Task LoadTasksAsync()
    {
        Debug.WriteLine($"MainViewModel.LoadTasksAsync: Requesting tasks from persistence service.");
        bool wasSubscribed = false;
        bool changesMade = false;
        List<TaskItem> loadedTasks = [];

        try
        {
            if (Tasks != null) { Tasks.CollectionChanged -= Tasks_CollectionChanged; wasSubscribed = true; }

            loadedTasks = await _taskPersistenceService.LoadTasksAsync();
            Debug.WriteLine($"MainViewModel.LoadTasksAsync: Received {loadedTasks.Count} tasks from service.");

            DateTime today = DateTime.Today;
            foreach (TaskItem task in loadedTasks)
            {
                // Delegate repeating task date check to the service
                if (_repeatingTaskService.EnsureCorrectDueDateOnLoad(task, today))
                {
                    changesMade = true;
                }
                task.IsFadingOut = false;
                task.PositionColor = Colors.Transparent;
            }

            List<TaskItem> tasksToAdd = loadedTasks.OrderBy(task => task.Order).ToList();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Tasks.Clear();
                foreach (var task in tasksToAdd) { Tasks.Add(task); }
                UpdateUiVisualState();
                Debug.WriteLine($"MainViewModel.LoadTasksAsync: Updated Tasks collection on UI thread.");
            });

            if (changesMade)
            {
                Debug.WriteLine("MainViewModel.LoadTasksAsync: Changes made to due dates, triggering save.");
                TriggerSave();
            }
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"MainViewModel.LoadTasksAsync: Error during loading/processing: {exception.GetType().Name} - {exception.Message}");
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Tasks.Clear();
                UpdateUiVisualState();
            });
        }
        finally
        {
            if (Tasks != null && (wasSubscribed || Tasks.Any())) { Tasks.CollectionChanged -= Tasks_CollectionChanged; Tasks.CollectionChanged += Tasks_CollectionChanged; }
            else if (Tasks != null) { Tasks.CollectionChanged += Tasks_CollectionChanged; }
            Debug.WriteLine("MainViewModel.LoadTasksAsync: Finished.");
        }
    }

    // REMOVED: CalculateNextValidDueDateForRepeatingTask (moved to service)

    [RelayCommand]
    private async Task MarkTaskDone(TaskItem? task)
    {
        if (task is null || task.IsFadingOut) return;

        if (task.TimeType == TaskTimeType.None || task.TimeType == TaskTimeType.SpecificDate)
        {
            // Standard removal logic
            task.IsFadingOut = true;
            await Task.Delay(350);
            bool removedSuccessfully = false;
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                removedSuccessfully = Tasks.Remove(task);
                if (removedSuccessfully) { UpdateUiVisualState(); TriggerSave(); }
                else task.IsFadingOut = false; // Reset if removal failed unexpectedly
            });
        }
        else if (task.TimeType == TaskTimeType.Repeating)
        {
            // Delegate repeating task update to the service
            bool dateUpdated = _repeatingTaskService.UpdateRepeatingTaskDueDate(task);

            if (dateUpdated)
            {
                // If the date was successfully updated, refresh UI and save
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    UpdateUiVisualState();
                    TriggerSave();
                });
            }
            else
            {
                // If the service indicates the date couldn't be updated (e.g., error or no next date), remove the task
                task.IsFadingOut = true;
                await Task.Delay(350);
                bool removedSuccessfully = false;
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    removedSuccessfully = Tasks.Remove(task);
                    if (removedSuccessfully) { UpdateUiVisualState(); TriggerSave(); }
                    else task.IsFadingOut = false;
                });
            }
        }
    }

    [RelayCommand]
    private async Task ResetDailyTask(TaskItem? task)
    {
        // Delegate reset logic to the service
        bool resetSuccessful = _repeatingTaskService.ResetDailyTaskDueDate(task);

        if (resetSuccessful)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                UpdateUiVisualState();
                TriggerSave();
            });
        }
        else
        {
            Debug.WriteLine($"ResetDailyTask Command: Task '{task?.Title}' did not meet criteria or service failed.");
        }
    }

    private async Task HandleCalendarSettingChanged() { await LoadTasksAsync(); }

    private async void HandleAddTask(TaskItem? newTask)
    {
        if (newTask is null) return;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            newTask.Order = Tasks.Count; // Assign order before adding
            Tasks.Add(newTask);
            UpdateUiVisualState(); // Update UI after adding
            TriggerSave();
        });
    }

    private async void HandleUpdateTask(TaskItem? updatedTask)
    {
        if (updatedTask is null) return;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            int index = Tasks.ToList().FindIndex(task => task.Id == updatedTask.Id);
            if (index != -1)
            {
                // Preserve original order and index during update
                updatedTask.Order = Tasks[index].Order;
                updatedTask.Index = index;
                Tasks[index] = updatedTask; // Replace item in the collection
                UpdateUiVisualState(); // Update UI after replace
                TriggerSave();
            }
            else Debug.WriteLine($"HandleUpdateTask: Task with ID {updatedTask.Id} not found.");
        });
    }

    private async void HandleDeleteTask(Guid taskId)
    {
        bool removedSuccessfully = false;
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            TaskItem? taskToRemove = Tasks.FirstOrDefault(task => task.Id == taskId);
            if (taskToRemove is not null)
            {
                removedSuccessfully = Tasks.Remove(taskToRemove);
                if (removedSuccessfully) { UpdateUiVisualState(); TriggerSave(); }
            }
        });
    }

    private void Tasks_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs eventArgs)
    {
        // Always update UI state on any change
        UpdateUiVisualState();
        // Only trigger save specifically on Move, as Add/Remove/Replace are handled by their respective message handlers/commands
        if (eventArgs.Action == NotifyCollectionChangedAction.Move)
        {
            TriggerSave();
        }
    }

    private void UpdateUiVisualState()
    {
        if (!MainThread.IsMainThread)
        {
            MainThread.BeginInvokeOnMainThread(UpdateUiVisualState);
            return;
        }

        _taskVisualStateService.UpdateTaskIndicesAndColors(Tasks);
        var progressResult = _taskVisualStateService.CalculateProgress(Tasks);
        TaskProgress = progressResult.Progress;
        TaskProgressColor = progressResult.ProgressColor;
    }

    private async Task SaveTasks()
    {
        Debug.WriteLine("MainViewModel.SaveTasks: Delegating save to TaskPersistenceService.");
        List<TaskItem> currentTasks = [];
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            currentTasks = new List<TaskItem>(Tasks);
        });
        await _taskPersistenceService.SaveTasksAsync(currentTasks);
        Debug.WriteLine("MainViewModel.SaveTasks: Save delegation completed.");
    }

    private void TriggerSave()
    {
        _debounceTimer?.Dispose();
        _debounceTimer = new Timer(async (state) => { await SaveTasks(); _debounceTimer?.Dispose(); _debounceTimer = null; }, null, TimeSpan.FromMilliseconds(500), Timeout.InfiniteTimeSpan);
    }

    private async Task HandleTasksReloadRequested()
    {
        Debug.WriteLine("MainViewModel: Received TasksReloadRequestedMessage. Reloading tasks...");
        await LoadTasksAsync();
    }
}