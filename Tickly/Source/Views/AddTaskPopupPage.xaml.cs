// Views/AddTaskPopupPage.xaml.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel; // Required for PropertyChangedEventArgs
using System.Diagnostics;
using System.Globalization; // Needed for CultureInfo, CalendarWeekRule, PersianCalendar
using System.Linq;
using Tickly.Messages; // Contains AddTaskMessage, UpdateTaskMessage, DeleteTaskMessage
using Tickly.Models;   // Contains TaskItem and enums
using Tickly.Services; // Needed for AppSettings
using Tickly.Utils;    // Needed for DateUtils helper

namespace Tickly.Views;

// --- Helper Class for RadioButton Bindings ---
// Represents an option (like Priority or RepetitionType) that can be selected.
public partial class SelectableOption<T> : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected; // Bound to RadioButton.IsChecked

    public string Name { get; }   // Text displayed next to the RadioButton
    public T Value { get; }      // The actual enum value this option represents

    public SelectableOption(string name, T value, bool isSelected = false)
    {
        Name = name;
        Value = value;
        _isSelected = isSelected; // Initialize IsSelected (use field directly in constructor)
    }
}

// --- ViewModel for the Add/Edit Task Page ---
// Manages the data and logic for the AddTaskPopupPage UI.
public partial class AddTaskPopupPageViewModel : ObservableObject
{
    // --- Properties for Edit Mode Control ---
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageTitle))]      // Update PageTitle when IsEditMode changes
    [NotifyPropertyChangedFor(nameof(ConfirmButtonText))]// Update ConfirmButtonText when IsEditMode changes
    private bool _isEditMode = false; // Determines if the page is for adding (false) or editing (true)

    public string PageTitle => IsEditMode ? "Edit Task" : "Add New Task"; // Dynamically set page title
    public string ConfirmButtonText => IsEditMode ? "Update" : "Confirm"; // Dynamically set confirm button text

    private TaskItem? _taskToEdit; // Stores the original task when in edit mode

    // --- Observable Properties Bound to UI Elements ---
    [ObservableProperty]
    private string _title = string.Empty; // Bound to the Title Entry

    // This DueDate property is now ONLY used by the "Specific Date" DatePicker.
    // It's NOT used as the start date for Repeating tasks anymore during creation.
    [ObservableProperty]
    private DateTime _dueDate = DateTime.Today;

    // Radio button states for Time Type
    [ObservableProperty]
    private bool _isTimeTypeNone = true;
    [ObservableProperty]
    private bool _isTimeTypeSpecificDate;
    [ObservableProperty]
    private bool _isTimeTypeRepeating;

    // Collections for Radio Button groups
    [ObservableProperty]
    private ObservableCollection<SelectableOption<TaskPriority>> _priorityOptions;
    [ObservableProperty]
    private ObservableCollection<SelectableOption<TaskRepetitionType>> _repetitionTypeOptions;

    // Visibility control for Weekly options
    [ObservableProperty]
    private bool _isWeeklySelected;

    // --- DayOfWeek Handling ---
    private readonly CultureInfo _calendarCulture; // Readonly, set in constructor
    [ObservableProperty] private ObservableCollection<string> _displayDaysOfWeek = new();
    [ObservableProperty] private string? _selectedDisplayDayOfWeek;
    private DayOfWeek _selectedDayOfWeekEnum = DateTime.Today.DayOfWeek;

    // --- Constructor ---
    public AddTaskPopupPageViewModel()
    {
        // Determine culture based on saved setting
        _calendarCulture = AppSettings.SelectedCalendarSystem == CalendarSystemType.Persian
                          ? new CultureInfo("fa-IR")
                          : CultureInfo.InvariantCulture;

        LoadDisplayDaysOfWeek(); // Populate day names for picker

        // Set default selected display day based on today
        _selectedDayOfWeekEnum = DateTime.Today.DayOfWeek;
        _selectedDisplayDayOfWeek = MapDayOfWeekToDisplayDay(_selectedDayOfWeekEnum, _calendarCulture);

        // Initialize Priority options
        PriorityOptions = new ObservableCollection<SelectableOption<TaskPriority>>
        {
            new SelectableOption<TaskPriority>("High (Red)", TaskPriority.High),
            new SelectableOption<TaskPriority>("Medium (Orange)", TaskPriority.Medium, !IsEditMode),
            new SelectableOption<TaskPriority>("Low (Green)", TaskPriority.Low)
        };

        // Initialize Repetition Type options
        RepetitionTypeOptions = new ObservableCollection<SelectableOption<TaskRepetitionType>>
        {
            new SelectableOption<TaskRepetitionType>("Daily", TaskRepetitionType.Daily, !IsEditMode),
            new SelectableOption<TaskRepetitionType>("Alternate Day", TaskRepetitionType.AlternateDay),
            new SelectableOption<TaskRepetitionType>("Weekly", TaskRepetitionType.Weekly)
        };

        // Set up event handlers
        SetupOptionChangeHandlers();
        SetupTimeTypeChangeHandlers();
        this.PropertyChanged += ViewModel_PropertyChanged; // Handle picker selection changes

        RecalculateWeeklyVisibility(); // Initial check for weekly picker visibility
    }

    // --- UI Logic Methods ---

    // Populates the DisplayDaysOfWeek collection based on the culture
    private void LoadDisplayDaysOfWeek()
    {
        DisplayDaysOfWeek.Clear();
        string[] dayNames = _calendarCulture.DateTimeFormat.DayNames;
        if (AppSettings.SelectedCalendarSystem == CalendarSystemType.Persian)
        {
            DisplayDaysOfWeek.Add(dayNames[6]); DisplayDaysOfWeek.Add(dayNames[0]); DisplayDaysOfWeek.Add(dayNames[1]);
            DisplayDaysOfWeek.Add(dayNames[2]); DisplayDaysOfWeek.Add(dayNames[3]); DisplayDaysOfWeek.Add(dayNames[4]);
            DisplayDaysOfWeek.Add(dayNames[5]);
        }
        else
        {
            var sortedDays = Enum.GetValues(typeof(DayOfWeek)).Cast<DayOfWeek>().OrderBy(d => (int)d);
            foreach (var day in sortedDays) { DisplayDaysOfWeek.Add(_calendarCulture.DateTimeFormat.GetDayName(day)); }
        }
        Debug.WriteLine($"Loaded DisplayDaysOfWeek ({_calendarCulture.Name}): {string.Join(", ", DisplayDaysOfWeek)}");
    }

    // Maps DayOfWeek enum TO display string
    private string MapDayOfWeekToDisplayDay(DayOfWeek dayOfWeek, CultureInfo culture)
    { try { return culture.DateTimeFormat.GetDayName(dayOfWeek); } catch { return dayOfWeek.ToString(); } }

    // Maps display string BACK TO DayOfWeek enum
    private DayOfWeek MapDisplayDayToDayOfWeek(string? displayDay, CultureInfo culture)
    {
        if (string.IsNullOrEmpty(displayDay)) return DateTime.Today.DayOfWeek;
        string[] dayNames = culture.DateTimeFormat.DayNames;
        for (int i = 0; i < dayNames.Length; i++) { if (string.Equals(dayNames[i], displayDay, StringComparison.OrdinalIgnoreCase)) return (DayOfWeek)i; }
        Debug.WriteLine($"Warning: Could not map display day '{displayDay}' back to DayOfWeek."); return DateTime.Today.DayOfWeek;
    }

    // Handles changes for RepetitionType options
    private void SetupOptionChangeHandlers() { foreach (var option in RepetitionTypeOptions) { option.PropertyChanged += HandleOptionPropertyChanged; } }
    private void HandleOptionPropertyChanged(object? sender, PropertyChangedEventArgs args) { if (sender is SelectableOption<TaskRepetitionType> && args.PropertyName == nameof(SelectableOption<TaskRepetitionType>.IsSelected)) RecalculateWeeklyVisibility(); }

    // Handles changes for main TimeType radio buttons
    private void SetupTimeTypeChangeHandlers() { this.PropertyChanged += (sender, args) => { if (args.PropertyName is nameof(IsTimeTypeRepeating) or nameof(IsTimeTypeSpecificDate) or nameof(IsTimeTypeNone)) RecalculateWeeklyVisibility(); }; }

    // Handle changes to the selected *display* day to update the underlying enum
    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e) { if (e.PropertyName == nameof(SelectedDisplayDayOfWeek)) { _selectedDayOfWeekEnum = MapDisplayDayToDayOfWeek(SelectedDisplayDayOfWeek, _calendarCulture); Debug.WriteLine($"SelectedDisplayDayOfWeek changed, mapped enum: {_selectedDayOfWeekEnum}"); } }

    // Updates visibility of weekly picker
    private void RecalculateWeeklyVisibility() { IsWeeklySelected = IsTimeTypeRepeating && (RepetitionTypeOptions.FirstOrDefault(o => o.IsSelected)?.Value == TaskRepetitionType.Weekly); }

    // --- Edit Mode Initialization ---
    public void InitializeForEdit(TaskItem task)
    {
        _taskToEdit = task;
        IsEditMode = true;
        Title = task.Title;
        // Set the Specific Date Picker's date, even if the task is repeating,
        // just so it has a sensible value if the user switches type.
        DueDate = task.DueDate ?? DateTime.Today;

        IsTimeTypeNone = task.TimeType == TaskTimeType.None;
        IsTimeTypeSpecificDate = task.TimeType == TaskTimeType.SpecificDate;
        IsTimeTypeRepeating = task.TimeType == TaskTimeType.Repeating;

        foreach (var option in PriorityOptions) { option.IsSelected = (option.Value == task.Priority); }

        if (IsTimeTypeRepeating) { foreach (var option in RepetitionTypeOptions) { option.IsSelected = (option.Value == (task.RepetitionType ?? TaskRepetitionType.Daily)); } }
        else { foreach (var option in RepetitionTypeOptions) { option.IsSelected = false; } }

        _selectedDayOfWeekEnum = task.RepetitionDayOfWeek ?? DateTime.Today.DayOfWeek;
        SelectedDisplayDayOfWeek = MapDayOfWeekToDisplayDay(_selectedDayOfWeekEnum, _calendarCulture);

        RecalculateWeeklyVisibility();
        Debug.WriteLine($"Initialized ViewModel for Edit: {task.Title}");
    }

    // --- Data Retrieval (MODIFIED) ---
    public TaskItem GetTaskItemFromViewModel()
    {
        var selectedPriority = PriorityOptions.FirstOrDefault(p => p.IsSelected)?.Value ?? TaskPriority.Medium;
        TaskTimeType timeType = TaskTimeType.None;
        DateTime? finalDueDate = null; // Will be calculated/set below
        TaskRepetitionType? repetitionType = null;
        DayOfWeek? repetitionDayOfWeek = null;

        if (IsTimeTypeSpecificDate)
        {
            timeType = TaskTimeType.SpecificDate;
            // Use the date from the dedicated DatePicker for specific dates
            finalDueDate = DueDate.Date; // Use picker's date, strip time
            Debug.WriteLine($"GetTaskItem: Type=SpecificDate, Date={finalDueDate}");
        }
        else if (IsTimeTypeRepeating)
        {
            timeType = TaskTimeType.Repeating;
            repetitionType = RepetitionTypeOptions.FirstOrDefault(r => r.IsSelected)?.Value ?? TaskRepetitionType.Daily;
            repetitionDayOfWeek = (repetitionType == TaskRepetitionType.Weekly) ? _selectedDayOfWeekEnum : null;

            // Calculate initial DueDate for NEW repeating tasks based on logic
            if (!IsEditMode)
            {
                DateTime today = DateTime.Today;
                switch (repetitionType)
                {
                    case TaskRepetitionType.Daily:
                    case TaskRepetitionType.AlternateDay:
                        finalDueDate = today; // Start today
                        break;
                    case TaskRepetitionType.Weekly:
                        // Start on the next occurrence of the selected day, including today
                        finalDueDate = DateUtils.GetNextWeekday(today, _selectedDayOfWeekEnum);
                        break;
                    default:
                        finalDueDate = today; // Fallback
                        break;
                }
                Debug.WriteLine($"GetTaskItem: Type=Repeating (New), Rep={repetitionType}, InitialDate={finalDueDate}");
            }
            else // If EDITING a repeating task
            {
                // Use the existing DueDate from the task being edited.
                // This date only changes when marked done.
                finalDueDate = _taskToEdit?.DueDate?.Date;
                Debug.WriteLine($"GetTaskItem: Type=Repeating (Edit), Rep={repetitionType}, Keeping Date={finalDueDate}");
            }
        }
        else // TimeType is None
        {
            Debug.WriteLine($"GetTaskItem: Type=None");
            finalDueDate = null; // Ensure date is null for 'None' type
        }


        // Update existing task or create new one
        if (IsEditMode && _taskToEdit != null)
        {
            Debug.WriteLine($"GetTaskItem: Updating existing TaskItem: {_taskToEdit.Id}");
            _taskToEdit.Title = Title;
            _taskToEdit.Priority = selectedPriority;
            _taskToEdit.TimeType = timeType;
            // Only update DueDate if type is SpecificDate OR if it was null and now isn't (unlikely edit case)
            if (timeType == TaskTimeType.SpecificDate || (_taskToEdit.DueDate == null && finalDueDate != null))
            {
                _taskToEdit.DueDate = finalDueDate;
            }
            _taskToEdit.RepetitionType = repetitionType;
            _taskToEdit.RepetitionDayOfWeek = repetitionDayOfWeek;
            return _taskToEdit;
        }
        else // Creating NEW task
        {
            Debug.WriteLine($"GetTaskItem: Creating new TaskItem");
            // Use the calculated finalDueDate
            return new TaskItem(Title, selectedPriority, timeType, finalDueDate, repetitionType, repetitionDayOfWeek);
        }
    }

    // Property for Delete confirmation
    public Guid? TaskIdToDelete => _taskToEdit?.Id;

    // Helper to update backing field and raise PropertyChanged if value changed
    protected bool UpdateProperty<T>(ref T field, T value, string propertyName)
    { if (EqualityComparer<T>.Default.Equals(field, value)) return false; field = value; OnPropertyChanged(propertyName); return true; }

} // End of ViewModel Class


// --- Code-Behind (AddTaskPopupPage.xaml.cs) ---
[QueryProperty(nameof(TaskToEdit), "TaskToEdit")]
public partial class AddTaskPopupPage : ContentPage
{
    private readonly AddTaskPopupPageViewModel _viewModel;
    private TaskItem? _taskToEdit;

    // Property to receive edited task via navigation
    public TaskItem? TaskToEdit
    {
        set
        {
            _taskToEdit = value;
            if (_taskToEdit != null && _viewModel != null)
            {
                _viewModel.InitializeForEdit(_taskToEdit);
            }
        }
    }

    // Constructor
    public AddTaskPopupPage()
    {
        InitializeComponent();
        _viewModel = new AddTaskPopupPageViewModel();
        BindingContext = _viewModel;
        Debug.WriteLine("AddTaskPopupPage: Initialized.");
    }

    // Optional: Handle cases where QueryProperty might set before BindingContext is ready
    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        if (_taskToEdit != null && !_viewModel.IsEditMode) // Check if edit init is needed
        {
            _viewModel.InitializeForEdit(_taskToEdit);
            Debug.WriteLine("AddTaskPopupPage: Forced InitializeForEdit in OnNavigatedTo.");
        }
    }

    // Confirm/Update Button Handler
    private async void OnConfirmClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_viewModel.Title))
        { await DisplayAlert("Validation Error", "Task title cannot be empty.", "OK"); return; }

        var taskData = _viewModel.GetTaskItemFromViewModel();
        if (_viewModel.IsEditMode) { WeakReferenceMessenger.Default.Send(new UpdateTaskMessage(taskData)); }
        else { WeakReferenceMessenger.Default.Send(new AddTaskMessage(taskData)); }
        await Shell.Current.Navigation.PopModalAsync();
    }

    // Cancel Button Handler
    private async void OnCancelClicked(object sender, EventArgs e)
    { await Shell.Current.Navigation.PopModalAsync(); }

    // Delete Button Handler
    private async void OnDeleteClicked(object sender, EventArgs e)
    {
        if (!_viewModel.IsEditMode || _viewModel.TaskIdToDelete == null) return;
        bool confirmed = await DisplayAlert("Confirm Delete", $"Delete '{_viewModel.Title}'?", "Delete", "Cancel");
        if (confirmed)
        {
            WeakReferenceMessenger.Default.Send(new DeleteTaskMessage(_viewModel.TaskIdToDelete.Value));
            await Shell.Current.Navigation.PopModalAsync();
        }
    }
} // End of Code-Behind Class