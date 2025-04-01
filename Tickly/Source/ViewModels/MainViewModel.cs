// File: ViewModels/MainViewModel.cs
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
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Tickly.Messages;
using Tickly.Models;
using Tickly.Views;
using Tickly.Utils;
using System.Threading;

namespace Tickly.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<TaskItem> _tasks;

    private readonly string _filePath;
    private bool _isSaving = false;
    private readonly object _saveLock = new();

    private SortOrderType _currentSortOrder = SortOrderType.Manual;

    [ObservableProperty]
    private List<string> _sortOptionsDisplay;

    private string _selectedSortOption;
    public string SelectedSortOption
    {
        get => _selectedSortOption;
        set
        {
            if (SetProperty(ref _selectedSortOption, value))
            {
                ApplySortOrderCommand.Execute(null);
            }
        }
    }


    public MainViewModel()
    {
        _filePath = Path.Combine(FileSystem.AppDataDirectory, "tasks.json");
        _tasks = new();

        SortOptionsDisplay =
        [
            "Manual Order",
            "Priority (High First)",
            "Priority (Low First)"
        ];
        _selectedSortOption = SortOptionsDisplay[0];

        _ = LoadTasksAsync();

        WeakReferenceMessenger.Default.Register<AddTaskMessage>(this, (r, m) => HandleAddTask(m.Value));
        WeakReferenceMessenger.Default.Register<UpdateTaskMessage>(this, (r, m) => HandleUpdateTask(m.Value));
        WeakReferenceMessenger.Default.Register<DeleteTaskMessage>(this, (r, m) => HandleDeleteTask(m.Value));
        WeakReferenceMessenger.Default.Register<CalendarSettingChangedMessage>(this, (r, m) => HandleCalendarSettingChanged());
        WeakReferenceMessenger.Default.Register<TasksReloadRequestedMessage>(this, async (r, m) => await HandleTasksReloadRequested());
    }


    [RelayCommand]
    private async Task NavigateToAddPage()
    {
        try
        {
            await Shell.Current.GoToAsync(nameof(AddTaskPopupPage), true, new Dictionary<string, object> { { "TaskToEdit", null! } });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error navigating to add page: {ex.Message}");
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
            var navigationParameter = new Dictionary<string, object> { { "TaskToEdit", taskToEdit } };
            await Shell.Current.GoToAsync(nameof(AddTaskPopupPage), true, navigationParameter);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error navigating to edit page for task {taskToEdit.Id}: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task LoadTasksAsync()
    {
        lock (_saveLock)
        {
            if (_isSaving)
            {
                return;
            }
        }
        Debug.WriteLine($"LoadTasksAsync: Attempting to load tasks from: {_filePath}");
        bool wasSubscribed = false;
        try
        {
            Tasks.CollectionChanged -= Tasks_CollectionChanged;
            wasSubscribed = true;
        }
        catch
        { }

        bool changesMade = false;

        try
        {
            List<TaskItem> loadedTasks = [];
            if (File.Exists(_filePath))
            {
                string json = await File.ReadAllTextAsync(_filePath);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    try
                    {
                        loadedTasks = JsonSerializer.Deserialize<List<TaskItem>>(json) ?? [];
                    }
                    catch (JsonException jsonEx)
                    {
                        Debug.WriteLine($"LoadTasksAsync: Error deserializing tasks JSON: {jsonEx.Message}");
                    }
                }
            }

            DateTime today = DateTime.Today;
            foreach (var task in loadedTasks)
            {
                if (task.TimeType == TaskTimeType.Repeating && task.DueDate.HasValue && task.DueDate.Value.Date < today)
                {
                    DateTime originalDueDate = task.DueDate.Value.Date;
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
                                if (daysDifference % 2 == 0)
                                {
                                    nextValidDueDate = today;
                                }
                                else
                                {
                                    nextValidDueDate = today.AddDays(1);
                                }
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

                    if (task.DueDate.Value.Date != nextValidDueDate)
                    {
                        Debug.WriteLine($"LoadTasksAsync: Adjusting overdue task '{task.Title}' ({task.RepetitionType}) from {task.DueDate.Value.Date:d} to {nextValidDueDate:d}");
                        task.DueDate = nextValidDueDate;
                        changesMade = true;
                    }
                }
            }

            var tasksToAdd = loadedTasks.OrderBy(t => t.Order).ToList();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Tasks.Clear();
                foreach (var task in tasksToAdd) { task.IsFadingOut = false; Tasks.Add(task); }
                UpdateTaskOrderProperty();
            });

            _currentSortOrder = SortOrderType.Manual;
            UpdateSelectedSortOptionDisplay(SortOrderType.Manual);

            if (changesMade)
            {
                Debug.WriteLine("LoadTasksAsync: Saving tasks due to date adjustments.");
                await TriggerSave();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"LoadTasksAsync: Error loading tasks: {ex.GetType().Name} - {ex.Message}");
            if (Tasks.Any())
            {
                await MainThread.InvokeOnMainThreadAsync(Tasks.Clear);
            }
            _currentSortOrder = SortOrderType.Manual;
            UpdateSelectedSortOptionDisplay(SortOrderType.Manual);
        }
        finally
        {
            if (wasSubscribed || Tasks.Any())
            {
                Tasks.CollectionChanged -= Tasks_CollectionChanged;
                Tasks.CollectionChanged += Tasks_CollectionChanged;
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

        if (task.TimeType == TaskTimeType.None || task.TimeType == TaskTimeType.SpecificDate)
        {
            task.IsFadingOut = true;
            await Task.Delay(350);
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                if (Tasks.Remove(task))
                {
                    UpdateTaskOrderProperty();
                    await TriggerSave();
                }
                else
                {
                    task.IsFadingOut = false;
                }
            });
            ResetSortToManual();
        }
        else if (task.TimeType == TaskTimeType.Repeating)
        {
            DateTime? nextDueDate = DateUtils.CalculateNextDueDate(task);
            if (nextDueDate.HasValue)
            {
                task.DueDate = nextDueDate;

                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    int index = Tasks.IndexOf(task);
                    if (index != -1)
                    {
                        UpdateTaskOrderProperty();
                        await TriggerSave();
                    }
                    else
                    {
                        if (Tasks.Remove(task))
                        {
                            Tasks.Add(task);
                            UpdateTaskOrderProperty();
                            await TriggerSave();
                        }
                    }
                });

                if (_currentSortOrder != SortOrderType.Manual)
                {
                    ApplySortOrderCommand.Execute(null);
                }
            }
            else
            {
                Debug.WriteLine($"MarkTaskDone: Could not calculate next due date for repeating task '{task.Title}'. Removing.");
                task.IsFadingOut = true;
                await Task.Delay(350);
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    if (Tasks.Remove(task))
                    {
                        UpdateTaskOrderProperty();
                        await TriggerSave();
                    }
                    else
                    {
                        task.IsFadingOut = false;
                    }
                });
                ResetSortToManual();
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


    [RelayCommand]
    private async Task ApplySortOrder()
    {
        SortOrderType requestedSortOrder = SelectedSortOption switch
        {
            "Priority (High First)" => SortOrderType.PriorityHighFirst,
            "Priority (Low First)" => SortOrderType.PriorityLowFirst,
            _ => SortOrderType.Manual
        };

        if (requestedSortOrder == SortOrderType.Manual && _currentSortOrder != SortOrderType.Manual)
        {
            await LoadTasksAsync();
            return;
        }

        if (requestedSortOrder == _currentSortOrder && requestedSortOrder != SortOrderType.Manual)
        {
            return;
        }


        List<TaskItem> currentTasks = [];
        await MainThread.InvokeOnMainThreadAsync(() => { currentTasks = new List<TaskItem>(Tasks); });

        List<TaskItem> sortedTasks;
        if (requestedSortOrder == SortOrderType.PriorityHighFirst)
        {
            sortedTasks = currentTasks.OrderBy(t => t.Priority)
                                      .ThenBy(t => GetSortableTitle(t.Title), StringComparer.OrdinalIgnoreCase)
                                      .ToList();
        }
        else if (requestedSortOrder == SortOrderType.PriorityLowFirst)
        {
            sortedTasks = currentTasks.OrderByDescending(t => t.Priority)
                                      .ThenBy(t => GetSortableTitle(t.Title), StringComparer.OrdinalIgnoreCase)
                                      .ToList();
        }
        else
        {
            return;
        }


        if (currentTasks.SequenceEqual(sortedTasks))
        {
            _currentSortOrder = requestedSortOrder;
            UpdateSelectedSortOptionDisplay(requestedSortOrder);
            return;
        }


        Tasks.CollectionChanged -= Tasks_CollectionChanged;
        try
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Tasks.Clear();
                foreach (var task in sortedTasks)
                {
                    Tasks.Add(task);
                }
            });
            _currentSortOrder = requestedSortOrder;
            UpdateSelectedSortOptionDisplay(requestedSortOrder);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ApplySortOrder: Error during sorting: {ex.Message}");
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

    private async void HandleCalendarSettingChanged()
    {
        await LoadTasksAsync();
    }

    private async void HandleAddTask(TaskItem? newTask)
    {
        if (newTask is null)
        {
            return;
        }
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            newTask.Order = Tasks.Count;
            Tasks.Add(newTask);
            await TriggerSave();
        });
        ResetSortToManual();
    }

    private async void HandleUpdateTask(TaskItem? updatedTask)
    {
        if (updatedTask is null)
        {
            return;
        }
        await MainThread.InvokeOnMainThreadAsync(async () =>
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
                updatedTask.Order = Tasks[index].Order;
                Tasks[index] = updatedTask;
                await TriggerSave();
            }
        });
        ResetSortToManual();
    }

    private async void HandleDeleteTask(Guid taskId)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var taskToRemove = Tasks.FirstOrDefault(t => t.Id == taskId);
            if (taskToRemove is not null)
            {
                if (Tasks.Remove(taskToRemove))
                {
                    UpdateTaskOrderProperty();
                    await TriggerSave();
                }
            }
        });
    }

    private async void Tasks_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Move)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                UpdateTaskOrderProperty();
                await TriggerSave();
            });
            ResetSortToManual();
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
        for (int i = 0; i < Tasks.Count; i++)
        {
            if (i < Tasks.Count && Tasks[i] is not null && Tasks[i].Order != i)
            {
                Tasks[i].Order = i;
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

            if (tasksToSave is not null)
            {
                tasksToSave = tasksToSave.OrderBy(t => t.Order).ToList();

                string json = JsonSerializer.Serialize(tasksToSave, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_filePath, json);
                Debug.WriteLine($"SaveTasks: Successfully saved {tasksToSave.Count} tasks to {_filePath}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SaveTasks: Error saving tasks: {ex.Message}");
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

    private Timer? _debounceTimer;
    private async Task TriggerSave()
    {
        _debounceTimer?.Dispose();

        _debounceTimer = new Timer(async (_) =>
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
        if (_currentSortOrder != SortOrderType.Manual)
        {
            _currentSortOrder = SortOrderType.Manual;
            UpdateSelectedSortOptionDisplay(SortOrderType.Manual);
        }
    }

    private void UpdateSelectedSortOptionDisplay(SortOrderType sortOrder)
    {
        string newDisplayValue = sortOrder switch
        {
            SortOrderType.PriorityHighFirst => SortOptionsDisplay[1],
            SortOrderType.PriorityLowFirst => SortOptionsDisplay[2],
            _ => SortOptionsDisplay[0]
        };
        if (_selectedSortOption != newDisplayValue)
        {
            SetProperty(ref _selectedSortOption, newDisplayValue, nameof(SelectedSortOption));
            Debug.WriteLine($"UpdateSelectedSortOptionDisplay: Picker set to '{newDisplayValue}'");
        }
    }

    private async Task HandleTasksReloadRequested()
    {
        Debug.WriteLine("MainViewModel: Received TasksReloadRequestedMessage. Reloading tasks...");
        await LoadTasksAsync();
    }
}