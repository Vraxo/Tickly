// Source/ViewModels/MainViewModel.cs
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Tickly.Messages;
using Tickly.Models;
using Tickly.Services;
using Tickly.Utils;
using Tickly.Views; // Needed for AddTaskPopupPage reference

namespace Tickly.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    public ObservableCollection<TaskItem> Tasks { get; set; } = [];
    public List<string> SortOptionsDisplay { get; set; }

    public string SelectedSortOption
    {
        get => field; // Use field keyword for auto-property backing field

        set
        {
            if (!SetProperty(ref field, value))
            {
                return;
            }

            SortOrderType newSortOrder = value switch
            {
                "Priority (High First)" => SortOrderType.PriorityHighFirst,
                "Priority (Low First)" => SortOrderType.PriorityLowFirst,
                _ => SortOrderType.Manual
            };

            // Only update AppSettings if the user explicitly selects a non-manual sort
            if (newSortOrder is not SortOrderType.Manual)
            {
                AppSettings.SelectedSortOrder = newSortOrder;
            }
            // If they select Manual, we don't change AppSettings.SelectedSortOrder here.
            // The actual sorting (or lack thereof) is handled by ApplySortOrderCommand.

            ApplySortOrderCommand.Execute(null); // Apply the newly selected sort immediately
        }
    }

    [ObservableProperty]
    private double taskCompletionProgress;

    private readonly ITaskPersistenceService taskPersistenceService;
    private bool isSaving;
    private Timer? debounceTimer;
    private readonly Lock saveLock = new();
    private const int TargetTaskCount = 15; // Target number for 0% progress
    private string field = string.Empty; // Backing field for SelectedSortOption

    public MainViewModel(ITaskPersistenceService persistenceService)
    {
        taskPersistenceService = persistenceService;

        SortOptionsDisplay =
        [
            "Manual Order",
            "Priority (High First)",
            "Priority (Low First)"
        ];

        SelectedSortOption = GetSortOptionDisplayString(AppSettings.SelectedSortOrder);

        _ = LoadTasksAsync(); // Load tasks and update progress initially

        WeakReferenceMessenger.Default.Register<AddTaskMessage>(this, (recipient, message) => HandleAddTask(message.Value));
        WeakReferenceMessenger.Default.Register<UpdateTaskMessage>(this, (recipient, message) => HandleUpdateTask(message.Value));
        WeakReferenceMessenger.Default.Register<DeleteTaskMessage>(this, (recipient, message) => HandleDeleteTask(message.Value));
        WeakReferenceMessenger.Default.Register<CalendarSettingChangedMessage>(this, async (recipient, message) => await HandleCalendarSettingChanged());
        WeakReferenceMessenger.Default.Register<TasksReloadRequestedMessage>(this, async (recipient, message) => await HandleTasksReloadRequested());

        Tasks.CollectionChanged += Tasks_CollectionChanged;
    }

    [RelayCommand]
    private async Task NavigateToAddPage()
    {
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
        if (taskToEdit is null)
        {
            return;
        }

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
        bool acquiredLock = false;
        lock (saveLock)
        {
            if (isSaving) return;
            isSaving = true;
            acquiredLock = true;
        }

        if (!acquiredLock) return;


        List<TaskItem> loadedTasks = [];
        bool dueDateChangesMade = false;

        try
        {
            loadedTasks = await taskPersistenceService.LoadTasksAsync();
            dueDateChangesMade = UpdateRepeatingTaskDueDates(loadedTasks);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Tasks.Clear();
                List<TaskItem> tasksToAdd = (AppSettings.SelectedSortOrder == SortOrderType.Manual)
                    ? [.. loadedTasks.OrderBy(t => t.Order)]
                    : [.. loadedTasks];

                foreach (TaskItem task in tasksToAdd)
                {
                    Tasks.Add(task);
                }
                UpdateProgress(); // Update progress after loading
            });

            if (AppSettings.SelectedSortOrder != SortOrderType.Manual)
            {
                ApplySortOrderCommand.Execute(null);
            }
            else
            {
                UpdateSelectedSortOptionDisplay(SortOrderType.Manual);
            }


            if (dueDateChangesMade)
            {
                TriggerSave();
            }
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"LoadTasksAsync: Error processing loaded tasks: {exception.GetType().Name} - {exception.Message}");
            if (Tasks.Any())
            {
                await MainThread.InvokeOnMainThreadAsync(() => { Tasks.Clear(); UpdateProgress(); });
            }
        }
        finally
        {
            lock (saveLock)
            {
                isSaving = false;
            }
        }
    }


    [RelayCommand]
    private async Task MarkTaskDone(TaskItem? task)
    {
        if (task is null || task.IsFadingOut)
        {
            return;
        }

        if (task.TimeType == TaskTimeType.Repeating)
        {
            await HandleRepeatingTaskCompletion(task);
        }
        else
        {
            await HandleNonRepeatingTaskCompletion(task);
        }
    }

    [RelayCommand]
    private async Task ApplySortOrder()
    {
        SortOrderType sortToApply = SelectedSortOption switch
        {
            "Priority (High First)" => SortOrderType.PriorityHighFirst,
            "Priority (Low First)" => SortOrderType.PriorityLowFirst,
            _ => SortOrderType.Manual
        };


        if (sortToApply == SortOrderType.Manual)
        {
            await LoadTasksAsync();
            UpdateSelectedSortOptionDisplay(SortOrderType.Manual);
            return;
        }

        List<TaskItem> currentTasks = [];

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            currentTasks = [.. Tasks];
        });

        List<TaskItem> sortedTasks = sortToApply switch
        {
            SortOrderType.PriorityHighFirst => [.. currentTasks
                .OrderByDescending(IsTaskEnabled)
                .ThenBy(task => task.Priority)
                .ThenBy(task => GetSortableTitle(task.Title), StringComparer.OrdinalIgnoreCase)],
            SortOrderType.PriorityLowFirst => [.. currentTasks
                .OrderByDescending(IsTaskEnabled)
                .ThenByDescending(task => task.Priority)
                .ThenBy(task => GetSortableTitle(task.Title), StringComparer.OrdinalIgnoreCase)],
            _ => [.. currentTasks.OrderBy(task => task.Order)]
        };

        bool orderChanged = !currentTasks.SequenceEqual(sortedTasks);

        if (orderChanged)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Tasks.Clear();
                foreach (TaskItem task in sortedTasks)
                {
                    Tasks.Add(task);
                }
                // No need to call UpdateProgress here, CollectionChanged handles it
            });
        }

        UpdateSelectedSortOptionDisplay(sortToApply);
    }


    private async Task HandleNonRepeatingTaskCompletion(TaskItem task)
    {
        task.IsFadingOut = true;
        await Task.Delay(350);

        bool removedSuccessfully = false;
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            removedSuccessfully = Tasks.Remove(task);
            if (removedSuccessfully)
            {
                if (AppSettings.SelectedSortOrder == SortOrderType.Manual)
                {
                    UpdateTaskOrderPropertyInternal();
                }
                UpdateProgress(); // Update progress after removal
                TriggerSave();
            }
            else
            {
                task.IsFadingOut = false;
            }
        });

        if (removedSuccessfully && AppSettings.SelectedSortOrder != SortOrderType.Manual)
        {
            ApplySortOrderCommand.Execute(null);
        }
    }

    private async Task HandleRepeatingTaskCompletion(TaskItem task)
    {
        DateTime? nextDueDate = DateUtils.CalculateNextDueDate(task);

        if (nextDueDate.HasValue)
        {
            task.DueDate = nextDueDate;
            UpdateProgress(); // Progress doesn't change, but call for consistency
            TriggerSave();

            if (AppSettings.SelectedSortOrder != SortOrderType.Manual)
            {
                ApplySortOrderCommand.Execute(null);
            }
            else
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    int index = Tasks.IndexOf(task);
                    if (index != -1)
                    {
                        // No action needed if TaskItem implements INotifyPropertyChanged
                    }
                });
            }
        }
        else
        {
            await HandleNonRepeatingTaskCompletion(task);
        }
    }

    private bool UpdateRepeatingTaskDueDates(List<TaskItem> tasks)
    {
        bool changesMade = false;
        DateTime today = DateTime.Today;

        foreach (TaskItem task in tasks)
        {
            if (task.TimeType == TaskTimeType.Repeating && task.DueDate.HasValue && task.DueDate.Value.Date < today)
            {
                DateTime originalDueDate = task.DueDate.Value.Date;
                DateTime nextValidDueDate = CalculateNextValidDueDateForRepeatingTask(task, today, originalDueDate);

                if (task.DueDate.Value.Date != nextValidDueDate)
                {
                    task.DueDate = nextValidDueDate;
                    changesMade = true;
                }
            }
            task.IsFadingOut = false;
        }
        return changesMade;
    }

    private void UpdateTaskOrderPropertyInternal()
    {
        for (int index = 0; index < Tasks.Count; index++)
        {
            TaskItem? currentTask = Tasks[index];
            if (currentTask != null && currentTask.Order != index)
            {
                currentTask.Order = index;
            }
        }
    }

    private async Task SaveTasksAsyncInternal()
    {
        bool acquiredLock = false;
        List<TaskItem> tasksToSave = [];

        try
        {
            lock (saveLock)
            {
                if (isSaving) return;
                isSaving = true;
                acquiredLock = true;
            }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (AppSettings.SelectedSortOrder == SortOrderType.Manual)
                {
                    UpdateTaskOrderPropertyInternal();
                }
                tasksToSave = [.. Tasks];
            });

            await taskPersistenceService.SaveTasksAsync(tasksToSave);
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"SaveTasksAsyncInternal: Error saving tasks: {exception.Message}");
        }
        finally
        {
            if (acquiredLock)
            {
                lock (saveLock)
                {
                    isSaving = false;
                }
            }
        }
    }


    private void TriggerSave()
    {
        debounceTimer?.Dispose();

        debounceTimer = new Timer(async _ =>
        {
            await SaveTasksAsyncInternal();
            debounceTimer?.Dispose();
            debounceTimer = null;
        },
        null,
        TimeSpan.FromMilliseconds(500),
        Timeout.InfiniteTimeSpan);
    }

    private void ResetSortToManual()
    {
        UpdateSelectedSortOptionDisplay(SortOrderType.Manual);
    }

    private string GetSortOptionDisplayString(SortOrderType sortOrder)
    {
        return sortOrder switch
        {
            SortOrderType.PriorityHighFirst => SortOptionsDisplay[1],
            SortOrderType.PriorityLowFirst => SortOptionsDisplay[2],
            _ => SortOptionsDisplay[0]
        };
    }

    private void UpdateSelectedSortOptionDisplay(SortOrderType sortOrder)
    {
        string newDisplayValue = GetSortOptionDisplayString(sortOrder);
        if (field != newDisplayValue)
        {
            field = newDisplayValue;
            OnPropertyChanged(nameof(SelectedSortOption));
        }
    }


    private async Task HandleCalendarSettingChanged()
    {
        await LoadTasksAsync();
    }

    private async Task HandleTasksReloadRequested()
    {
        await LoadTasksAsync();
    }

    private async void HandleAddTask(TaskItem? newTask)
    {
        if (newTask is null) return;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            newTask.Order = Tasks.Count;
            Tasks.Add(newTask);
            UpdateProgress(); // Update progress after adding

            if (AppSettings.SelectedSortOrder == SortOrderType.Manual)
            {
                UpdateTaskOrderPropertyInternal();
            }

            TriggerSave();

            if (AppSettings.SelectedSortOrder != SortOrderType.Manual)
            {
                ApplySortOrderCommand.Execute(null);
            }
        });
    }

    private async void HandleUpdateTask(TaskItem? updatedTask)
    {
        if (updatedTask is null) return;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
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
                if (AppSettings.SelectedSortOrder == SortOrderType.Manual)
                {
                    updatedTask.Order = Tasks[index].Order;
                }
                Tasks[index] = updatedTask;
                UpdateProgress(); // Update progress after update (count might not change, but good practice)
                TriggerSave();

                if (AppSettings.SelectedSortOrder != SortOrderType.Manual)
                {
                    ApplySortOrderCommand.Execute(null);
                }
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
            if (taskToRemove != null)
            {
                removedSuccessfully = Tasks.Remove(taskToRemove);
                if (removedSuccessfully)
                {
                    if (AppSettings.SelectedSortOrder == SortOrderType.Manual)
                    {
                        UpdateTaskOrderPropertyInternal();
                    }
                    UpdateProgress(); // Update progress after removal
                    TriggerSave();
                }
            }
        });

        if (removedSuccessfully && AppSettings.SelectedSortOrder != SortOrderType.Manual)
        {
            ApplySortOrderCommand.Execute(null);
        }
    }


    private void Tasks_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Update progress whenever the collection changes (Add, Remove, Move, Reset)
        // If multiple items change (e.g., Reset), this ensures progress updates.
        UpdateProgress();

        if (e.Action == NotifyCollectionChangedAction.Move)
        {
            UpdateTaskOrderPropertyInternal();
            TriggerSave();
            ResetSortToManual();
            AppSettings.SelectedSortOrder = SortOrderType.Manual;
        }
    }


    private static DateTime CalculateNextValidDueDateForRepeatingTask(TaskItem task, DateTime today, DateTime originalDueDate)
    {
        return task.RepetitionType switch
        {
            TaskRepetitionType.Daily => today,
            TaskRepetitionType.AlternateDay =>
                (today - originalDueDate).TotalDays % 2 == 0
                    ? today
                    : today.AddDays(1),
            TaskRepetitionType.Weekly when task.RepetitionDayOfWeek.HasValue =>
                DateUtils.GetNextWeekday(today, task.RepetitionDayOfWeek.Value),
            _ => GetNextWeeklyOccurrence(originalDueDate, today) ?? today
        };
    }

    private static DateTime? GetNextWeeklyOccurrence(DateTime startDate, DateTime today)
    {
        DateTime nextDate = startDate.Date;
        while (nextDate < today)
        {
            nextDate = nextDate.AddDays(7);
        }
        return nextDate;
    }

    private static string GetSortableTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.Empty;
        }

        int firstLetterIndex = -1;
        for (int i = 0; i < title.Length; i++)
        {
            if (char.IsLetterOrDigit(title[i]))
            {
                firstLetterIndex = i;
                break;
            }
        }

        return (firstLetterIndex == -1)
            ? title.Trim()
            : title.Substring(firstLetterIndex).Trim();
    }

    private static bool IsTaskEnabled(TaskItem task)
    {
        bool isRepeating = task.TimeType == TaskTimeType.Repeating;
        bool hasDueDate = task.DueDate.HasValue;
        bool isDueInFuture = hasDueDate && task.DueDate.Value.Date > DateTime.Today;

        return !isRepeating || !hasDueDate || !isDueInFuture;
    }

    private void UpdateProgress()
    {
        // Calculate progress: 1.0 when 0 tasks, 0.0 when TargetTaskCount or more tasks.
        double progressValue = 1.0 - ((double)Tasks.Count / TargetTaskCount);
        TaskCompletionProgress = Math.Clamp(progressValue, 0.0, 1.0);
        // Debug.WriteLine($"Progress Updated: {TaskCompletionProgress} ({Tasks.Count} tasks)");
    }
}