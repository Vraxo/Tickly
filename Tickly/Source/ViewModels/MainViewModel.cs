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
    private ObservableCollection<TaskItem> _tasks;
    public ObservableCollection<TaskItem> Tasks
    {
        get => _tasks;
        set => SetProperty(ref _tasks, value);
    }

    private readonly string _filePath;
    private bool _isSaving = false;
    private readonly object _saveLock = new();

    // Default sort order is now PriorityHighFirst
    private SortOrderType _currentSortOrder = SortOrderType.PriorityHighFirst;

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
            // Ensure SetProperty is called before executing the command
            // to update the backing field correctly.
            if (SetProperty(ref _selectedSortOption, value))
            {
                // Determine the new sort order based on the selected string
                SortOrderType newSortOrder = value switch
                {
                    "Priority (High First)" => SortOrderType.PriorityHighFirst,
                    "Priority (Low First)" => SortOrderType.PriorityLowFirst,
                    _ => SortOrderType.Manual
                };

                // Only execute if the underlying sort type changes or if triggering manually
                if (newSortOrder != _currentSortOrder || value == _selectedSortOption) // Allow re-triggering same sort
                {
                    ApplySortOrderCommand.Execute(null);
                }
            }
        }
    }


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
        // Default selected option is now PriorityHighFirst
        _selectedSortOption = _sortOptionsDisplay[1];

        _ = LoadTasksAsync();

        WeakReferenceMessenger.Default.Register<AddTaskMessage>(this, (r, m) => HandleAddTask(m.Value));
        WeakReferenceMessenger.Default.Register<UpdateTaskMessage>(this, (r, m) => HandleUpdateTask(m.Value));
        WeakReferenceMessenger.Default.Register<DeleteTaskMessage>(this, (r, m) => HandleDeleteTask(m.Value));
        WeakReferenceMessenger.Default.Register<CalendarSettingChangedMessage>(this, async (r, m) => await HandleCalendarSettingChanged());
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
        List<TaskItem> tasksToAdd = []; // Keep track of tasks loaded

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

            // Load based on saved order first
            tasksToAdd = loadedTasks.OrderBy(t => t.Order).ToList();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Tasks.Clear();
                foreach (var task in tasksToAdd) { task.IsFadingOut = false; Tasks.Add(task); }
                // Do not update Task.Order property here yet if applying sort below
            });

            // Apply the default or currently selected sort order AFTER loading
            // This ensures the initial view is sorted as expected.
            ApplySortOrderCommand.Execute(null); // This will use the current _selectedSortOption/ _currentSortOrder

            // UpdateSelectedSortOptionDisplay(_currentSortOrder); // Ensure Picker matches state

            if (changesMade)
            {
                Debug.WriteLine("LoadTasksAsync: Saving tasks due to date adjustments (after potential sort).");
                // Save potentially reordered list if dates changed and sort was applied
                TriggerSave();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"LoadTasksAsync: Error loading tasks: {ex.GetType().Name} - {ex.Message}");
            if (Tasks.Any())
            {
                await MainThread.InvokeOnMainThreadAsync(Tasks.Clear);
            }
            // Reset to default sort if loading failed
            _currentSortOrder = SortOrderType.PriorityHighFirst;
            UpdateSelectedSortOptionDisplay(_currentSortOrder);
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

        bool wasRepeating = task.TimeType == TaskTimeType.Repeating;

        if (task.TimeType == TaskTimeType.None || task.TimeType == TaskTimeType.SpecificDate)
        {
            task.IsFadingOut = true;
            await Task.Delay(350);
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (Tasks.Remove(task))
                {
                    UpdateTaskOrderProperty(); // Update order for remaining items
                    TriggerSave();
                }
                else
                {
                    task.IsFadingOut = false;
                }
            });
            // Re-apply sort if not manual, as removal affects order
            if (_currentSortOrder != SortOrderType.Manual)
            {
                ApplySortOrderCommand.Execute(null);
            }
        }
        else if (task.TimeType == TaskTimeType.Repeating)
        {
            DateTime? nextDueDate = DateUtils.CalculateNextDueDate(task);
            if (nextDueDate.HasValue)
            {
                task.DueDate = nextDueDate;
                TriggerSave(); // Save the date change

                if (_currentSortOrder != SortOrderType.Manual)
                {
                    ApplySortOrderCommand.Execute(null); // Re-sort based on new date/enabled state
                }
                else
                {
                    // If manual, force UI refresh for the item
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        int index = Tasks.IndexOf(task);
                        if (index != -1)
                        {
                            Tasks[index] = task; // Re-assignment to help UI update
                        }
                    });
                }
            }
            else // Error calculating next date, treat as removal
            {
                Debug.WriteLine($"MarkTaskDone: Could not calculate next due date for repeating task '{task.Title}'. Removing.");
                task.IsFadingOut = true;
                await Task.Delay(350);
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (Tasks.Remove(task))
                    {
                        UpdateTaskOrderProperty();
                        TriggerSave();
                    }
                    else
                    {
                        task.IsFadingOut = false;
                    }
                });
                // Re-apply sort if not manual after removal
                if (_currentSortOrder != SortOrderType.Manual)
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

        // If explicitly selecting Manual sort, reload based on saved order
        if (requestedSortOrder == SortOrderType.Manual && _currentSortOrder != SortOrderType.Manual)
        {
            // Only reload if changing *to* manual from something else
            _currentSortOrder = SortOrderType.Manual; // Update state first
            UpdateSelectedSortOptionDisplay(_currentSortOrder);
            await LoadTasksAsync(); // Reloads and preserves saved order
            return; // Stop further processing as LoadTasksAsync handles UI
        }
        else if (requestedSortOrder == SortOrderType.Manual)
        {
            // Already manual or selected manual again, do nothing extra, just update state
            _currentSortOrder = SortOrderType.Manual;
            UpdateSelectedSortOptionDisplay(_currentSortOrder);
            // Optionally, could still call LoadTasksAsync() to ensure it perfectly matches saved state
            // await LoadTasksAsync();
            return;
        }

        // Proceed with non-manual sorting logic

        List<TaskItem> currentTasks = [];
        await MainThread.InvokeOnMainThreadAsync(() => { currentTasks = new List<TaskItem>(Tasks); });

        List<TaskItem> sortedTasks;
        if (requestedSortOrder == SortOrderType.PriorityHighFirst)
        {
            sortedTasks = currentTasks.OrderByDescending(t => IsTaskEnabled(t))
                                      .ThenBy(t => t.Priority)
                                      .ThenBy(t => GetSortableTitle(t.Title), StringComparer.OrdinalIgnoreCase)
                                      .ToList();
        }
        else if (requestedSortOrder == SortOrderType.PriorityLowFirst)
        {
            sortedTasks = currentTasks.OrderByDescending(t => IsTaskEnabled(t))
                                      .ThenByDescending(t => t.Priority)
                                      .ThenBy(t => GetSortableTitle(t.Title), StringComparer.OrdinalIgnoreCase)
                                      .ToList();
        }
        else // Should have been handled by the Manual check above
        {
            return;
        }

        bool orderChanged = !currentTasks.SequenceEqual(sortedTasks);

        if (orderChanged)
        {
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
                    // Do NOT update Task.Order when applying a temporary sort
                });
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

        _currentSortOrder = requestedSortOrder;
        UpdateSelectedSortOptionDisplay(_currentSortOrder); // Ensure picker matches
        // Do NOT save here, non-manual sorts are view state only
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
            // Assign order based on current count BEFORE adding for manual saving
            newTask.Order = Tasks.Count;
            Tasks.Add(newTask);
            TriggerSave(); // Save the new task and its initial manual order

            // If not in manual sort, re-apply the current sort to include the new item correctly
            if (_currentSortOrder != SortOrderType.Manual)
            {
                ApplySortOrderCommand.Execute(null);
            }
            else
            {
                // If manual, the new item is just added at the end, which is fine
                // UpdateTaskOrderProperty(); // Ensure Task.Order is consistent if needed, but save handles it
            }
        });
        // ResetSortToManual() is removed - either re-apply sort or stay manual
    }

    private async void HandleUpdateTask(TaskItem? updatedTask)
    {
        if (updatedTask is null)
        {
            return;
        }
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
                // Preserve the original manual order when updating
                updatedTask.Order = Tasks[index].Order;
                Tasks[index] = updatedTask; // Replace in collection
                TriggerSave(); // Save the updated task data

                // If not manual sort, re-apply to position correctly
                if (_currentSortOrder != SortOrderType.Manual)
                {
                    ApplySortOrderCommand.Execute(null);
                }
            }
            else // Task not found, should not happen in edit mode
            {
                Debug.WriteLine($"HandleUpdateTask: Task with ID {updatedTask.Id} not found.");
                // Optionally reset sort or reload if this state is problematic
                // ResetSortToManual();
            }
        });
    }

    private async void HandleDeleteTask(Guid taskId)
    {
        TaskItem? taskToRemove = null;
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            taskToRemove = Tasks.FirstOrDefault(t => t.Id == taskId);
            if (taskToRemove is not null)
            {
                if (Tasks.Remove(taskToRemove))
                {
                    UpdateTaskOrderProperty(); // Update orders for remaining items
                    TriggerSave();
                }
            }
        });

        // If a task was successfully removed and we are in a sorted view, re-apply the sort
        if (taskToRemove is not null && _currentSortOrder != SortOrderType.Manual)
        {
            ApplySortOrderCommand.Execute(null);
        }
    }

    private async void Tasks_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Only handle manual reordering (Move action)
        if (e.Action == NotifyCollectionChangedAction.Move)
        {
            // Update internal Order property based on new visual position
            UpdateTaskOrderProperty();
            // Save the new manual order
            TriggerSave();
            // Switch view state to Manual Order
            ResetSortToManual();
        }
        // Add/Remove/Replace are handled within their respective message handlers or MarkTaskDone
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
        // This should only be called when the user manually reorders or when saving manual order
        for (int i = 0; i < Tasks.Count; i++)
        {
            if (i < Tasks.Count && Tasks[i] != null && Tasks[i].Order != i)
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

            // Ensure Task.Order properties are up-to-date based on the *current* visual order IF in Manual mode
            // If not in manual mode, the saved order should reflect the last known manual order.
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (_currentSortOrder == SortOrderType.Manual)
                {
                    UpdateTaskOrderPropertyInternal(); // Update Order based on current arrangement
                }
                // Create a copy of the current items for saving
                // Their Order property holds the correct manual sequence
                tasksToSave = new List<TaskItem>(Tasks);
            });


            if (tasksToSave is not null)
            {
                // Always sort by the Order property before serializing to preserve manual order
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
    private void TriggerSave()
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

    // Called when user manually reorders (via CollectionChanged)
    private void ResetSortToManual()
    {
        if (_currentSortOrder != SortOrderType.Manual)
        {
            _currentSortOrder = SortOrderType.Manual;
            UpdateSelectedSortOptionDisplay(SortOrderType.Manual);
        }
        // No reload needed here, the visual order IS the new manual order
    }

    private void UpdateSelectedSortOptionDisplay(SortOrderType sortOrder)
    {
        string newDisplayValue = sortOrder switch
        {
            SortOrderType.PriorityHighFirst => SortOptionsDisplay[1],
            SortOrderType.PriorityLowFirst => SortOptionsDisplay[2],
            _ => SortOptionsDisplay[0] // Manual Order
        };

        // Use SetProperty to update the picker's SelectedItem binding
        // This ensures the change originates from the ViewModel state
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