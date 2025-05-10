using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Tickly.Messages;
using Tickly.Models;
using Tickly.Services;
using Tickly.Views;

namespace Tickly.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private ObservableCollection<TaskItem> _tasks = [];

    public ObservableCollection<TaskItem> Tasks
    {
        get => _tasks;
        set => SetProperty(ref _tasks, value);
    }

    private Timer? _debounceTaskSaveTimer;
    private Timer? _debounceProgressSaveTimer;
    private readonly TaskVisualStateService _taskVisualStateService;
    private readonly TaskStorageService _taskStorageService;
    private readonly ProgressStorageService _progressStorageService;
    private readonly RepeatingTaskService _repeatingTaskService;

    [ObservableProperty]
    private double taskProgress = 0;

    [ObservableProperty]
    private Color taskProgressColor = Color.FromRgba(0, 255, 0, 1);

    public MainViewModel(
        TaskStorageService taskStorageService,
        ProgressStorageService progressStorageService,
        RepeatingTaskService repeatingTaskService,
        TaskVisualStateService taskVisualStateService)
    {
        _taskStorageService = taskStorageService;
        _progressStorageService = progressStorageService;
        _repeatingTaskService = repeatingTaskService;
        _taskVisualStateService = taskVisualStateService;

        _ = LoadTasksAsync();

        WeakReferenceMessenger.Default.Register<AddTaskMessage>(this, (recipient, message) => HandleAddTask(message.Value));
        WeakReferenceMessenger.Default.Register<UpdateTaskMessage>(this, (recipient, message) => HandleUpdateTask(message.Value));
        WeakReferenceMessenger.Default.Register<DeleteTaskMessage>(this, (recipient, message) => HandleDeleteTask(message.Value));
        WeakReferenceMessenger.Default.Register<CalendarSettingsChangedMessage>(this, async (recipient, message) => await HandleCalendarSettingChanged());
        WeakReferenceMessenger.Default.Register<TasksReloadRequestedMessage>(this, async (recipient, message) => await HandleTasksReloadRequested());

        UpdateUiVisualState();
    }

    [RelayCommand]
    private async Task NavigateToAddPage()
    {
        Debug.WriteLine($"MainViewModel.NavigateToAddPage: Command invoked on {DeviceInfo.Platform}.");
        try
        {
            Dictionary<string, object> navigationParameter = new() { { "TaskToEdit", null! } };
            await Shell.Current.GoToAsync(nameof(AddTaskPopupPage), true, navigationParameter);
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"Error navigating to add page: {exception.Message}");
        }
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
        catch (Exception exception)
        {
            Debug.WriteLine($"Error navigating to edit page for task {taskToEdit.Id}: {exception.Message}");
        }
    }

    [RelayCommand]
    private async Task LoadTasksAsync()
    {
        Debug.WriteLine($"MainViewModel.LoadTasksAsync: Requesting tasks from storage service.");

        bool wasSubscribed = Tasks.Count > 0;
        bool changesMade = false;
        List<TaskItem> loadedTasks = [];

        try
        {
            Tasks.CollectionChanged -= Tasks_CollectionChanged;

            loadedTasks = await _taskStorageService.LoadTasksAsync();
            Debug.WriteLine($"MainViewModel.LoadTasksAsync: Received {loadedTasks.Count} tasks from service.");

            var today = DateTime.Today;

            foreach (TaskItem task in loadedTasks)
            {
                if (_repeatingTaskService.EnsureCorrectDueDateOnLoad(task, today))
                {
                    changesMade = true;
                }

                task.IsFadingOut = false;
                task.PositionColor = Colors.Transparent;
            }

            List<TaskItem> tasksToAdd = [.. loadedTasks.OrderBy(task => task.Order)];

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
            Tasks.CollectionChanged += Tasks_CollectionChanged;
            Debug.WriteLine("MainViewModel.LoadTasksAsync: Finished and re-subscribed to CollectionChanged.");
        }
    }

    [RelayCommand]
    private async Task MarkTaskDone(TaskItem? task)
    {
        if (task is null || task.IsFadingOut)
        {
            return;
        }

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
            bool dateUpdated = _repeatingTaskService.UpdateRepeatingTaskDueDate(task);

            if (dateUpdated)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    Tasks.CollectionChanged -= Tasks_CollectionChanged;
                    var sortedTasks = Tasks.OrderBy(t => t.Order).ToList();
                    Tasks.Clear();
                    foreach (var t in sortedTasks) Tasks.Add(t);
                    Tasks.CollectionChanged += Tasks_CollectionChanged;

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
        bool resetSuccessful = _repeatingTaskService.ResetDailyTaskDueDate(task);

        if (resetSuccessful)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Tasks.CollectionChanged -= Tasks_CollectionChanged;
                var sortedTasks = Tasks.OrderBy(t => t.Order).ToList();
                Tasks.Clear();
                foreach (var t in sortedTasks) Tasks.Add(t);
                Tasks.CollectionChanged += Tasks_CollectionChanged;

                UpdateUiVisualState();
                TriggerSave();
            });
        }
        else
        {
            Debug.WriteLine($"ResetDailyTask Command: Task '{task?.Title}' (ID: {task?.Id}) did not meet criteria or service failed.");
        }
    }

    private async Task HandleCalendarSettingChanged()
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            UpdateUiVisualState();
        });
    }

    private async void HandleAddTask(TaskItem? newTask)
    {
        if (newTask is null)
        {
            return;
        }

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
        if (updatedTask is null)
        {
            return;
        }

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
            else
            {
                Debug.WriteLine($"HandleUpdateTask: Task with ID {updatedTask.Id} not found.");
            }
        });
    }

    private async void HandleDeleteTask(Guid taskId)
    {
        bool removedSuccessfully = false;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            TaskItem? taskToRemove = Tasks.FirstOrDefault(task => task.Id == taskId);

            if (taskToRemove is null)
            {
                return;
            }

            removedSuccessfully = Tasks.Remove(taskToRemove);
            if (removedSuccessfully) { UpdateUiVisualState(); TriggerSave(); }
        });
    }

    private void Tasks_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs eventArgs)
    {
        UpdateUiVisualState();
        if (eventArgs.Action == NotifyCollectionChangedAction.Move)
        {
            for (int i = 0; i < Tasks.Count; i++)
            {
                if (Tasks[i].Order != i) Tasks[i].Order = i;
            }
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
        TaskProgressResult progressResult = _taskVisualStateService.CalculateProgress(Tasks);
        TaskProgress = progressResult.Progress;
        TaskProgressColor = progressResult.ProgressColor;

        TriggerSaveDailyProgress();
    }

    private async Task SaveTasks()
    {
        Debug.WriteLine("MainViewModel.SaveTasks: Delegating save to TaskStorageService.");
        List<TaskItem> currentTasks = [];

        currentTasks = [.. Tasks];

        await _taskStorageService.SaveTasksAsync(currentTasks);
        Debug.WriteLine("MainViewModel.SaveTasks: Save delegation completed.");
    }

    private void TriggerSave()
    {
        _debounceTaskSaveTimer?.Dispose();
        _debounceTaskSaveTimer = new(async (state) =>
        {
            await SaveTasks();
            _debounceTaskSaveTimer?.Dispose();
            _debounceTaskSaveTimer = null;
        },
        null,
        TimeSpan.FromMilliseconds(500),
        Timeout.InfiniteTimeSpan);
    }

    private async Task SaveCurrentDayProgressAsync()
    {
        if (_progressStorageService is null)
        {
            Debug.WriteLine("MainViewModel.SaveCurrentDayProgressAsync: ProgressStorageService is null. Cannot save progress.");
            return;
        }

        DateTime todayDate = DateTime.Today;
        double currentProgress = TaskProgress;

        Debug.WriteLine($"MainViewModel.SaveCurrentDayProgressAsync: Saving progress for {todayDate:yyyy-MM-dd} - {currentProgress:P2}");

        DailyProgress progressEntry = new(todayDate, currentProgress);
        await _progressStorageService.AddOrUpdateDailyProgressEntryAsync(progressEntry);

        Debug.WriteLine($"MainViewModel.SaveCurrentDayProgressAsync: Progress for {todayDate:yyyy-MM-dd} saved/updated.");
    }

    private void TriggerSaveDailyProgress()
    {
        _debounceProgressSaveTimer?.Dispose();
        _debounceProgressSaveTimer = new(async (state) =>
        {
            await SaveCurrentDayProgressAsync();
            _debounceProgressSaveTimer?.Dispose();
            _debounceProgressSaveTimer = null;
        },
        null,
        TimeSpan.FromMilliseconds(1000),
        Timeout.InfiniteTimeSpan);
    }

    public async Task FinalizeAndSaveProgressAsync()
    {
        _debounceProgressSaveTimer?.Dispose();
        _debounceProgressSaveTimer = null;
        await SaveCurrentDayProgressAsync();
        Debug.WriteLine("MainViewModel.FinalizeAndSaveProgressAsync: Final progress save executed for today.");
    }

    private async Task HandleTasksReloadRequested()
    {
        Debug.WriteLine("MainViewModel: Received TasksReloadRequestedMessage. Reloading tasks...");
        await LoadTasksAsync();
    }
}