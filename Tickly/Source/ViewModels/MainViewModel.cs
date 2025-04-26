// File: Source\ViewModels\MainViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics; // Needed for Color
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
using Tickly.Services; // Needed for AppSettings
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

    private List<string> _sortOptionsDisplay;
    public List<string> SortOptionsDisplay
    {
        get => _sortOptionsDisplay;
        set => SetProperty(ref _sortOptionsDisplay, value);
    }

    private string _selectedSortOption;
    public string SelectedSortOption
    {
        get => _selectedSortOption;

        set
        {
            if (SetProperty(ref _selectedSortOption, value))
            {
                SortOrderType newSortOrder = value switch
                {
                    "Priority (High First)" => SortOrderType.PriorityHighFirst,
                    "Priority (Low First)" => SortOrderType.PriorityLowFirst,
                    _ => SortOrderType.Manual
                };

                if (newSortOrder != SortOrderType.Manual)
                {
                    AppSettings.SelectedSortOrder = newSortOrder;
                }

                ApplySortOrderCommand.Execute(null);
            }
        }
    }

    [ObservableProperty]
    private double taskProgress;

    [ObservableProperty]
    private Color taskProgressColor;

    private const double ReferenceTaskCount = 10.0; // Adjusted: Max tasks for 'full red'. Fewer tasks make it greener faster.

    public MainViewModel()
    {
        _filePath = Path.Combine(FileSystem.AppDataDirectory, "tasks.json");
        _tasks = new();

        _sortOptionsDisplay =
        [
            "Manual Order",
            "Priority (High First)",
            "Priority (Low First)"
        ];

        _selectedSortOption = GetSortOptionDisplayString(AppSettings.SelectedSortOrder);

        _ = LoadTasksAsync(); // LoadTasksAsync will call UpdateTaskProgressAndColor

        WeakReferenceMessenger.Default.Register<AddTaskMessage>(this, (recipient, message) => HandleAddTask(message.Value));
        WeakReferenceMessenger.Default.Register<UpdateTaskMessage>(this, (recipient, message) => HandleUpdateTask(message.Value));
        WeakReferenceMessenger.Default.Register<DeleteTaskMessage>(this, (recipient, message) => HandleDeleteTask(message.Value));
        WeakReferenceMessenger.Default.Register<CalendarSettingChangedMessage>(this, async (recipient, message) => await HandleCalendarSettingChanged());
        WeakReferenceMessenger.Default.Register<TasksReloadRequestedMessage>(this, async (recipient, message) => await HandleTasksReloadRequested());

        UpdateTaskProgressAndColor();
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

        lock (_saveLock)
        {
            if (_isSaving)
            {
                return;
            }
            acquiredLock = true;
        }

        if (!acquiredLock)
        {
            return;
        }

        Debug.WriteLine($"LoadTasksAsync: Attempting to load tasks from: {_filePath}");

        bool wasSubscribed = false;
        bool changesMade = false;
        List<TaskItem> loadedTasks = [];

        try
        {
            if (Tasks != null)
            {
                Tasks.CollectionChanged -= Tasks_CollectionChanged;
                wasSubscribed = true;
            }

            if (File.Exists(_filePath))
            {
                string json = await File.ReadAllTextAsync(_filePath);

                if (!string.IsNullOrWhiteSpace(json))
                {
                    try
                    {
                        loadedTasks = JsonSerializer.Deserialize<List<TaskItem>>(json) ?? [];
                    }
                    catch (JsonException jsonException)
                    {
                        Debug.WriteLine($"LoadTasksAsync: Error deserializing tasks JSON: {jsonException.Message}");
                        loadedTasks = [];
                    }
                }
            }

            DateTime today = DateTime.Today;
            List<TaskItem> tasksToRemove = [];

            foreach (TaskItem task in loadedTasks)
            {
                if (task.TimeType == TaskTimeType.Repeating && task.DueDate.HasValue && task.DueDate.Value.Date < today)
                {
                    DateTime originalDueDate = task.DueDate.Value.Date;
                    DateTime nextValidDueDate = CalculateNextValidDueDateForRepeatingTask(task, today, originalDueDate);

                    if (task.DueDate.Value.Date != nextValidDueDate)
                    {
                        Debug.WriteLine($"LoadTasksAsync: Adjusting overdue task '{task.Title}' ({task.RepetitionType}) from {task.DueDate.Value.Date:d} to {nextValidDueDate:d}");
                        task.DueDate = nextValidDueDate;
                        changesMade = true;
                    }
                }
                task.IsFadingOut = false;
            }

            List<TaskItem> tasksToAdd = loadedTasks.OrderBy(task => task.Order).ToList();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Tasks.Clear();
                foreach (TaskItem task in tasksToAdd)
                {
                    Tasks.Add(task);
                }
                UpdateTaskProgressAndColor();
            });

            if (AppSettings.SelectedSortOrder != SortOrderType.Manual)
            {
                ApplySortOrderCommand.Execute(null);
            }
            else
            {
                UpdateSelectedSortOptionDisplay(SortOrderType.Manual);
            }

            if (changesMade)
            {
                Debug.WriteLine("LoadTasksAsync: Saving tasks due to date adjustments.");
                TriggerSave();
            }
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"LoadTasksAsync: Error loading tasks: {exception.GetType().Name} - {exception.Message}");
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Tasks.Clear();
                UpdateTaskProgressAndColor();
            });
            AppSettings.SelectedSortOrder = SortOrderType.PriorityHighFirst;
            UpdateSelectedSortOptionDisplay(AppSettings.SelectedSortOrder);
        }
        finally
        {
            if (Tasks != null && (wasSubscribed || Tasks.Any()))
            {
                Tasks.CollectionChanged -= Tasks_CollectionChanged;
                Tasks.CollectionChanged += Tasks_CollectionChanged;
            }
            else if (Tasks != null)
            {
                Tasks.CollectionChanged += Tasks_CollectionChanged;
            }

            if (acquiredLock)
            {
                lock (_saveLock)
                {

                }
            }
        }
    }


    private static DateTime CalculateNextValidDueDateForRepeatingTask(TaskItem task, DateTime today, DateTime originalDueDate)
    {
        DateTime nextValidDueDate = originalDueDate;

        switch (task.RepetitionType)
        {
            case TaskRepetitionType.Daily:
                {
                    nextValidDueDate = today;
                    break;
                }
            case TaskRepetitionType.AlternateDay:
                {
                    double daysDifference = (today - originalDueDate).TotalDays;

                    nextValidDueDate = daysDifference % 2 == 0
                        ? today
                        : today.AddDays(1);
                    break;
                }
            case TaskRepetitionType.Weekly:
                {
                    if (task.RepetitionDayOfWeek.HasValue)
                    {
                        nextValidDueDate = DateUtils.GetNextWeekday(today, task.RepetitionDayOfWeek.Value);
                    }
                    else
                    {
                        while (nextValidDueDate < today)
                        {
                            nextValidDueDate = nextValidDueDate.AddDays(7);
                        }
                    }
                    break;
                }
        }
        return nextValidDueDate;
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
                if (removedSuccessfully)
                {
                    UpdateTaskOrderProperty();
                    UpdateTaskProgressAndColor();
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
        else if (task.TimeType == TaskTimeType.Repeating)
        {
            DateTime? nextDueDate = DateUtils.CalculateNextDueDate(task);

            if (nextDueDate.HasValue)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    task.DueDate = nextDueDate;
                    int index = Tasks.IndexOf(task);
                    if (index != -1)
                    {
                        Tasks[index] = task;
                    }
                    UpdateTaskProgressAndColor();
                    TriggerSave();
                });


                if (AppSettings.SelectedSortOrder != SortOrderType.Manual)
                {
                    ApplySortOrderCommand.Execute(null);
                }
            }
            else
            {
                Debug.WriteLine($"MarkTaskDone: Could not calculate next due date for repeating task '{task.Title}'. Removing.");
                task.IsFadingOut = true;
                await Task.Delay(350);

                bool removedSuccessfully = false;
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    removedSuccessfully = Tasks.Remove(task);
                    if (removedSuccessfully)
                    {
                        UpdateTaskOrderProperty();
                        UpdateTaskProgressAndColor();
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
        }
    }


    private static string GetSortableTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.Empty;
        }

        int firstLetterIndex = -1;

        for (int index = 0; index < title.Length; index++)
        {
            if (char.IsLetterOrDigit(title[index]))
            {
                firstLetterIndex = index;
                break;
            }
        }

        return firstLetterIndex == -1
            ? title.Trim()
            : title.Substring(firstLetterIndex).Trim();
    }

    private static bool IsTaskEnabled(TaskItem task)
    {
        if (task.TimeType == TaskTimeType.Repeating && task.DueDate.HasValue && task.DueDate.Value.Date > DateTime.Today)
        {
            return false;
        }
        return true;
    }

    [RelayCommand]
    private async Task ApplySortOrder()
    {
        SortOrderType requestedSortOrder = SelectedSortOption switch
        {
            "Priority (High First)" => SortOrderType.PriorityHighFirst,
            "Priority (Low First)" => SortOrderType.PriorityLowFirst,
            _ => SortOrderType.Manual
        };

        SortOrderType sortToApply = requestedSortOrder == SortOrderType.Manual
            ? SortOrderType.Manual
            : AppSettings.SelectedSortOrder;


        if (sortToApply == SortOrderType.Manual)
        {
            await LoadTasksAsync();
            UpdateSelectedSortOptionDisplay(SortOrderType.Manual);
            return;
        }

        List<TaskItem> currentTasks = [];
        await MainThread.InvokeOnMainThreadAsync(() => { currentTasks = new List<TaskItem>(Tasks); });

        List<TaskItem> sortedTasks = sortToApply switch
        {
            SortOrderType.PriorityHighFirst => currentTasks
                .OrderByDescending(IsTaskEnabled) // Keep disabled tasks at the bottom
                .ThenBy(task => task.Priority)    // Sort enabled by priority H->L
                .ThenBy(task => GetSortableTitle(task.Title), StringComparer.OrdinalIgnoreCase)
                .ToList(),
            SortOrderType.PriorityLowFirst => currentTasks
                .OrderByDescending(IsTaskEnabled) // Keep disabled tasks at the bottom
                .ThenByDescending(task => task.Priority) // Sort enabled by priority L->H
                .ThenBy(task => GetSortableTitle(task.Title), StringComparer.OrdinalIgnoreCase)
                .ToList(),
            _ => currentTasks.OrderBy(task => task.Order).ToList() // Manual uses saved Order
        };

        bool orderChanged = !currentTasks.SequenceEqual(sortedTasks);

        if (orderChanged)
        {
            Tasks.CollectionChanged -= Tasks_CollectionChanged;
            try
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    Tasks.Clear();
                    foreach (TaskItem task in sortedTasks)
                    {
                        Tasks.Add(task);
                    }
                    UpdateTaskProgressAndColor();
                });
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"ApplySortOrder: Error during sorting: {exception.Message}");
            }
            finally
            {
                if (Tasks.Any())
                {
                    Tasks.CollectionChanged -= Tasks_CollectionChanged;
                    Tasks.CollectionChanged += Tasks_CollectionChanged;
                }
            }
        }

        UpdateSelectedSortOptionDisplay(sortToApply);
    }


    private async Task HandleCalendarSettingChanged()
    {
        await LoadTasksAsync();
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
            UpdateTaskProgressAndColor();
            TriggerSave();

            if (AppSettings.SelectedSortOrder != SortOrderType.Manual)
            {
                ApplySortOrderCommand.Execute(null);
            }
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
                Tasks[index] = updatedTask;
                UpdateTaskProgressAndColor();
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
            if (taskToRemove is not null)
            {
                removedSuccessfully = Tasks.Remove(taskToRemove);
                if (removedSuccessfully)
                {
                    UpdateTaskOrderProperty();
                    UpdateTaskProgressAndColor();
                    TriggerSave();
                }
            }
        });

        if (removedSuccessfully && AppSettings.SelectedSortOrder != SortOrderType.Manual)
        {
            ApplySortOrderCommand.Execute(null);
        }
    }

    private void Tasks_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs eventArgs)
    {
        UpdateTaskProgressAndColor();

        if (eventArgs.Action == NotifyCollectionChangedAction.Move)
        {
            UpdateTaskOrderProperty();
            TriggerSave();
            ResetSortToManual();
        }
        else if (eventArgs.Action == NotifyCollectionChangedAction.Add || eventArgs.Action == NotifyCollectionChangedAction.Remove)
        {
            if (eventArgs.Action == NotifyCollectionChangedAction.Remove)
            {
                UpdateTaskOrderProperty();
            }
        }
    }


    private void UpdateTaskOrderProperty()
    {
        if (!MainThread.IsMainThread)
        {
            MainThread.BeginInvokeOnMainThread(UpdateTaskOrderPropertyInternal);
        }
        else
        {
            UpdateTaskOrderPropertyInternal();
        }
    }

    private void UpdateTaskOrderPropertyInternal()
    {
        for (int index = 0; index < Tasks.Count; index++)
        {
            TaskItem currentTask = Tasks[index];
            if (currentTask is not null && currentTask.Order != index)
            {
                currentTask.Order = index;
            }
        }
    }

    private async Task SaveTasks()
    {
        bool acquiredLock = false;
        List<TaskItem> tasksToSave = [];

        try
        {
            lock (_saveLock)
            {
                if (_isSaving)
                {
                    Debug.WriteLine("SaveTasks: Save already in progress. Skipping.");
                    return;
                }
                _isSaving = true;
                acquiredLock = true;
            }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                UpdateTaskOrderPropertyInternal();
                tasksToSave = new List<TaskItem>(Tasks);
            });


            if (tasksToSave is not null && tasksToSave.Count > 0)
            {
                JsonSerializerOptions options = new() { WriteIndented = true };
                string json = JsonSerializer.Serialize(tasksToSave, options);
                await File.WriteAllTextAsync(_filePath, json);
                Debug.WriteLine($"SaveTasks: Successfully saved {tasksToSave.Count} tasks to {_filePath}");
            }
            else if (tasksToSave is not null && tasksToSave.Count == 0)
            {
                if (File.Exists(_filePath))
                {
                    File.Delete(_filePath);
                    Debug.WriteLine($"SaveTasks: Deleted tasks file as the list is empty: {_filePath}");
                }
                else
                {
                    Debug.WriteLine($"SaveTasks: Task list is empty, no file to save or delete at {_filePath}");
                }
            }
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"SaveTasks: Error saving tasks: {exception.Message}");
        }
        finally
        {
            if (acquiredLock)
            {
                lock (_saveLock)
                {
                    _isSaving = false;
                }
            }
        }
    }

    private void TriggerSave()
    {
        _debounceTimer?.Dispose();

        _debounceTimer = new Timer(async (state) =>
        {
            await SaveTasks();
            _debounceTimer?.Dispose();
            _debounceTimer = null;
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

        if (_selectedSortOption != newDisplayValue)
        {
            SetProperty(ref _selectedSortOption, newDisplayValue, nameof(SelectedSortOption));
            Debug.WriteLine($"UpdateSelectedSortOptionDisplay: Picker display updated to '{newDisplayValue}'");
        }
    }

    private async Task HandleTasksReloadRequested()
    {
        Debug.WriteLine("MainViewModel: Received TasksReloadRequestedMessage. Reloading tasks...");
        await LoadTasksAsync();
    }

    private void UpdateTaskProgressAndColor()
    {
        // Count only enabled tasks
        double enabledTaskCount = Tasks?.Count(IsTaskEnabled) ?? 0;

        // Calculate progress based on enabled tasks
        double progressValue = ReferenceTaskCount <= 0 ? (enabledTaskCount > 0 ? 0.0 : 1.0)
                         : Math.Clamp(1.0 - (enabledTaskCount / ReferenceTaskCount), 0.0, 1.0);

        TaskProgress = progressValue;

        // Interpolate color from Red (0.0) to Green (1.0)
        float redComponent = (float)(1.0 - progressValue);
        float greenComponent = (float)progressValue;
        float blueComponent = 0.0f;

        redComponent = Math.Clamp(redComponent, 0.0f, 1.0f);
        greenComponent = Math.Clamp(greenComponent, 0.0f, 1.0f);

        TaskProgressColor = new Color(redComponent, greenComponent, blueComponent);

        Debug.WriteLine($"UpdateTaskProgressAndColor: EnabledCount={enabledTaskCount}, TotalCount={Tasks?.Count ?? 0}, Progress={TaskProgress}, Color={TaskProgressColor}");
    }
}