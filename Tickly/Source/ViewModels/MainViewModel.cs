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
using Microsoft.Maui.ApplicationModel; // Needed for MainThread
using Microsoft.Maui.Controls; // Needed for Shell
using Tickly.Messages;
using Tickly.Models;
using Tickly.Views;
using Tickly.Utils; // Needed for DateUtils

namespace Tickly.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<TaskItem> _tasks;

    private readonly string _filePath;
    private bool _isSaving = false;
    private readonly object _saveLock = new object();

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
        _tasks = new ObservableCollection<TaskItem>();

        SortOptionsDisplay = new List<string>
        {
            "Manual Order",
            "Priority (High First)",
            "Priority (Low First)"
        };
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
        try { await Shell.Current.GoToAsync(nameof(AddTaskPopupPage), true, new Dictionary<string, object> { { "TaskToEdit", null } }); }
        catch (Exception ex) { Debug.WriteLine($"Error navigating to add page: {ex.Message}"); }
    }

    [RelayCommand]
    private async Task NavigateToEditPage(TaskItem? taskToEdit)
    {
        if (taskToEdit == null) { return; }
        try
        {
            var navigationParameter = new Dictionary<string, object> { { "TaskToEdit", taskToEdit } };
            await Shell.Current.GoToAsync(nameof(AddTaskPopupPage), true, navigationParameter);
        }
        catch (Exception ex) { Debug.WriteLine($"Error navigating to edit page for task {taskToEdit.Id}: {ex.Message}"); }
    }

    [RelayCommand]
    private async Task LoadTasksAsync()
    {
        lock (_saveLock) { if (_isSaving) { return; } }
        Debug.WriteLine($"LoadTasksAsync: Attempting to load tasks from: {_filePath}");
        bool wasSubscribed = false;
        try { Tasks.CollectionChanged -= Tasks_CollectionChanged; wasSubscribed = true; } catch { }

        bool changesMade = false; // Flag to check if any task dates were adjusted

        try
        {
            List<TaskItem> loadedTasks = new List<TaskItem>();
            if (File.Exists(_filePath))
            {
                string json = await File.ReadAllTextAsync(_filePath);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    try { loadedTasks = JsonSerializer.Deserialize<List<TaskItem>>(json) ?? new List<TaskItem>(); }
                    catch (JsonException jsonEx) { Debug.WriteLine($"LoadTasksAsync: Error deserializing tasks JSON: {jsonEx.Message}"); }
                }
            }

            // --- Adjust Overdue Repeating Tasks ---
            DateTime today = DateTime.Today;
            foreach (var task in loadedTasks)
            {
                if (task.TimeType == TaskTimeType.Repeating && task.DueDate.HasValue && task.DueDate.Value.Date < today)
                {
                    DateTime originalDueDate = task.DueDate.Value.Date;
                    DateTime nextValidDueDate = originalDueDate; // Start with original

                    switch (task.RepetitionType)
                    {
                        case TaskRepetitionType.Daily:
                            nextValidDueDate = today;
                            break;

                        case TaskRepetitionType.AlternateDay:
                            // Calculate how many due dates were missed
                            double daysDifference = (today - originalDueDate).TotalDays;
                            // If an even number of days passed, today is a due date
                            // If an odd number, tomorrow is the next due date
                            if (daysDifference % 2 == 0)
                            {
                                nextValidDueDate = today;
                            }
                            else
                            {
                                nextValidDueDate = today.AddDays(1);
                            }
                            break;

                        case TaskRepetitionType.Weekly:
                            if (task.RepetitionDayOfWeek.HasValue)
                            {
                                // Find the next occurrence starting from today
                                nextValidDueDate = DateUtils.GetNextWeekday(today, task.RepetitionDayOfWeek.Value);
                            }
                            else
                            {
                                // Fallback if day is somehow missing (shouldn't happen with UI)
                                // Advance week by week until >= today
                                while (nextValidDueDate < today)
                                {
                                    nextValidDueDate = nextValidDueDate.AddDays(7);
                                }
                            }
                            break;
                    }

                    // Update if calculated date is different
                    if (task.DueDate.Value.Date != nextValidDueDate)
                    {
                        Debug.WriteLine($"LoadTasksAsync: Adjusting overdue task '{task.Title}' ({task.RepetitionType}) from {task.DueDate.Value.Date:d} to {nextValidDueDate:d}");
                        task.DueDate = nextValidDueDate;
                        changesMade = true; // Mark that a change occurred
                    }
                }
            }
            // --- End Adjustment Logic ---

            // Sort loaded tasks by original order before adding to collection
            var tasksToAdd = loadedTasks.OrderBy(t => t.Order).ToList();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Tasks.Clear();
                foreach (var task in tasksToAdd) { task.IsFadingOut = false; Tasks.Add(task); }
                UpdateTaskOrderProperty(); // Ensure order property is consistent
            });

            // Reset sort display to Manual after loading
            _currentSortOrder = SortOrderType.Manual;
            UpdateSelectedSortOptionDisplay(SortOrderType.Manual);

            // Save changes if any dates were adjusted during load
            if (changesMade)
            {
                Debug.WriteLine("LoadTasksAsync: Saving tasks due to date adjustments.");
                await TriggerSave(); // Use TriggerSave for debouncing/locking
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"LoadTasksAsync: Error loading tasks: {ex.GetType().Name} - {ex.Message}");
            if (Tasks.Any()) await MainThread.InvokeOnMainThreadAsync(Tasks.Clear);
            _currentSortOrder = SortOrderType.Manual;
            UpdateSelectedSortOptionDisplay(SortOrderType.Manual);
        }
        finally
        {
            // Re-subscribe ONLY if needed (if collection has items or was previously subscribed)
            if (wasSubscribed || Tasks.Any())
            {
                Tasks.CollectionChanged -= Tasks_CollectionChanged; // Ensure no double subscription
                Tasks.CollectionChanged += Tasks_CollectionChanged;
            }
        }
    }

    [RelayCommand]
    private async Task MarkTaskDone(TaskItem? task)
    {
        if (task == null || task.IsFadingOut) { return; }
        if (task.TimeType == TaskTimeType.None || task.TimeType == TaskTimeType.SpecificDate)
        {
            task.IsFadingOut = true;
            await Task.Delay(350);
            await MainThread.InvokeOnMainThreadAsync(async () => { if (Tasks.Remove(task)) { UpdateTaskOrderProperty(); await TriggerSave(); } else { task.IsFadingOut = false; } });
            ResetSortToManual();
        }
        else if (task.TimeType == TaskTimeType.Repeating)
        {
            // Calculate the NEXT due date based on the CURRENT due date
            DateTime? nextDueDate = DateUtils.CalculateNextDueDate(task);
            if (nextDueDate.HasValue)
            {
                // Temporarily store old task data if needed for smooth UI update
                // var oldTaskData = new { task.Title, task.Priority, task.TimeType, task.RepetitionType, task.RepetitionDayOfWeek, task.Order };

                // Update the due date *before* removing/adding to avoid flicker if possible
                task.DueDate = nextDueDate;

                // Instead of Remove/Add which can cause visual jump, try updating in place
                // Find the index and trigger property change notifications
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    int index = Tasks.IndexOf(task);
                    if (index != -1)
                    {
                        // No need to Remove/Add if just updating properties of the existing object
                        // The UI should update based on INotifyPropertyChanged from TaskItem.DueDate
                        // Force a refresh of the item binding if needed (less common now with ObservableObject)
                        // Tasks[index] = task; // Reassigning might trigger UI refresh more reliably for some collection views
                        UpdateTaskOrderProperty(); // Update order just in case
                        await TriggerSave(); // Save the updated task
                    }
                    else
                    {
                        // Fallback if task was somehow not found (unlikely)
                        if (Tasks.Remove(task)) // If removed successfully, add back the updated one
                        {
                            Tasks.Add(task); // Add updated task back (might go to end)
                            UpdateTaskOrderProperty(); // Re-apply order
                            await TriggerSave();
                        }
                    }
                });

                // Re-apply sort if needed (or reset to manual)
                if (_currentSortOrder != SortOrderType.Manual)
                {
                    ApplySortOrderCommand.Execute(null); // Re-apply current sort
                }
                else
                {
                    // If manual, we might want to keep it manual, or potentially
                    // move the task based on its new date if dates were visible in manual sort
                    // For now, just save and keep manual order
                }
            }
            else
            {
                // Handle cases where next due date couldn't be calculated (error or unexpected type)
                Debug.WriteLine($"MarkTaskDone: Could not calculate next due date for repeating task '{task.Title}'. Removing.");
                // Fallback to remove like non-repeating
                task.IsFadingOut = true;
                await Task.Delay(350);
                await MainThread.InvokeOnMainThreadAsync(async () => { if (Tasks.Remove(task)) { UpdateTaskOrderProperty(); await TriggerSave(); } else { task.IsFadingOut = false; } });
                ResetSortToManual();
            }
        }
    }

    [RelayCommand]
    private async Task ApplySortOrder()
    {
        SortOrderType requestedSortOrder = SelectedSortOption switch { "Priority (High First)" => SortOrderType.PriorityHighFirst, "Priority (Low First)" => SortOrderType.PriorityLowFirst, _ => SortOrderType.Manual };

        // If requesting manual sort, reload from saved order
        if (requestedSortOrder == SortOrderType.Manual && _currentSortOrder != SortOrderType.Manual)
        {
            await LoadTasksAsync(); // Reloads using saved 'Order' property
            return;
        }

        // Avoid re-sorting if already sorted that way
        if (requestedSortOrder == _currentSortOrder && requestedSortOrder != SortOrderType.Manual) { return; }


        List<TaskItem> currentTasks = new List<TaskItem>();
        await MainThread.InvokeOnMainThreadAsync(() => { currentTasks = new List<TaskItem>(Tasks); });

        List<TaskItem> sortedTasks;
        if (requestedSortOrder == SortOrderType.PriorityHighFirst) { sortedTasks = currentTasks.OrderBy(t => t.Priority).ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase).ToList(); }
        else if (requestedSortOrder == SortOrderType.PriorityLowFirst) { sortedTasks = currentTasks.OrderByDescending(t => t.Priority).ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase).ToList(); }
        else { return; } // Should be handled by reload case above


        // Avoid UI churn if the order hasn't actually changed
        if (currentTasks.SequenceEqual(sortedTasks))
        {
            _currentSortOrder = requestedSortOrder; // Update state even if sequence is same
            UpdateSelectedSortOptionDisplay(requestedSortOrder); // Reflect in Picker
            return;
        }


        Tasks.CollectionChanged -= Tasks_CollectionChanged; // Prevent triggering save on each add/remove
        try
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Tasks.Clear();
                foreach (var task in sortedTasks)
                {
                    Tasks.Add(task); // Add tasks in the new sorted order
                }
                // Note: Do NOT update Task.Order property here, as that reflects manual order
            });
            // Do NOT save here, sorting is temporary view state unless it's manual
            _currentSortOrder = requestedSortOrder;
            UpdateSelectedSortOptionDisplay(requestedSortOrder); // Ensure Picker reflects sort
        }
        catch (Exception ex) { Debug.WriteLine($"ApplySortOrder: Error during sorting: {ex.Message}"); }
        finally { if (Tasks.Any()) { Tasks.CollectionChanged -= Tasks_CollectionChanged; Tasks.CollectionChanged += Tasks_CollectionChanged; } } // Re-attach handler
    }

    private async void HandleCalendarSettingChanged()
    {
        // Reload tasks to reflect new calendar formatting
        await LoadTasksAsync();
    }
    private async void HandleAddTask(TaskItem? newTask)
    {
        if (newTask == null) return;
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            newTask.Order = Tasks.Count; // Assign next available order index
            Tasks.Add(newTask);
            // UpdateTaskOrderProperty(); // Redundant as we just set it
            await TriggerSave(); // Save the new task and its order
        });
        // If currently sorted, adding might disrupt sort. Reset to manual or re-apply sort.
        // Resetting to manual is safest as new item position in sorted list isn't obvious
        ResetSortToManual();
    }
    private async void HandleUpdateTask(TaskItem? updatedTask)
    {
        if (updatedTask == null) return;
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
                // Preserve original order unless explicitly changed
                updatedTask.Order = Tasks[index].Order;
                Tasks[index] = updatedTask; // Replace item in collection
                await TriggerSave(); // Save the changes
            }
        });
        // If currently sorted, updating might change position. Reset to manual or re-apply sort.
        ResetSortToManual();
    }
    private async void HandleDeleteTask(Guid taskId)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var taskToRemove = Tasks.FirstOrDefault(t => t.Id == taskId);
            if (taskToRemove != null)
            {
                if (Tasks.Remove(taskToRemove))
                {
                    UpdateTaskOrderProperty(); // Re-assign order indexes to remaining tasks
                    await TriggerSave(); // Save the updated list and orders
                }
            }
        });
        // Deleting might affect sorted view, but usually safe to keep current sort
        // Resetting to manual ensures consistency if needed.
        // ResetSortToManual(); // Optional: uncomment if deleting should always reset sort
    }
    private async void Tasks_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Only save when items are manually reordered (Move action)
        if (e.Action == NotifyCollectionChangedAction.Move)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                UpdateTaskOrderProperty(); // Update Order property based on new positions
                await TriggerSave(); // Save the new manual order
            });
            // User manually reordered, so switch back to Manual sort view
            ResetSortToManual();
        }
        // Add/Remove/Replace actions trigger saves within their respective handlers (HandleAddTask, etc.)
        // Reset action is handled by LoadTasksAsync
    }
    private void UpdateTaskOrderProperty()
    {
        if (!MainThread.IsMainThread) { MainThread.BeginInvokeOnMainThread(UpdateTaskOrderPropertyInternal); }
        else { UpdateTaskOrderPropertyInternal(); }
    }
    private void UpdateTaskOrderPropertyInternal()
    {
        for (int i = 0; i < Tasks.Count; i++)
        {
            // Check bounds and null before accessing Task properties
            if (i < Tasks.Count && Tasks[i] != null && Tasks[i].Order != i)
            {
                Tasks[i].Order = i; // Assign index as the Order value for saving manual order
            }
        }
    }
    private async Task SaveTasks()
    {
        bool acquiredLock = false;
        List<TaskItem> tasksToSave = new List<TaskItem>();

        try
        {
            // Ensure thread safety for checking/setting _isSaving flag
            lock (_saveLock)
            {
                if (_isSaving)
                {
                    Debug.WriteLine("SaveTasks: Save already in progress. Skipping.");
                    return; // Exit if another save is running
                }
                _isSaving = true; // Mark as saving
                acquiredLock = true;
            }

            // Prepare the list of tasks to save on the main thread to ensure collection consistency
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                UpdateTaskOrderPropertyInternal(); // Ensure orders are correct before saving
                tasksToSave = new List<TaskItem>(Tasks); // Create a copy for serialization
            });

            if (tasksToSave != null) // Check if list creation succeeded
            {
                // Sort by the Order property before serializing to preserve manual order
                tasksToSave = tasksToSave.OrderBy(t => t.Order).ToList();

                string json = JsonSerializer.Serialize(tasksToSave, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_filePath, json);
                Debug.WriteLine($"SaveTasks: Successfully saved {tasksToSave.Count} tasks to {_filePath}");
            }
        }
        catch (Exception ex) { Debug.WriteLine($"SaveTasks: Error saving tasks: {ex.Message}"); }
        finally
        {
            if (acquiredLock)
            {
                // Release the saving flag
                lock (_saveLock) { _isSaving = false; }
            }
        }
    }
    // Debounced save trigger
    private System.Threading.Timer? _debounceTimer;
    private async Task TriggerSave()
    {
        // Dispose previous timer if exists
        _debounceTimer?.Dispose();

        // Create a new timer that calls SaveTasks after a delay
        _debounceTimer = new System.Threading.Timer(async (_) =>
        {
            await SaveTasks(); // Call the actual save method
            _debounceTimer?.Dispose(); // Clean up the timer after execution
            _debounceTimer = null;
        },
        null, // No state needed
        TimeSpan.FromMilliseconds(500), // Wait 500ms before saving
        Timeout.InfiniteTimeSpan); // Don't repeat automatically
    }
    private void ResetSortToManual()
    {
        if (_currentSortOrder != SortOrderType.Manual)
        {
            _currentSortOrder = SortOrderType.Manual;
            UpdateSelectedSortOptionDisplay(SortOrderType.Manual);
            // Optionally, explicitly reload to guarantee view matches saved order
            // _ = LoadTasksAsync(); // Uncomment if needed, but might be overkill
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
            // Use SetProperty to ensure UI update via INotifyPropertyChanged
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