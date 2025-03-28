// Views/AddTaskPopupPage.xaml.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel; // Required for PropertyChangedEventArgs
using System.Diagnostics;
using System.Linq;
using Tickly.Messages; // Contains AddTaskMessage, UpdateTaskMessage, DeleteTaskMessage
using Tickly.Models;   // Contains TaskItem and enums

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

    // Data source and selection for DayOfWeek Picker
    [ObservableProperty]
    private List<DayOfWeek> _daysOfWeek = Enum.GetValues(typeof(DayOfWeek)).Cast<DayOfWeek>().ToList();
    [ObservableProperty]
    private DayOfWeek _selectedDayOfWeek = DateTime.Today.DayOfWeek; // Default to today

    // --- Constructor ---
    public AddTaskPopupPageViewModel()
    {
        // Initialize Priority options
        PriorityOptions = new ObservableCollection<SelectableOption<TaskPriority>>
        {
            new SelectableOption<TaskPriority>("High (Red)", TaskPriority.High),
            // Default to Medium only when *adding* a new task
            new SelectableOption<TaskPriority>("Medium (Orange)", TaskPriority.Medium, !IsEditMode),
            new SelectableOption<TaskPriority>("Low (Green)", TaskPriority.Low)
        };

        // Initialize Repetition Type options
        RepetitionTypeOptions = new ObservableCollection<SelectableOption<TaskRepetitionType>>
        {
             // Default to Daily only when *adding* a new task and repeating is chosen
            new SelectableOption<TaskRepetitionType>("Daily", TaskRepetitionType.Daily, !IsEditMode),
            new SelectableOption<TaskRepetitionType>("Alternate Day", TaskRepetitionType.AlternateDay),
            new SelectableOption<TaskRepetitionType>("Weekly", TaskRepetitionType.Weekly)
        };

        // Set up event handlers to react to changes in selections
        SetupOptionChangeHandlers();
        SetupTimeTypeChangeHandlers();
        RecalculateWeeklyVisibility(); // Initial check
    }

    // --- UI Logic Methods ---

    // Subscribes to PropertyChanged events for selectable options
    private void SetupOptionChangeHandlers()
    {
        // Could add Priority changes here later if needed
        // foreach (var option in PriorityOptions) { option.PropertyChanged += HandleOptionPropertyChanged; }

        // React when a RepetitionType option's IsSelected changes
        foreach (var option in RepetitionTypeOptions)
        {
            option.PropertyChanged += HandleOptionPropertyChanged;
        }
    }

    // Handles changes in IsSelected for RepetitionType options
    private void HandleOptionPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        // Check if the 'IsSelected' property changed for a RepetitionType option
        if (sender is SelectableOption<TaskRepetitionType> && args.PropertyName == nameof(SelectableOption<TaskRepetitionType>.IsSelected))
        {
            // If an option became selected, recalculate if the Weekly picker should be shown
            var changedOption = (SelectableOption<TaskRepetitionType>)sender;
            if (changedOption.IsSelected)
            {
                RecalculateWeeklyVisibility();
            }
        }
    }

    // Subscribes to PropertyChanged events for the main TimeType radio buttons
    private void SetupTimeTypeChangeHandlers()
    {
        this.PropertyChanged += (sender, args) =>
        {
            // If any of the TimeType boolean flags change...
            if (args.PropertyName == nameof(IsTimeTypeRepeating) ||
                args.PropertyName == nameof(IsTimeTypeSpecificDate) ||
                args.PropertyName == nameof(IsTimeTypeNone))
            {
                // ...recalculate if the Weekly picker should be visible
                RecalculateWeeklyVisibility();
            }
        };
    }

    // Updates the IsWeeklySelected property based on current selections
    private void RecalculateWeeklyVisibility()
    {
        // Show the DayOfWeek picker only if 'Repeating' is selected AND the selected repetition type is 'Weekly'
        IsWeeklySelected = IsTimeTypeRepeating && (RepetitionTypeOptions.FirstOrDefault(o => o.IsSelected)?.Value == TaskRepetitionType.Weekly);
        // Debug.WriteLine($"RecalculateWeeklyVisibility: IsTimeTypeRepeating={IsTimeTypeRepeating}, IsWeeklySelected={IsWeeklySelected}");
    }

    // --- Edit Mode Initialization ---

    // Populates the ViewModel with data from an existing TaskItem for editing
    public void InitializeForEdit(TaskItem task)
    {
        _taskToEdit = task;
        IsEditMode = true; // Set mode to Edit

        Debug.WriteLine($"Initializing ViewModel for Edit: {task.Title}");

        // Set simple properties
        Title = task.Title;
        DueDate = task.DueDate ?? DateTime.Today; // Use Today if DueDate was null

        // Set Time Type radio buttons based on the task's TimeType
        IsTimeTypeNone = task.TimeType == TaskTimeType.None;
        IsTimeTypeSpecificDate = task.TimeType == TaskTimeType.SpecificDate;
        IsTimeTypeRepeating = task.TimeType == TaskTimeType.Repeating;

        // Set Priority radio button selection
        foreach (var option in PriorityOptions)
        {
            option.IsSelected = (option.Value == task.Priority);
        }

        // Set Repetition Type radio button selection (only if repeating)
        if (IsTimeTypeRepeating)
        {
            foreach (var option in RepetitionTypeOptions)
            {
                // Mark the correct repetition type as selected, default handled if null
                option.IsSelected = (option.Value == (task.RepetitionType ?? TaskRepetitionType.Daily));
            }
        }
        else // Ensure repetition options are cleared if not repeating
        {
            foreach (var option in RepetitionTypeOptions) { option.IsSelected = false; }
        }


        // Set Day of Week picker (only relevant if weekly)
        SelectedDayOfWeek = task.RepetitionDayOfWeek ?? DateTime.Today.DayOfWeek; // Default to today if null

        // Ensure UI visibility is correct after setting values
        RecalculateWeeklyVisibility();
    }

    // --- Data Retrieval ---

    // Creates or updates a TaskItem based on the current ViewModel state
    public TaskItem GetTaskItemFromViewModel()
    {
        // Get selected priority (default to Medium if somehow none selected)
        var selectedPriority = PriorityOptions.FirstOrDefault(p => p.IsSelected)?.Value ?? TaskPriority.Medium;

        // Determine TimeType and related data based on radio button selections
        TaskTimeType timeType = TaskTimeType.None;
        DateTime? finalDueDate = null; // Use bound DueDate property
        TaskRepetitionType? repetitionType = null;
        DayOfWeek? repetitionDayOfWeek = null;

        if (IsTimeTypeSpecificDate)
        {
            timeType = TaskTimeType.SpecificDate;
            finalDueDate = DueDate;
        }
        else if (IsTimeTypeRepeating)
        {
            timeType = TaskTimeType.Repeating;
            finalDueDate = DueDate; // Repeating tasks also use the DueDate property as their start date
            repetitionType = RepetitionTypeOptions.FirstOrDefault(r => r.IsSelected)?.Value ?? TaskRepetitionType.Daily; // Default to Daily

            if (repetitionType == TaskRepetitionType.Weekly)
            {
                repetitionDayOfWeek = SelectedDayOfWeek; // Use bound SelectedDayOfWeek
            }
        }
        // If IsTimeTypeNone is true, defaults (None, null, null, null) are correct

        // If editing, update the existing TaskItem object
        if (IsEditMode && _taskToEdit != null)
        {
            Debug.WriteLine($"Updating existing TaskItem: {_taskToEdit.Id}");
            _taskToEdit.Title = Title;
            _taskToEdit.Priority = selectedPriority;
            _taskToEdit.TimeType = timeType;
            _taskToEdit.DueDate = finalDueDate;
            _taskToEdit.RepetitionType = repetitionType;
            _taskToEdit.RepetitionDayOfWeek = repetitionDayOfWeek;
            // Order is managed by MainViewModel on save/load/move
            return _taskToEdit;
        }
        else // If adding, create a new TaskItem object
        {
            Debug.WriteLine("Creating new TaskItem");
            return new TaskItem(
                Title,
                selectedPriority,
                timeType,
                finalDueDate,
                repetitionType,
                repetitionDayOfWeek
            // Order will be set by MainViewModel when added
            );
        }
    }

    // Provides the ID of the task being edited, used for deletion confirmation/message
    public Guid? TaskIdToDelete => _taskToEdit?.Id;
}


// --- Code-Behind for the Add/Edit Task Page ---
// Connects the XAML UI to the ViewModel and handles navigation events.

// Attribute to receive data passed during navigation
[QueryProperty(nameof(TaskToEdit), "TaskToEdit")] // "TaskToEdit" must match the key used in MainViewModel's GoToAsync call
public partial class AddTaskPopupPage : ContentPage
{
    private readonly AddTaskPopupPageViewModel _viewModel;

    // Property that receives the TaskItem object when navigating for editing
    private TaskItem? _taskToEdit;
    public TaskItem? TaskToEdit // Must be nullable or handle null appropriately
    {
        // This setter is called by the navigation system when the QueryProperty is matched
        set
        {
            _taskToEdit = value; // Store the passed object (might be null if navigating for Add)
            if (_taskToEdit != null && _viewModel != null)
            {
                Debug.WriteLine($"AddTaskPopupPage: Received task for edit via QueryProperty: {_taskToEdit.Title}");
                // If we received a task, tell the ViewModel to initialize itself for editing
                // Ensure ViewModel is ready before calling this.
                _viewModel.InitializeForEdit(_taskToEdit);
            }
            else if (_taskToEdit == null && value != null)
            {
                // This shouldn't happen if the key matches and the object is correct
                Debug.WriteLine("AddTaskPopupPage: Received non-null value for TaskToEdit, but cast failed or ViewModel null.");
            }
            // If value is null, we are in Add mode, ViewModel defaults are fine.
        }
    }

    // --- Constructor ---
    public AddTaskPopupPage()
    {
        InitializeComponent();
        _viewModel = new AddTaskPopupPageViewModel(); // Create the ViewModel instance
        BindingContext = _viewModel; // Set the BindingContext so XAML bindings work
        Debug.WriteLine("AddTaskPopupPage: Initialized and ViewModel created.");
    }

    // --- Lifecycle Overrides (Optional but can be useful) ---
    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        Debug.WriteLine($"AddTaskPopupPage: NavigatedTo. IsEditMode = {_viewModel.IsEditMode}");
        // Safety check: If TaskToEdit was set *before* the page/ViewModel was fully ready,
        // ensure InitializeForEdit is called now.
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
        // Basic validation
        if (string.IsNullOrWhiteSpace(_viewModel.Title))
        {
            await DisplayAlert("Validation Error", "Task title cannot be empty.", "OK");
            return;
        }

        // Get the TaskItem data (either new or updated) from the ViewModel
        var taskData = _viewModel.GetTaskItemFromViewModel();

        if (_viewModel.IsEditMode)
        {
            // Send an Update message if editing
            WeakReferenceMessenger.Default.Send(new UpdateTaskMessage(taskData));
            Debug.WriteLine($"Sent UpdateTaskMessage for ID: {taskData.Id}");
        }
        else
        {
            // Send an Add message if adding
            WeakReferenceMessenger.Default.Send(new AddTaskMessage(taskData));
            Debug.WriteLine($"Sent AddTaskMessage for Title: {taskData.Title}");
        }

        // Close the modal popup page
        await Shell.Current.Navigation.PopModalAsync();
    }

    // Handles the "Cancel" button click
    private async void OnCancelClicked(object sender, EventArgs e)
    {
        // Simply close the modal popup page without saving
        await Shell.Current.Navigation.PopModalAsync();
    }

    // Handles the "Delete" button click (only visible in Edit mode)
    private async void OnDeleteClicked(object sender, EventArgs e)
    {
        // Ensure we are in edit mode and have a task ID
        if (!_viewModel.IsEditMode || _viewModel.TaskIdToDelete == null)
        {
            Debug.WriteLine("Delete clicked, but not in edit mode or TaskId is null.");
            return;
        }

        // Confirm with the user before deleting
        bool confirmed = await DisplayAlert(
            "Confirm Delete",
            $"Are you sure you want to delete the task '{_viewModel.Title}'?", // Use ViewModel title for confirmation message
            "Delete", // Accept button text
            "Cancel"  // Cancel button text
        );

        if (confirmed)
        {
            // Send a Delete message with the task's ID
            WeakReferenceMessenger.Default.Send(new DeleteTaskMessage(_viewModel.TaskIdToDelete.Value));
            Debug.WriteLine($"Sent DeleteTaskMessage for ID: {_viewModel.TaskIdToDelete.Value}");

            // Close the modal popup page
            await Shell.Current.Navigation.PopModalAsync();
        }
    }
}