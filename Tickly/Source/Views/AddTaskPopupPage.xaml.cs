using System.Collections.ObjectModel;
using System.Diagnostics; // For Debug.WriteLine
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Tickly.Messages; // Ensure UpdateTaskMessage and DeleteTaskMessage are included
using Tickly.Models;

namespace Tickly.Views;

// Helper class for binding collections to RadioButtons
public partial class SelectableOption<T> : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;
    public string Name { get; }
    public T Value { get; }

    public SelectableOption(string name, T value, bool isSelected = false)
    {
        Name = name;
        Value = value;
        IsSelected = isSelected;
    }
}

// ViewModel for the Add/Edit Task Page
public partial class AddTaskPopupPageViewModel : ObservableObject
{
    // Properties bound to UI controls
    [ObservableProperty] private string _title = string.Empty; // Add Title property for binding
    [ObservableProperty] private DateTime _dueDate = DateTime.Today;
    [ObservableProperty] private bool _isTimeTypeNone = true; // Default selection
    [ObservableProperty] private bool _isTimeTypeSpecificDate;
    [ObservableProperty] private bool _isTimeTypeRepeating;
    // REMOVED: PriorityOptions
    // [ObservableProperty] private ObservableCollection<SelectableOption<TaskPriority>> _priorityOptions;
    [ObservableProperty] private ObservableCollection<SelectableOption<TaskRepetitionType>> _repetitionTypeOptions;
    [ObservableProperty] private bool _isWeeklySelected; // Controls visibility of DayOfWeek picker

    // Updated: Use List<string> for display names to simplify binding
    [ObservableProperty] private List<string> _displayDaysOfWeek;
    [ObservableProperty] private string _selectedDisplayDayOfWeek;

    // Internal property to track the original TaskItem for Edit mode
    private TaskItem? _originalTask;

    // Properties for UI state (EditMode, Titles)
    [ObservableProperty] private bool _isEditMode;
    [ObservableProperty] private string _pageTitle = "Add New Task";
    [ObservableProperty] private string _confirmButtonText = "Add Task";

    public AddTaskPopupPageViewModel()
    {
        // REMOVED: Priority options initialization
        // PriorityOptions = new ObservableCollection<SelectableOption<TaskPriority>> { ... };

        // Initialize Repetition options
        RepetitionTypeOptions = new ObservableCollection<SelectableOption<TaskRepetitionType>>
        {
            new SelectableOption<TaskRepetitionType>("Daily", TaskRepetitionType.Daily, true), // Default selected
            new SelectableOption<TaskRepetitionType>("Alternate Day", TaskRepetitionType.AlternateDay),
            new SelectableOption<TaskRepetitionType>("Weekly", TaskRepetitionType.Weekly)
        };

        // Initialize Display Days of Week
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        DisplayDaysOfWeek = culture.DateTimeFormat.DayNames.ToList();
        SelectedDisplayDayOfWeek = culture.DateTimeFormat.GetDayName(DateTime.Today.DayOfWeek);

        // Handle changes in RepetitionType selection
        foreach (var option in RepetitionTypeOptions)
        {
            option.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(SelectableOption<TaskRepetitionType>.IsSelected))
                {
                    var changedOption = sender as SelectableOption<TaskRepetitionType>;
                    if (changedOption != null && changedOption.IsSelected)
                    {
                        IsWeeklySelected = (changedOption.Value == TaskRepetitionType.Weekly);
                    }
                    else if (changedOption != null && !changedOption.IsSelected && changedOption.Value == TaskRepetitionType.Weekly)
                    {
                        if (RepetitionTypeOptions.FirstOrDefault(o => o.IsSelected)?.Value != TaskRepetitionType.Weekly)
                        {
                            IsWeeklySelected = false;
                        }
                    }
                }
            };
        }
        IsWeeklySelected = RepetitionTypeOptions.FirstOrDefault(o => o.IsSelected)?.Value == TaskRepetitionType.Weekly;

        // Handle changes in the main TimeType selection
        this.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(IsTimeTypeRepeating) || args.PropertyName == nameof(IsTimeTypeSpecificDate) || args.PropertyName == nameof(IsTimeTypeNone))
            {
                IsWeeklySelected = IsTimeTypeRepeating && (RepetitionTypeOptions.FirstOrDefault(o => o.IsSelected)?.Value == TaskRepetitionType.Weekly);
            }
        };
    }

    public void LoadFromTask(TaskItem task)
    {
        _originalTask = task;
        IsEditMode = true;
        PageTitle = "Edit Task";
        ConfirmButtonText = "Update Task";

        Title = task.Title;

        // REMOVED: Loading priority
        // foreach (var option in PriorityOptions) option.IsSelected = (option.Value == task.Priority);
        // if (!PriorityOptions.Any(o => o.IsSelected)) PriorityOptions.FirstOrDefault(o => o.Value == TaskPriority.Medium)!.IsSelected = true;

        IsTimeTypeNone = task.TimeType == TaskTimeType.None;
        IsTimeTypeSpecificDate = task.TimeType == TaskTimeType.SpecificDate;
        IsTimeTypeRepeating = task.TimeType == TaskTimeType.Repeating;

        DueDate = task.DueDate ?? DateTime.Today;

        if (task.TimeType == TaskTimeType.Repeating)
        {
            foreach (var option in RepetitionTypeOptions)
                option.IsSelected = (option.Value == task.RepetitionType);
            if (!RepetitionTypeOptions.Any(o => o.IsSelected))
                RepetitionTypeOptions.FirstOrDefault(o => o.Value == TaskRepetitionType.Daily)!.IsSelected = true;

            var culture = System.Globalization.CultureInfo.InvariantCulture;
            SelectedDisplayDayOfWeek = culture.DateTimeFormat.GetDayName(task.RepetitionDayOfWeek ?? DateTime.Today.DayOfWeek);

            IsWeeklySelected = RepetitionTypeOptions.FirstOrDefault(o => o.IsSelected)?.Value == TaskRepetitionType.Weekly;
        }
        else
        {
            RepetitionTypeOptions.FirstOrDefault(o => o.Value == TaskRepetitionType.Daily)!.IsSelected = true;
            var culture = System.Globalization.CultureInfo.InvariantCulture;
            SelectedDisplayDayOfWeek = culture.DateTimeFormat.GetDayName(DateTime.Today.DayOfWeek);
            IsWeeklySelected = false;
        }
    }

    public TaskItem? GetTaskItem()
    {
        string title = Title?.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            Debug.WriteLine("Validation Error: Task title cannot be empty.");
            return null;
        }

        // REMOVED: Reading priority
        // var selectedPriorityOption = PriorityOptions.FirstOrDefault(p => p.IsSelected);
        // TaskPriority priority = selectedPriorityOption?.Value ?? TaskPriority.Medium;

        TaskTimeType timeType = IsTimeTypeSpecificDate ? TaskTimeType.SpecificDate :
                                IsTimeTypeRepeating ? TaskTimeType.Repeating :
                                TaskTimeType.None;

        DateTime? dueDate = null;
        if (timeType == TaskTimeType.SpecificDate || timeType == TaskTimeType.Repeating)
        {
            dueDate = DueDate;
        }

        TaskRepetitionType? repetitionType = null;
        DayOfWeek? repetitionDayOfWeek = null;
        if (timeType == TaskTimeType.Repeating)
        {
            var selectedRepetitionOption = RepetitionTypeOptions.FirstOrDefault(r => r.IsSelected);
            repetitionType = selectedRepetitionOption?.Value ?? TaskRepetitionType.Daily;

            if (repetitionType == TaskRepetitionType.Weekly)
            {
                repetitionDayOfWeek = GetDayOfWeekFromDisplayName(SelectedDisplayDayOfWeek);
            }
        }

        if (IsEditMode && _originalTask != null)
        {
            var updatedTask = new TaskItem(
                title, /* REMOVED: priority, */ timeType, dueDate, repetitionType, repetitionDayOfWeek, _originalTask.Order
            )
            {
                Id = _originalTask.Id,
                Index = _originalTask.Index // Preserve index if updating
            };
            return updatedTask;
        }
        else
        {
            var newTask = new TaskItem(title, /* REMOVED: priority, */ timeType, dueDate, repetitionType, repetitionDayOfWeek);
            // Index and Order will be assigned by MainViewModel
            return newTask;
        }
    }

    private DayOfWeek? GetDayOfWeekFromDisplayName(string displayName)
    {
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        for (int i = 0; i < culture.DateTimeFormat.DayNames.Length; i++)
        {
            if (culture.DateTimeFormat.DayNames[i].Equals(displayName, StringComparison.OrdinalIgnoreCase))
            {
                return (DayOfWeek)i;
            }
        }
        return null;
    }

    public void Reset()
    {
        Title = string.Empty;
        // REMOVED: Reset priority
        // PriorityOptions.FirstOrDefault(o => o.Value == TaskPriority.Medium)!.IsSelected = true;
        IsTimeTypeNone = true;
        IsTimeTypeSpecificDate = false;
        IsTimeTypeRepeating = false;
        DueDate = DateTime.Today;
        RepetitionTypeOptions.FirstOrDefault(o => o.Value == TaskRepetitionType.Daily)!.IsSelected = true;
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        SelectedDisplayDayOfWeek = culture.DateTimeFormat.GetDayName(DateTime.Today.DayOfWeek);
        IsWeeklySelected = false;

        _originalTask = null;
        IsEditMode = false;
        PageTitle = "Add New Task";
        ConfirmButtonText = "Add Task";

        // Notify UI about all property changes after reset
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(IsTimeTypeNone));
        OnPropertyChanged(nameof(IsTimeTypeSpecificDate));
        OnPropertyChanged(nameof(IsTimeTypeRepeating));
        OnPropertyChanged(nameof(DueDate));
        OnPropertyChanged(nameof(SelectedDisplayDayOfWeek));
        OnPropertyChanged(nameof(IsWeeklySelected));
        OnPropertyChanged(nameof(IsEditMode));
        OnPropertyChanged(nameof(PageTitle));
        OnPropertyChanged(nameof(ConfirmButtonText));
    }
}

// Code-behind for the Add/Edit Task Page
[QueryProperty(nameof(TaskToEdit), "TaskToEdit")]
public partial class AddTaskPopupPage : ContentPage
{
    private AddTaskPopupPageViewModel _viewModel;

    public TaskItem TaskToEdit
    {
        set
        {
            if (value != null)
            {
                Debug.WriteLine($"AddTaskPopupPage: Received TaskToEdit with ID: {value.Id}");
                _viewModel.LoadFromTask(value);
            }
            else
            {
                Debug.WriteLine("AddTaskPopupPage: Navigated without TaskToEdit (Add Mode).");
                _viewModel.Reset();
            }
        }
    }

    public AddTaskPopupPage()
    {
        InitializeComponent();
        _viewModel = new AddTaskPopupPageViewModel();
        BindingContext = _viewModel;
        Debug.WriteLine("AddTaskPopupPage: Constructor completed.");
    }

    private async Task ShowAndroidDebugAlert(string title, string message)
    {
#if ANDROID
        try
        {
            // Ensure we are on the main thread for UI operations
            if (!MainThread.IsMainThread)
            {
                await MainThread.InvokeOnMainThreadAsync(async () => await DisplayAlert(title, message, "OK"));
            }
            else
            {
                await DisplayAlert(title, message, "OK");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ANDROID_DEBUG_ALERT_FAIL] {title}: {ex.Message}");
        }
#else
        await Task.CompletedTask;
#endif
    }


    private async void OnConfirmClicked(object sender, EventArgs e)
    {
        // await ShowAndroidDebugAlert("Debug Confirm", "OnConfirmClicked entered."); // No longer needed for this specific issue
        var taskItem = _viewModel.GetTaskItem();

        if (taskItem == null)
        {
            await DisplayAlert("Validation Error", "Task title cannot be empty.", "OK");
            return;
        }

        if (_viewModel.IsEditMode)
        {
            Debug.WriteLine($"Sending UpdateTaskMessage for Task ID: {taskItem.Id}");
            WeakReferenceMessenger.Default.Send(new UpdateTaskMessage(taskItem));
        }
        else
        {
            Debug.WriteLine($"Sending AddTaskMessage for new task: {taskItem.Title}");
            WeakReferenceMessenger.Default.Send(new AddTaskMessage(taskItem));
        }

        await Shell.Current.Navigation.PopModalAsync();
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        // await ShowAndroidDebugAlert("Debug Cancel", "OnCancelClicked entered."); // No longer needed for this specific issue
        await Shell.Current.Navigation.PopModalAsync();
    }

    private async void OnDeleteClicked(object sender, EventArgs e)
    {
        // await ShowAndroidDebugAlert("Debug Delete", "OnDeleteClicked entered."); // No longer needed for this specific issue
        var taskToDelete = _viewModel.GetTaskItem();

        if (_viewModel.IsEditMode && taskToDelete != null)
        {
            bool confirmed = await DisplayAlert("Confirm Delete", $"Are you sure you want to delete the task '{taskToDelete.Title}'?", "Yes", "No");
            if (!confirmed)
            {
                return;
            }

            Debug.WriteLine($"Sending DeleteTaskMessage for Task ID: {taskToDelete.Id}");
            WeakReferenceMessenger.Default.Send(new DeleteTaskMessage(taskToDelete.Id));

            await Shell.Current.Navigation.PopModalAsync();
        }
        else
        {
            Debug.WriteLine("OnDeleteClicked triggered but not in Edit Mode or Task ID missing. Cannot delete.");
            await DisplayAlert("Error", "Cannot delete task. No task loaded for editing.", "OK");
        }
    }

    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        base.OnNavigatedFrom(args);
        Debug.WriteLine("AddTaskPopupPage: Navigated From.");
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        Debug.WriteLine($"AddTaskPopupPage: Navigated To. Edit Mode: {_viewModel.IsEditMode}");
    }
}
