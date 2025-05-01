namespace Tickly.ViewModels;

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Tickly.Messages;
using Tickly.Models;
using Tickly.Services; // Include both services
using Tickly.Utils;
using Tickly.Views;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly ITaskPersistenceService _persistenceService;
    private readonly ITaskStateCalculator _taskStateCalculator; // Add calculator service

    // Static colors removed - they are now managed by the calculator service instance

    private ObservableCollection<TaskItem> _tasks;

    [ObservableProperty]
    private double taskProgress;

    [ObservableProperty]
    private Color taskProgressColor;

    public ObservableCollection<TaskItem> Tasks
    {
        get => _tasks;
        private set
        {
            var oldValue = _tasks;
            if (SetProperty(ref _tasks, value))
            {
                if (oldValue != null)
                {
                    oldValue.CollectionChanged -= Tasks_CollectionChanged;
                }
                if (_tasks != null)
                {
                    _tasks.CollectionChanged += Tasks_CollectionChanged;
                }
                // Update state whenever the collection instance changes
                UpdateCalculatedStateProperties();
            }
        }
    }

    // Constructor injection for both services
    public MainViewModel(ITaskPersistenceService persistenceService, ITaskStateCalculator taskStateCalculator)
    {
        _persistenceService = persistenceService;
        _taskStateCalculator = taskStateCalculator; // Assign injected calculator

        _tasks = [];
        _tasks.CollectionChanged += Tasks_CollectionChanged;

        _ = LoadTasksAsync();

        WeakReferenceMessenger.Default.Register<AddTaskMessage>(this, (recipient, message) => HandleAddTask(message.Value));
        WeakReferenceMessenger.Default.Register<UpdateTaskMessage>(this, (recipient, message) => HandleUpdateTask(message.Value));
        WeakReferenceMessenger.Default.Register<DeleteTaskMessage>(this, (recipient, message) => HandleDeleteTask(message.Value));
        WeakReferenceMessenger.Default.Register<CalendarSettingChangedMessage>(this, async (recipient, message) => await HandleCalendarSettingChanged());
        WeakReferenceMessenger.Default.Register<TasksReloadRequestedMessage>(this, async (recipient, message) => await HandleTasksReloadRequested());

        // Initial calculation after setup
        UpdateCalculatedStateProperties();
    }

    [RelayCommand]
    private async Task LoadTasksAsync()
    {
        Debug.WriteLine("MainViewModel: LoadTasksAsync called.");
        var changesMade = false;

        try
        {
            var loadedTasks = await _persistenceService.LoadTasksAsync();

            var today = DateTime.Today;
            foreach (var task in loadedTasks)
            {
                if (task.TimeType == TaskTimeType.Repeating && task.DueDate.HasValue && task.DueDate.Value.Date < today)
                {
                    var originalDueDate = task.DueDate.Value.Date;
                    var nextValidDueDate = CalculateNextValidDueDateForRepeatingTask(task, today, originalDueDate);
                    if (task.DueDate.Value.Date != nextValidDueDate)
                    {
                        task.DueDate = nextValidDueDate; changesMade = true;
                    }
                }
                task.IsFadingOut = false; // Reset transient UI state
                // PositionColor will be set by the calculator later
            }

            var tasksToAdd = loadedTasks.OrderBy(task => task.Order).ToList();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                // Assign new collection instance to trigger setter logic
                Tasks = new ObservableCollection<TaskItem>(tasksToAdd);
                // State properties are updated via setter and CollectionChanged handler
            });

            if (changesMade)
            {
                TriggerSave();
            }
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"MainViewModel: Error during LoadTasksAsync: {exception.GetType().Name} - {exception.Message}");
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Tasks = []; // Assign empty collection
            });
        }
    }

    [RelayCommand]
    private async Task NavigateToAddPage()
    {
        try
        {
            var navigationParameter = new Dictionary<string, object> { { "TaskToEdit", null! } };
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
            var navigationParameter = new Dictionary<string, object> { { "TaskToEdit", taskToEdit } };
            await Shell.Current.GoToAsync(nameof(AddTaskPopupPage), true, navigationParameter);
        }
        catch (Exception exception) { Debug.WriteLine($"Error navigating to edit page for task {taskToEdit.Id}: {exception.Message}"); }
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
                removedSuccessfully = Tasks.Remove(task); // Triggers CollectionChanged -> UpdateCalculatedStateProperties
                if (removedSuccessfully) TriggerSave();
                else task.IsFadingOut = false;
            });
        }
        else if (task.TimeType == TaskTimeType.Repeating)
        {
            var nextDueDate = DateUtils.CalculateNextDueDate(task);
            if (nextDueDate.HasValue)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    task.DueDate = nextDueDate;
                    UpdateCalculatedStateProperties(); // Recalculate progress explicitly as collection didn't change
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
                    removedSuccessfully = Tasks.Remove(task); // Triggers CollectionChanged -> UpdateCalculatedStateProperties
                    if (removedSuccessfully) TriggerSave();
                    else task.IsFadingOut = false;
                });
            }
        }
    }

    [RelayCommand]
    private async Task ResetDailyTask(TaskItem? task)
    {
        if (task is null ||
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
            UpdateCalculatedStateProperties(); // Recalculate progress explicitly
            TriggerSave();
        });
    }

    // This remains as it's specific task lifecycle logic, not general state calculation
    private static DateTime CalculateNextValidDueDateForRepeatingTask(TaskItem task, DateTime today, DateTime originalDueDate)
    {
        var nextValidDueDate = originalDueDate;
        switch (task.RepetitionType)
        {
            case TaskRepetitionType.Daily: nextValidDueDate = today; break;
            case TaskRepetitionType.AlternateDay:
                var daysDifference = (today - originalDueDate).TotalDays;
                nextValidDueDate = daysDifference % 2 == 0 ? today : today.AddDays(1);
                break;
            case TaskRepetitionType.Weekly:
                if (task.RepetitionDayOfWeek.HasValue) nextValidDueDate = DateUtils.GetNextWeekday(today, task.RepetitionDayOfWeek.Value);
                else
                {
                    while (nextValidDueDate < today) nextValidDueDate = nextValidDueDate.AddDays(7);
                }
                break;
        }
        return nextValidDueDate;
    }

    private void TriggerSave()
    {
        _persistenceService.TriggerSave(Tasks);
    }

    private async Task HandleCalendarSettingChanged()
    {
        await LoadTasksAsync();
    }

    private async Task HandleTasksReloadRequested()
    {
        Debug.WriteLine("MainViewModel: Received TasksReloadRequestedMessage. Reloading tasks...");
        await LoadTasksAsync();
    }

    private async void HandleAddTask(TaskItem? newTask)
    {
        if (newTask is null) return;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            // Order will be set by the calculator in UpdateCalculatedStateProperties via CollectionChanged
            Tasks.Add(newTask); // Triggers CollectionChanged -> UpdateCalculatedStateProperties
            TriggerSave();
        });
    }

    private async void HandleUpdateTask(TaskItem? updatedTask)
    {
        if (updatedTask is null) return;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            var index = Tasks.ToList().FindIndex(task => task.Id == updatedTask.Id);
            if (index != -1)
            {
                // Preserve existing order/index properties potentially, though calculator will overwrite index/order
                // updatedTask.Order = Tasks[index].Order;
                // updatedTask.Index = index;
                Tasks[index] = updatedTask; // Triggers CollectionChanged (Replace) -> UpdateCalculatedStateProperties
                TriggerSave();
            }
            else
            {
                Debug.WriteLine($"HandleUpdateTask: Task with ID {updatedTask.Id} not found for update.");
            }
        });
    }

    private async void HandleDeleteTask(Guid taskId)
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            var taskToRemove = Tasks.FirstOrDefault(task => task.Id == taskId);
            if (taskToRemove != null)
            {
                var removed = Tasks.Remove(taskToRemove); // Triggers CollectionChanged -> UpdateCalculatedStateProperties
                if (removed) TriggerSave();
            }
            else
            {
                Debug.WriteLine($"HandleDeleteTask: Task with ID {taskId} not found for deletion.");
            }
        });
    }

    private void Tasks_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs eventArgs)
    {
        // Central point to update all calculated state when the collection changes
        UpdateCalculatedStateProperties();

        // Save is triggered explicitly by most actions (Add, Remove, Update, MarkDone, Reset)
        // Only trigger save here specifically for Move actions if using drag-and-drop reordering
        if (eventArgs.Action == NotifyCollectionChangedAction.Move)
        {
            TriggerSave();
        }
    }

    // Central method to update all calculated properties using the service
    private void UpdateCalculatedStateProperties()
    {
        if (!MainThread.IsMainThread)
        {
            MainThread.BeginInvokeOnMainThread(UpdateCalculatedStateProperties);
            return;
        }

        // Update individual task index/order/color properties
        _taskStateCalculator.UpdateTaskIndicesAndPositionColors(Tasks);

        // Update overall progress and color properties
        var (progress, color) = _taskStateCalculator.CalculateOverallProgressState(Tasks);
        TaskProgress = progress;
        TaskProgressColor = color;
    }

    // Internal calculation methods removed - logic moved to TaskStateCalculator
    // UpdateTaskIndexAndColorProperty() removed
    // UpdateTaskIndexAndColorPropertyInternal() removed
    // UpdateTaskProgressAndColor() removed
}