// File: ViewModels\MainViewModel.cs
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
        // *** Register for Reload Message ***
        WeakReferenceMessenger.Default.Register<TasksReloadRequestedMessage>(this, async (r, m) => await HandleTasksReloadRequested());
        // *** END Register ***
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
        try
        {
            List<TaskItem> tasksToAdd = new List<TaskItem>();
            if (File.Exists(_filePath))
            {
                string json = await File.ReadAllTextAsync(_filePath);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    try { var loadedTasks = JsonSerializer.Deserialize<List<TaskItem>>(json); tasksToAdd = loadedTasks?.OrderBy(t => t.Order).ToList() ?? new List<TaskItem>(); }
                    catch (JsonException jsonEx) { Debug.WriteLine($"LoadTasksAsync: Error deserializing tasks JSON: {jsonEx.Message}"); }
                }
            }
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Tasks.Clear();
                foreach (var task in tasksToAdd) { task.IsFadingOut = false; Tasks.Add(task); }
                UpdateTaskOrderProperty();
            });
            _currentSortOrder = SortOrderType.Manual;
            UpdateSelectedSortOptionDisplay(SortOrderType.Manual);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"LoadTasksAsync: Error loading tasks: {ex.GetType().Name} - {ex.Message}");
            if (Tasks.Any()) await MainThread.InvokeOnMainThreadAsync(Tasks.Clear);
            _currentSortOrder = SortOrderType.Manual;
            UpdateSelectedSortOptionDisplay(SortOrderType.Manual);
        }
        finally { if (wasSubscribed || Tasks.Any()) { Tasks.CollectionChanged -= Tasks_CollectionChanged; Tasks.CollectionChanged += Tasks_CollectionChanged; } }
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
            DateTime? nextDueDate = DateUtils.CalculateNextDueDate(task);
            if (nextDueDate.HasValue)
            {
                task.DueDate = nextDueDate;
                await MainThread.InvokeOnMainThreadAsync(async () => { if (Tasks.Remove(task)) { Tasks.Add(task); UpdateTaskOrderProperty(); await TriggerSave(); } });
                ResetSortToManual();
            }
        }
    }

    [RelayCommand]
    private async Task ApplySortOrder()
    {
        SortOrderType requestedSortOrder = SelectedSortOption switch { "Priority (High First)" => SortOrderType.PriorityHighFirst, "Priority (Low First)" => SortOrderType.PriorityLowFirst, _ => SortOrderType.Manual };
        if (requestedSortOrder == _currentSortOrder) { return; }
        if (requestedSortOrder == SortOrderType.Manual) { await LoadTasksAsync(); return; }
        List<TaskItem> currentTasks = new List<TaskItem>();
        await MainThread.InvokeOnMainThreadAsync(() => { currentTasks = new List<TaskItem>(Tasks); });
        List<TaskItem> sortedTasks;
        if (requestedSortOrder == SortOrderType.PriorityHighFirst) { sortedTasks = currentTasks.OrderBy(t => t.Priority).ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase).ToList(); }
        else { sortedTasks = currentTasks.OrderByDescending(t => t.Priority).ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase).ToList(); }
        Tasks.CollectionChanged -= Tasks_CollectionChanged;
        try
        {
            await MainThread.InvokeOnMainThreadAsync(() => { Tasks.Clear(); foreach (var task in sortedTasks) { Tasks.Add(task); } UpdateTaskOrderProperty(); });
            await TriggerSave();
            _currentSortOrder = requestedSortOrder;
        }
        catch (Exception ex) { Debug.WriteLine($"ApplySortOrder: Error during sorting or saving: {ex.Message}"); }
        finally { if (Tasks.Any()) { Tasks.CollectionChanged -= Tasks_CollectionChanged; Tasks.CollectionChanged += Tasks_CollectionChanged; } }
    }

    private async void HandleCalendarSettingChanged() { await LoadTasksAsync(); }
    private async void HandleAddTask(TaskItem? newTask) { if (newTask == null) return; await MainThread.InvokeOnMainThreadAsync(async () => { newTask.Order = Tasks.Count; Tasks.Add(newTask); await TriggerSave(); }); ResetSortToManual(); }
    private async void HandleUpdateTask(TaskItem? updatedTask) { if (updatedTask == null) return; await MainThread.InvokeOnMainThreadAsync(async () => { int index = -1; for (int i = 0; i < Tasks.Count; i++) { if (Tasks[i].Id == updatedTask.Id) { index = i; break; } } if (index != -1) { updatedTask.Order = index; Tasks[index] = updatedTask; await TriggerSave(); } }); ResetSortToManual(); }
    private async void HandleDeleteTask(Guid taskId) { await MainThread.InvokeOnMainThreadAsync(async () => { var taskToRemove = Tasks.FirstOrDefault(t => t.Id == taskId); if (taskToRemove != null) { if (Tasks.Remove(taskToRemove)) { UpdateTaskOrderProperty(); await TriggerSave(); } } }); ResetSortToManual(); }
    private async void Tasks_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) { if (e.Action == NotifyCollectionChangedAction.Move) { await MainThread.InvokeOnMainThreadAsync(async () => { UpdateTaskOrderProperty(); await TriggerSave(); }); ResetSortToManual(); } }
    private void UpdateTaskOrderProperty() { if (!MainThread.IsMainThread) { MainThread.BeginInvokeOnMainThread(UpdateTaskOrderPropertyInternal); } else { UpdateTaskOrderPropertyInternal(); } }
    private void UpdateTaskOrderPropertyInternal() { for (int i = 0; i < Tasks.Count; i++) { if (i < Tasks.Count && Tasks[i] != null && Tasks[i].Order != i) { Tasks[i].Order = i; } } }
    private async Task SaveTasks() { bool acquiredLock = false; List<TaskItem> tasksToSave = new List<TaskItem>(); try { lock (_saveLock) { if (_isSaving) { return; } _isSaving = true; acquiredLock = true; } await MainThread.InvokeOnMainThreadAsync(() => { UpdateTaskOrderPropertyInternal(); tasksToSave = new List<TaskItem>(Tasks); }); if (tasksToSave != null) { string json = JsonSerializer.Serialize(tasksToSave, new JsonSerializerOptions { WriteIndented = true }); await File.WriteAllTextAsync(_filePath, json); } } catch (Exception ex) { Debug.WriteLine($"SaveTasks: Error saving tasks: {ex.Message}"); } finally { if (acquiredLock) { lock (_saveLock) { _isSaving = false; } } } }
    private async Task TriggerSave() { lock (_saveLock) { if (_isSaving) { return; } } try { await Task.Delay(300); await SaveTasks(); } catch (Exception ex) { Debug.WriteLine($"TriggerSave: Error during debounce or SaveTasks call: {ex.Message}"); } }
    private void ResetSortToManual() { if (_currentSortOrder != SortOrderType.Manual) { _currentSortOrder = SortOrderType.Manual; UpdateSelectedSortOptionDisplay(SortOrderType.Manual); } }
    private void UpdateSelectedSortOptionDisplay(SortOrderType sortOrder) { string newDisplayValue = sortOrder switch { SortOrderType.PriorityHighFirst => SortOptionsDisplay[1], SortOrderType.PriorityLowFirst => SortOptionsDisplay[2], _ => SortOptionsDisplay[0] }; if (_selectedSortOption != newDisplayValue) { SetProperty(ref _selectedSortOption, newDisplayValue, nameof(SelectedSortOption)); } }

    // *** NEW Handler for Reload Message ***
    private async Task HandleTasksReloadRequested()
    {
        Debug.WriteLine("MainViewModel: Received TasksReloadRequestedMessage. Reloading tasks...");
        await LoadTasksAsync();
    }
    // *** END NEW Handler ***
}