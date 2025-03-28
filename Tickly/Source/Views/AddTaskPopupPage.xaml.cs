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

    [ObservableProperty]
    private DateTime _dueDate = DateTime.Today; // Bound to both DatePickers

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

    // --- DayOfWeek Handling Modifications ---

    // Store the culture info based on settings for reuse
    private readonly CultureInfo _calendarCulture; // Made readonly as it's set in constructor

    // Holds the list of day names to display in the Picker (e.g., "Saturday", "Sunday" or "شنبه", "یکشنبه")
    [ObservableProperty]
    private ObservableCollection<string> _displayDaysOfWeek = new();

    // Holds the currently selected day name *string* from the Picker
    [ObservableProperty]
    private string? _selectedDisplayDayOfWeek;

    // We still need the underlying DayOfWeek enum for storage/logic
    private DayOfWeek _selectedDayOfWeekEnum = DateTime.Today.DayOfWeek;

    // --- Constructor ---
    public AddTaskPopupPageViewModel()
    {
        // Determine culture based on saved setting *at ViewModel creation time*
        _calendarCulture = AppSettings.SelectedCalendarSystem == CalendarSystemType.Persian
                          ? new CultureInfo("fa-IR")
                          : CultureInfo.InvariantCulture; // Use Invariant for consistent Gregorian day names/order

        LoadDisplayDaysOfWeek(); // Populate the picker items based on the determined culture

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
        this.PropertyChanged += ViewModel_PropertyChanged; // Handle changes to SelectedDisplayDayOfWeek

        RecalculateWeeklyVisibility(); // Initial check for weekly picker visibility
    }

    // --- UI Logic Methods ---

    // Populates the DisplayDaysOfWeek collection based on the culture
    private void LoadDisplayDaysOfWeek()
    {
        DisplayDaysOfWeek.Clear();
        string[] dayNames = _calendarCulture.DateTimeFormat.DayNames;

        // Order based on calendar system
        if (AppSettings.SelectedCalendarSystem == CalendarSystemType.Persian)
        {
            // Persian order: Shanbeh (Saturday, index 6) to Jomeh (Friday, index 5)
            // .NET DayNames for fa-IR: [0]یکشنبه, [1]دوشنبه, [2]سه‌شنبه, [3]چهارشنبه, [4]پنجشنبه, [5]جمعه, [6]شنبه
            DisplayDaysOfWeek.Add(dayNames[6]); // Saturday
            DisplayDaysOfWeek.Add(dayNames[0]); // Sunday
            DisplayDaysOfWeek.Add(dayNames[1]); // Monday
            DisplayDaysOfWeek.Add(dayNames[2]); // Tuesday
            DisplayDaysOfWeek.Add(dayNames[3]); // Wednesday
            DisplayDaysOfWeek.Add(dayNames[4]); // Thursday
            DisplayDaysOfWeek.Add(dayNames[5]); // Friday
        }
        else // Gregorian (ordered Sunday to Saturday as per DayOfWeek enum)
        {
            var sortedDays = Enum.GetValues(typeof(DayOfWeek)).Cast<DayOfWeek>().OrderBy(d => (int)d);
            foreach (var day in sortedDays)
            {
                // Use GetDayName for consistency
                DisplayDaysOfWeek.Add(_calendarCulture.DateTimeFormat.GetDayName(day));
            }
        }
        System.Diagnostics.Debug.WriteLine($"Loaded DisplayDaysOfWeek ({_calendarCulture.Name}): {string.Join(", ", DisplayDaysOfWeek)}");
    }

    // Maps DayOfWeek enum TO display string (e.g., DayOfWeek.Saturday -> "Saturday" or "شنبه")
    private string MapDayOfWeekToDisplayDay(DayOfWeek dayOfWeek, CultureInfo culture)
    {
        try
        {
            // Ensure correct name is retrieved based on culture
            return culture.DateTimeFormat.GetDayName(dayOfWeek);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error mapping DayOfWeek {dayOfWeek} to display day: {ex.Message}");
            return dayOfWeek.ToString(); // Fallback to enum name
        }
    }

    // Maps display string BACK TO DayOfWeek enum (e.g., "Saturday" or "شنبه" -> DayOfWeek.Saturday)
    private DayOfWeek MapDisplayDayToDayOfWeek(string? displayDay, CultureInfo culture)
    {
        if (string.IsNullOrEmpty(displayDay))
        {
            Debug.WriteLine($"Warning: MapDisplayDayToDayOfWeek called with null/empty displayDay. Falling back to Today.");
            return DateTime.Today.DayOfWeek; // Default fallback
        }

        string[] dayNames = culture.DateTimeFormat.DayNames;
        for (int i = 0; i < dayNames.Length; i++)
        {
            // Use OrdinalIgnoreCase for robust comparison
            if (string.Equals(dayNames[i], displayDay, StringComparison.OrdinalIgnoreCase))
            {
                // The index 'i' in DateTimeFormat.DayNames corresponds directly to the DayOfWeek enum value
                // (e.g., Sunday is index 0 in the array and DayOfWeek.Sunday is 0)
                return (DayOfWeek)i;
            }
        }
        System.Diagnostics.Debug.WriteLine($"Warning: Could not map display day '{displayDay}' back to DayOfWeek enum using culture {culture.Name}. Falling back to Today.");
        return DateTime.Today.DayOfWeek; // Fallback if no match found
    }


    // --- Event Handlers ---

    private void SetupOptionChangeHandlers()
    {
        foreach (var option in RepetitionTypeOptions)
        {
            option.PropertyChanged += HandleOptionPropertyChanged;
        }
    }

    private void HandleOptionPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (sender is SelectableOption<TaskRepetitionType> && args.PropertyName == nameof(SelectableOption<TaskRepetitionType>.IsSelected))
        {
            RecalculateWeeklyVisibility();
        }
    }

    private void SetupTimeTypeChangeHandlers()
    {
        this.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(IsTimeTypeRepeating) ||
                args.PropertyName == nameof(IsTimeTypeSpecificDate) ||
                args.PropertyName == nameof(IsTimeTypeNone))
            {
                RecalculateWeeklyVisibility();
            }
        };
    }

    // Handle changes to the selected *display* day to update the underlying enum
    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SelectedDisplayDayOfWeek))
        {
            // When the Picker's selected item (string) changes, update the internal enum value
            _selectedDayOfWeekEnum = MapDisplayDayToDayOfWeek(SelectedDisplayDayOfWeek, _calendarCulture);
            Debug.WriteLine($"SelectedDisplayDayOfWeek changed to '{SelectedDisplayDayOfWeek}', mapped to enum: {_selectedDayOfWeekEnum}");
        }
    }


    // Updates visibility of weekly picker
    private void RecalculateWeeklyVisibility()
    {
        IsWeeklySelected = IsTimeTypeRepeating && (RepetitionTypeOptions.FirstOrDefault(o => o.IsSelected)?.Value == TaskRepetitionType.Weekly);
        // Debug.WriteLine($"RecalculateWeeklyVisibility: IsWeeklySelected={IsWeeklySelected}"); // Less verbose debug
    }

    // --- Edit Mode Initialization ---
    public void InitializeForEdit(TaskItem task)
    {
        _taskToEdit = task;
        IsEditMode = true;

        Title = task.Title;
        DueDate = task.DueDate ?? DateTime.Today;

        IsTimeTypeNone = task.TimeType == TaskTimeType.None;
        IsTimeTypeSpecificDate = task.TimeType == TaskTimeType.SpecificDate;
        IsTimeTypeRepeating = task.TimeType == TaskTimeType.Repeating;

        // Set Priority RadioButtons
        foreach (var option in PriorityOptions) { option.IsSelected = (option.Value == task.Priority); }

        // Set RepetitionType RadioButtons
        if (IsTimeTypeRepeating)
        {
            foreach (var option in RepetitionTypeOptions)
            {
                option.IsSelected = (option.Value == (task.RepetitionType ?? TaskRepetitionType.Daily));
            }
        }
        else { foreach (var option in RepetitionTypeOptions) { option.IsSelected = false; } }

        // Initialize DayOfWeek: Set the internal enum and then update the display string for the Picker
        _selectedDayOfWeekEnum = task.RepetitionDayOfWeek ?? DateTime.Today.DayOfWeek;
        // Important: Use the culture loaded by this ViewModel instance
        SelectedDisplayDayOfWeek = MapDayOfWeekToDisplayDay(_selectedDayOfWeekEnum, _calendarCulture);

        RecalculateWeeklyVisibility(); // Ensure correct visibility after loading
    }

    // --- Data Retrieval ---
    public TaskItem GetTaskItemFromViewModel()
    {
        var selectedPriority = PriorityOptions.FirstOrDefault(p => p.IsSelected)?.Value ?? TaskPriority.Medium;

        TaskTimeType timeType = TaskTimeType.None;
        DateTime? finalDueDate = null;
        TaskRepetitionType? repetitionType = null;
        DayOfWeek? repetitionDayOfWeek = null; // Use the stored enum value

        if (IsTimeTypeSpecificDate)
        {
            timeType = TaskTimeType.SpecificDate;
            finalDueDate = DueDate;
        }
        else if (IsTimeTypeRepeating)
        {
            timeType = TaskTimeType.Repeating;
            finalDueDate = DueDate;
            repetitionType = RepetitionTypeOptions.FirstOrDefault(r => r.IsSelected)?.Value ?? TaskRepetitionType.Daily;

            if (repetitionType == TaskRepetitionType.Weekly)
            {
                // Get the DayOfWeek from the internally stored enum value (_selectedDayOfWeekEnum)
                repetitionDayOfWeek = _selectedDayOfWeekEnum;
            }
        }

        // Update existing task or create new one
        if (IsEditMode && _taskToEdit != null)
        {
            _taskToEdit.Title = Title;
            _taskToEdit.Priority = selectedPriority;
            _taskToEdit.TimeType = timeType;
            _taskToEdit.DueDate = finalDueDate;
            _taskToEdit.RepetitionType = repetitionType;
            _taskToEdit.RepetitionDayOfWeek = repetitionDayOfWeek; // Save the enum value
            return _taskToEdit;
        }
        else
        {
            return new TaskItem(Title, selectedPriority, timeType, finalDueDate, repetitionType, repetitionDayOfWeek); // Save the enum value
        }
    }

    // Property to access the ID for deletion confirmation
    public Guid? TaskIdToDelete => _taskToEdit?.Id;
}


// --- Code-Behind for the Add/Edit Task Page ---
// Connects the XAML UI to the ViewModel and handles navigation events.
[QueryProperty(nameof(TaskToEdit), "TaskToEdit")]
public partial class AddTaskPopupPage : ContentPage
{
    // Use readonly if ViewModel is only assigned in constructor
    private readonly AddTaskPopupPageViewModel _viewModel;

    // Property that receives the TaskItem object when navigating for editing
    private TaskItem? _taskToEdit;
    public TaskItem? TaskToEdit
    {
        set
        {
            _taskToEdit = value;
            // Ensure ViewModel exists before trying to initialize
            if (_taskToEdit != null && _viewModel != null)
            {
                Debug.WriteLine($"AddTaskPopupPage: Received task for edit via QueryProperty: {_taskToEdit.Title}");
                _viewModel.InitializeForEdit(_taskToEdit);
            }
            else if (_taskToEdit == null && value != null)
            {
                Debug.WriteLine("AddTaskPopupPage: Received non-null value for TaskToEdit, but cast failed or ViewModel null.");
            }
        }
    }

    // --- Constructor ---
    public AddTaskPopupPage()
    {
        InitializeComponent();
        // Create and assign the ViewModel
        _viewModel = new AddTaskPopupPageViewModel();
        BindingContext = _viewModel;
        Debug.WriteLine("AddTaskPopupPage: Initialized and ViewModel created.");
    }

    // --- Lifecycle Overrides ---
    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        Debug.WriteLine($"AddTaskPopupPage: NavigatedTo. IsEditMode = {_viewModel.IsEditMode}");
        // Safety check in case QueryProperty setter runs before ViewModel is fully ready
        // or if navigation happens in a way that bypasses QueryProperty initially.
        if (_taskToEdit != null && !_viewModel.IsEditMode)
        {
            Debug.WriteLine("AddTaskPopupPage: OnNavigatedTo - Forcing InitializeForEdit.");
            _viewModel.InitializeForEdit(_taskToEdit);
        }
    }

    // --- Event Handlers for Buttons ---

    // Handles the "Confirm" or "Update" button click
    private async void OnConfirmClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_viewModel.Title))
        {
            await DisplayAlert("Validation Error", "Task title cannot be empty.", "OK");
            return;
        }

        var taskData = _viewModel.GetTaskItemFromViewModel();

        if (_viewModel.IsEditMode)
        {
            WeakReferenceMessenger.Default.Send(new UpdateTaskMessage(taskData));
            Debug.WriteLine($"Sent UpdateTaskMessage for ID: {taskData.Id}");
        }
        else
        {
            WeakReferenceMessenger.Default.Send(new AddTaskMessage(taskData));
            Debug.WriteLine($"Sent AddTaskMessage for Title: {taskData.Title}");
        }

        await Shell.Current.Navigation.PopModalAsync();
    }

    // Handles the "Cancel" button click
    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await Shell.Current.Navigation.PopModalAsync();
    }

    // Handles the "Delete" button click
    private async void OnDeleteClicked(object sender, EventArgs e)
    {
        if (!_viewModel.IsEditMode || _viewModel.TaskIdToDelete == null)
        {
            Debug.WriteLine("Delete clicked, but not in edit mode or TaskId is null.");
            return;
        }

        bool confirmed = await DisplayAlert(
            "Confirm Delete",
            $"Are you sure you want to delete the task '{_viewModel.Title}'?",
            "Delete",
            "Cancel"
        );

        if (confirmed)
        {
            WeakReferenceMessenger.Default.Send(new DeleteTaskMessage(_viewModel.TaskIdToDelete.Value));
            Debug.WriteLine($"Sent DeleteTaskMessage for ID: {_viewModel.TaskIdToDelete.Value}");
            await Shell.Current.Navigation.PopModalAsync();
        }
    }
}