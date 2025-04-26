using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
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

    // Remove local _currentSortOrder, use AppSettings directly
    // private SortOrderType _currentSortOrder = SortOrderType.PriorityHighFirst;

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

                // Update AppSettings only if it's not 'Manual'
                // 'Manual' state is implicit when reordering, not a persisted preference.
                if (newSortOrder != SortOrderType.Manual)
                {
                    AppSettings.SelectedSortOrder = newSortOrder;
                }

                // Trigger the sort if the *runtime* sort order needs to change
                // (e.g., switching from Manual to Priority, or Priority High to Low)
                // Or if the value is being set initially or re-selected.
                ApplySortOrderCommand.Execute(null);
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

        // Initialize SelectedSortOption based on loaded preference
        _selectedSortOption = GetSortOptionDisplayString(AppSettings.SelectedSortOrder);

        _ = LoadTasksAsync();

        WeakReferenceMessenger.Default.Register<AddTaskMessage>(this, (recipient, message) => HandleAddTask(message.Value));
        WeakReferenceMessenger.Default.Register<UpdateTaskMessage>(this, (recipient, message) => HandleUpdateTask(message.Value));
        WeakReferenceMessenger.Default.Register<DeleteTaskMessage>(this, (recipient, message) => HandleDeleteTask(message.Value));
        WeakReferenceMessenger.Default.Register<CalendarSettingChangedMessage>(this, async (recipient, message) => await HandleCalendarSettingChanged());
        WeakReferenceMessenger.Default.Register<TasksReloadRequestedMessage>(this, async (recipient, message) => await HandleTasksReloadRequested());
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

        try
        {
            Tasks.CollectionChanged -= Tasks_CollectionChanged;
            wasSubscribed = true;
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"LoadTasksAsync: Error unsubscribing CollectionChanged: {exception.Message}");
        }

        List<TaskItem> loadedTasks = [];

        try
        {
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

            // Apply manual sort order initially from loaded data
            List<TaskItem> tasksToAdd = loadedTasks.OrderBy(task => task.Order).ToList();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Tasks.Clear();
                foreach (TaskItem task in tasksToAdd)
                {
                    Tasks.Add(task);
                }
            });

            // Now apply the *persisted* sort order if it's not manual
            if (AppSettings.SelectedSortOrder != SortOrderType.Manual)
            {
                ApplySortOrderCommand.Execute(null); // This will read from AppSettings
            }
            else
            {
                // Ensure UI reflects the loaded manual sort state
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

            if (Tasks.Any())
            {
                await MainThread.InvokeOnMainThreadAsync(Tasks.Clear);
            }

            // Reset to default sort if loading failed badly
            AppSettings.SelectedSortOrder = SortOrderType.PriorityHighFirst;
            UpdateSelectedSortOptionDisplay(AppSettings.SelectedSortOrder);
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
                bool removed = Tasks.Remove(task);
                if (removed)
                {
                    UpdateTaskOrderProperty();
                    TriggerSave();
                    removedSuccessfully = true;
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
                task.DueDate = nextDueDate;
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
                            Tasks[index] = task;
                        }
                    });
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
                    bool removed = Tasks.Remove(task);
                    if (removed)
                    {
                        UpdateTaskOrderProperty();
                        TriggerSave();
                        removedSuccessfully = true;
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

        // Determine the sort order to apply. Use persisted setting if not Manual.
        SortOrderType sortToApply = requestedSortOrder == SortOrderType.Manual
            ? SortOrderType.Manual // Special case: if user selects Manual, reload from saved order.
            : AppSettings.SelectedSortOrder; // Otherwise, use the (potentially just updated) setting.


        if (sortToApply == SortOrderType.Manual)
        {
            // If switching TO manual or re-selecting manual, load based on saved Order property.
            await LoadTasksAsync(); // Reloads tasks ordered by the saved `Order` property.
            UpdateSelectedSortOptionDisplay(SortOrderType.Manual); // Ensure UI reflects manual state.
            return;
        }

        // Apply Priority-based sorting
        List<TaskItem> currentTasks = [];
        await MainThread.InvokeOnMainThreadAsync(() => { currentTasks = new List<TaskItem>(Tasks); });

        List<TaskItem> sortedTasks = sortToApply switch
        {
            SortOrderType.PriorityHighFirst => currentTasks
                .OrderByDescending(IsTaskEnabled)
                .ThenBy(task => task.Priority)
                .ThenBy(task => GetSortableTitle(task.Title), StringComparer.OrdinalIgnoreCase)
                .ToList(),
            SortOrderType.PriorityLowFirst => currentTasks
                .OrderByDescending(IsTaskEnabled)
                .ThenByDescending(task => task.Priority)
                .ThenBy(task => GetSortableTitle(task.Title), StringComparer.OrdinalIgnoreCase)
                .ToList(),
            // Should not happen due to the initial check, but include for completeness
            _ => currentTasks.OrderBy(task => task.Order).ToList()
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

        // Ensure the UI reflects the applied sort order
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
        TaskItem? taskToRemove = null;
        bool removedSuccessfully = false;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            taskToRemove = Tasks.FirstOrDefault(task => task.Id == taskId);
            if (taskToRemove is not null)
            {
                bool removed = Tasks.Remove(taskToRemove);
                if (removed)
                {
                    UpdateTaskOrderProperty();
                    TriggerSave();
                    removedSuccessfully = true;
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
        // Only trigger manual sort reset and save on user reorder (Move)
        if (eventArgs.Action == NotifyCollectionChangedAction.Move)
        {
            UpdateTaskOrderProperty();
            TriggerSave();
            ResetSortToManual(); // This now only updates the UI display
        }
        // Optional: Trigger save on Add/Remove as well, though handled by message handlers currently
        // else if (e.Action == NotifyCollectionChangedAction.Add || e.Action == NotifyCollectionChangedAction.Remove)
        // {
        //     TriggerSave();
        // }
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
                // Always update internal Order property based on current list position before saving
                UpdateTaskOrderPropertyInternal();
                tasksToSave = new List<TaskItem>(Tasks);
            });


            if (tasksToSave is not null && tasksToSave.Count > 0)
            {
                // No need to re-sort here, Order property is already updated
                // tasksToSave = tasksToSave.OrderBy(task => task.Order).ToList();

                JsonSerializerOptions options = new() { WriteIndented = true };
                string json = JsonSerializer.Serialize(tasksToSave, options);
                await File.WriteAllTextAsync(_filePath, json);
                Debug.WriteLine($"SaveTasks: Successfully saved {tasksToSave.Count} tasks to {_filePath}");
            }
            else if (tasksToSave is not null && tasksToSave.Count == 0)
            {
                // Handle saving an empty list
                if (File.Exists(_filePath))
                {
                    File.Delete(_filePath); // Or write "[]"
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

    // This method now ONLY updates the UI display to "Manual Order".
    // It DOES NOT change the persisted AppSettings.SelectedSortOrder.
    private void ResetSortToManual()
    {
        UpdateSelectedSortOptionDisplay(SortOrderType.Manual);
    }

    // Helper to get the display string for a given SortOrderType
    private string GetSortOptionDisplayString(SortOrderType sortOrder)
    {
        return sortOrder switch
        {
            SortOrderType.PriorityHighFirst => SortOptionsDisplay[1], // "Priority (High First)"
            SortOrderType.PriorityLowFirst => SortOptionsDisplay[2],  // "Priority (Low First)"
            _ => SortOptionsDisplay[0]                                // "Manual Order"
        };
    }


    private void UpdateSelectedSortOptionDisplay(SortOrderType sortOrder)
    {
        string newDisplayValue = GetSortOptionDisplayString(sortOrder);

        // Use SetProperty to ensure INotifyPropertyChanged is raised correctly for the UI picker
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
}