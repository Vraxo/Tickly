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
    private readonly TaskVisualStateService _taskVisualStateService;

    [ObservableProperty]
    private double taskProgress;

    [ObservableProperty]
    private Color taskProgressColor;

    public MainViewModel()
    {
        _filePath = Path.Combine(FileSystem.AppDataDirectory, "tasks.json");
        _tasks = new();
        _taskVisualStateService = new TaskVisualStateService();

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
        bool acquiredLock = false;
        lock (_saveLock) { if (_isSaving) return; acquiredLock = true; }
        if (!acquiredLock) return;

        Debug.WriteLine($"LoadTasksAsync: Attempting to load tasks from: {_filePath}");
        bool wasSubscribed = false;
        bool changesMade = false;
        List<TaskItem> loadedTasks = [];

        try
        {
            if (Tasks != null) { Tasks.CollectionChanged -= Tasks_CollectionChanged; wasSubscribed = true; }

            if (File.Exists(_filePath))
            {
                string json = await File.ReadAllTextAsync(_filePath);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    try { loadedTasks = JsonSerializer.Deserialize<List<TaskItem>>(json) ?? []; }
                    catch (JsonException jsonException) { Debug.WriteLine($"LoadTasksAsync: Error deserializing tasks JSON: {jsonException.Message}"); loadedTasks = []; }
                }
            }

            DateTime today = DateTime.Today;
            foreach (TaskItem task in loadedTasks)
            {
                if (task.TimeType == TaskTimeType.Repeating && task.DueDate.HasValue && task.DueDate.Value.Date < today)
                {
                    DateTime originalDueDate = task.DueDate.Value.Date;
                    DateTime nextValidDueDate = CalculateNextValidDueDateForRepeatingTask(task, today, originalDueDate);
                    if (task.DueDate.Value.Date != nextValidDueDate)
                    {
                        task.DueDate = nextValidDueDate; changesMade = true;
                    }
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
            });

            if (changesMade) { TriggerSave(); }
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"LoadTasksAsync: Error loading tasks: {exception.GetType().Name} - {exception.Message}");
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
            if (acquiredLock) { lock (_saveLock) { /* Release lock */ } }
        }
    }

    private static DateTime CalculateNextValidDueDateForRepeatingTask(TaskItem task, DateTime today, DateTime originalDueDate)
    {
        DateTime nextValidDueDate = originalDueDate;
        switch (task.RepetitionType)
        {
            case TaskRepetitionType.Daily: nextValidDueDate = today; break;
            case TaskRepetitionType.AlternateDay:
                double daysDifference = (today - originalDueDate).TotalDays;
                nextValidDueDate = daysDifference % 2 == 0 ? today : today.AddDays(1);
                break;
            case TaskRepetitionType.Weekly:
                if (task.RepetitionDayOfWeek.HasValue) nextValidDueDate = DateUtils.GetNextWeekday(today, task.RepetitionDayOfWeek.Value);
                else while (nextValidDueDate < today) nextValidDueDate = nextValidDueDate.AddDays(7);
                break;
        }
        return nextValidDueDate;
    }

    [RelayCommand]
    private async Task MarkTaskDone(TaskItem? task)
    {
        if (task is null || task.IsFadingOut) return;

        if (task.TimeType == TaskTimeType.None || task.TimeType == TaskTimeType.SpecificDate)
        {
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
        else if (task.TimeType == TaskTimeType.Repeating)
        {
            DateTime? nextDueDate = DateUtils.CalculateNextDueDate(task);
            if (nextDueDate.HasValue)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    task.DueDate = nextDueDate;
                    UpdateUiVisualState();
                    TriggerSave();
                });
            }
            else
            {
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
        if (task == null ||
            task.TimeType != TaskTimeType.Repeating ||
            task.RepetitionType != TaskRepetitionType.Daily ||
            !task.DueDate.HasValue ||
            task.DueDate.Value.Date != DateTime.Today.AddDays(1))
        {
            Debug.WriteLine($"ResetDailyTask: Task does not meet reset criteria (ID: {task?.Id}, Due: {task?.DueDate})");
            return;
        }

        Debug.WriteLine($"ResetDailyTask: Resetting task '{task.Title}' (ID: {task.Id}) due date to today.");

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            task.DueDate = DateTime.Today;
            UpdateUiVisualState();
            TriggerSave();
        });
    }

    private async Task HandleCalendarSettingChanged() { await LoadTasksAsync(); }

    private async void HandleAddTask(TaskItem? newTask)
    {
        if (newTask is null) return;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            newTask.Order = Tasks.Count;
            Tasks.Add(newTask);
            UpdateUiVisualState();
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
                updatedTask.Order = Tasks[index].Order;
                updatedTask.Index = index;
                Tasks[index] = updatedTask;
                UpdateUiVisualState();
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
        UpdateUiVisualState();
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
        bool acquiredLock = false;
        List<TaskItem> tasksToSave = [];
        try
        {
            lock (_saveLock) { if (_isSaving) { return; } _isSaving = true; acquiredLock = true; }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                tasksToSave = new List<TaskItem>(Tasks);
            });

            if (tasksToSave != null && tasksToSave.Count > 0)
            {
                JsonSerializerOptions options = new() { WriteIndented = true };
                string json = JsonSerializer.Serialize(tasksToSave, options);
                await File.WriteAllTextAsync(_filePath, json);
            }
            else if (tasksToSave != null && tasksToSave.Count == 0)
            {
                if (File.Exists(_filePath)) { File.Delete(_filePath); }
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
        await LoadTasksAsync();
    }
}