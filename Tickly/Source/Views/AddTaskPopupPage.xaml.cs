// File: Views\AddTaskPopupPage.xaml.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Maui.Controls;
using Tickly.Messages; // Ensure UpdateTaskMessage and DeleteTaskMessage are included
using Tickly.Models;
using System.Diagnostics; // For Debug.WriteLine

namespace Tickly.Views
{
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
        [ObservableProperty] private ObservableCollection<SelectableOption<TaskPriority>> _priorityOptions;
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
            // Initialize Priority options (without color names)
            PriorityOptions = new ObservableCollection<SelectableOption<TaskPriority>>
            {
                new SelectableOption<TaskPriority>("High", TaskPriority.High),
                new SelectableOption<TaskPriority>("Medium", TaskPriority.Medium, true), // Default selected
                new SelectableOption<TaskPriority>("Low", TaskPriority.Low)
            };

            // Initialize Repetition options
            RepetitionTypeOptions = new ObservableCollection<SelectableOption<TaskRepetitionType>>
            {
                new SelectableOption<TaskRepetitionType>("Daily", TaskRepetitionType.Daily, true), // Default selected
                new SelectableOption<TaskRepetitionType>("Alternate Day", TaskRepetitionType.AlternateDay),
                new SelectableOption<TaskRepetitionType>("Weekly", TaskRepetitionType.Weekly)
            };

            // Initialize Display Days of Week
            // Use CultureInfo.InvariantCulture to ensure consistent order regardless of system settings
            var culture = System.Globalization.CultureInfo.InvariantCulture;
            DisplayDaysOfWeek = culture.DateTimeFormat.DayNames.ToList();
            // Set default selected display day based on Today
            SelectedDisplayDayOfWeek = culture.DateTimeFormat.GetDayName(DateTime.Today.DayOfWeek);

            // Handle changes in RepetitionType selection to show/hide DayOfWeek picker
            foreach (var option in RepetitionTypeOptions)
            {
                option.PropertyChanged += (sender, args) =>
                {
                    if (args.PropertyName == nameof(SelectableOption<TaskRepetitionType>.IsSelected))
                    {
                        var changedOption = sender as SelectableOption<TaskRepetitionType>;
                        // If this option became selected, update IsWeeklySelected based on its value
                        if (changedOption != null && changedOption.IsSelected)
                        {
                            IsWeeklySelected = (changedOption.Value == TaskRepetitionType.Weekly);
                        }
                        // Handle case where Weekly might be deselected (though RadioButton group should prevent direct deselection)
                        // Check if the currently selected option *is not* Weekly
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
            // Initial check for DayOfWeek picker visibility
            IsWeeklySelected = RepetitionTypeOptions.FirstOrDefault(o => o.IsSelected)?.Value == TaskRepetitionType.Weekly;

            // Handle changes in the main TimeType selection (None, Specific, Repeating)
            this.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(IsTimeTypeRepeating) || args.PropertyName == nameof(IsTimeTypeSpecificDate) || args.PropertyName == nameof(IsTimeTypeNone))
                {
                    // Ensure DayOfWeek picker visibility is correct based on whether Repeating is selected AND Weekly is chosen
                    IsWeeklySelected = IsTimeTypeRepeating && (RepetitionTypeOptions.FirstOrDefault(o => o.IsSelected)?.Value == TaskRepetitionType.Weekly);
                }
            };
        }

        // Populates the ViewModel fields based on an existing TaskItem (for editing)
        public void LoadFromTask(TaskItem task)
        {
            _originalTask = task; // Store the original task
            IsEditMode = true; // Set EditMode flag
            PageTitle = "Edit Task";
            ConfirmButtonText = "Update Task";

            Title = task.Title; // Load title into ViewModel property

            // Set Priority RadioButton
            foreach (var option in PriorityOptions)
                option.IsSelected = (option.Value == task.Priority);
            if (!PriorityOptions.Any(o => o.IsSelected)) // Ensure default if no match
                PriorityOptions.FirstOrDefault(o => o.Value == TaskPriority.Medium)!.IsSelected = true;

            // Set TimeType RadioButtons
            IsTimeTypeNone = task.TimeType == TaskTimeType.None;
            IsTimeTypeSpecificDate = task.TimeType == TaskTimeType.SpecificDate;
            IsTimeTypeRepeating = task.TimeType == TaskTimeType.Repeating;

            // Set DatePicker value (used for Specific Date or Repeating Start Date)
            DueDate = task.DueDate ?? DateTime.Today; // Use today if null

            // Set Repetition details if applicable
            if (task.TimeType == TaskTimeType.Repeating)
            {
                // Set Repetition Type RadioButton
                foreach (var option in RepetitionTypeOptions)
                    option.IsSelected = (option.Value == task.RepetitionType);
                if (!RepetitionTypeOptions.Any(o => o.IsSelected)) // Ensure default if no match
                    RepetitionTypeOptions.FirstOrDefault(o => o.Value == TaskRepetitionType.Daily)!.IsSelected = true;

                // Set DayOfWeek Picker selected item using display name
                var culture = System.Globalization.CultureInfo.InvariantCulture;
                SelectedDisplayDayOfWeek = culture.DateTimeFormat.GetDayName(task.RepetitionDayOfWeek ?? DateTime.Today.DayOfWeek);

                // Update DayOfWeek picker visibility based on loaded type
                IsWeeklySelected = RepetitionTypeOptions.FirstOrDefault(o => o.IsSelected)?.Value == TaskRepetitionType.Weekly;
            }
            else
            {
                // Reset repetition details if not a repeating task
                RepetitionTypeOptions.FirstOrDefault(o => o.Value == TaskRepetitionType.Daily)!.IsSelected = true;
                var culture = System.Globalization.CultureInfo.InvariantCulture;
                SelectedDisplayDayOfWeek = culture.DateTimeFormat.GetDayName(DateTime.Today.DayOfWeek);
                IsWeeklySelected = false;
            }
        }

        // Method to create or update a TaskItem based on ViewModel state
        public TaskItem? GetTaskItem()
        {
            // --- 1. Validate Input ---
            string title = Title?.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                // Consider how to report validation errors (e.g., return null, throw exception)
                Debug.WriteLine("Validation Error: Task title cannot be empty.");
                return null;
            }

            // --- 2. Read Data from ViewModel ---
            // Priority
            var selectedPriorityOption = PriorityOptions.FirstOrDefault(p => p.IsSelected);
            TaskPriority priority = selectedPriorityOption?.Value ?? TaskPriority.Medium;

            // Time Type
            TaskTimeType timeType = IsTimeTypeSpecificDate ? TaskTimeType.SpecificDate :
                                    IsTimeTypeRepeating ? TaskTimeType.Repeating :
                                    TaskTimeType.None;

            // Due Date
            DateTime? dueDate = null;
            if (timeType == TaskTimeType.SpecificDate || timeType == TaskTimeType.Repeating)
            {
                dueDate = DueDate;
            }

            // Repetition Details
            TaskRepetitionType? repetitionType = null;
            DayOfWeek? repetitionDayOfWeek = null;
            if (timeType == TaskTimeType.Repeating)
            {
                var selectedRepetitionOption = RepetitionTypeOptions.FirstOrDefault(r => r.IsSelected);
                repetitionType = selectedRepetitionOption?.Value ?? TaskRepetitionType.Daily;

                if (repetitionType == TaskRepetitionType.Weekly)
                {
                    // Convert selected display name back to DayOfWeek enum
                    repetitionDayOfWeek = GetDayOfWeekFromDisplayName(SelectedDisplayDayOfWeek);
                }
            }

            // --- 3. Create or Update TaskItem ---
            if (IsEditMode && _originalTask != null) // Updating existing task
            {
                var updatedTask = new TaskItem(
                    title, priority, timeType, dueDate, repetitionType, repetitionDayOfWeek, _originalTask.Order // Keep original order
                )
                {
                    Id = _originalTask.Id // *** CRUCIAL: Assign the original ID ***
                };
                return updatedTask;
            }
            else // Adding new task
            {
                var newTask = new TaskItem(title, priority, timeType, dueDate, repetitionType, repetitionDayOfWeek);
                // Order will be assigned by MainViewModel
                return newTask;
            }
        }

        // Helper to convert display day name back to DayOfWeek enum
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
            return null; // Not found
        }

        // Optional: Reset state if needed when closing or cancelling
        public void Reset()
        {
            Title = string.Empty;
            PriorityOptions.FirstOrDefault(o => o.Value == TaskPriority.Medium)!.IsSelected = true; // Reset priority
            IsTimeTypeNone = true; // Reset TimeType
            IsTimeTypeSpecificDate = false;
            IsTimeTypeRepeating = false;
            DueDate = DateTime.Today;
            RepetitionTypeOptions.FirstOrDefault(o => o.Value == TaskRepetitionType.Daily)!.IsSelected = true; // Reset repetition type
            var culture = System.Globalization.CultureInfo.InvariantCulture;
            SelectedDisplayDayOfWeek = culture.DateTimeFormat.GetDayName(DateTime.Today.DayOfWeek); // Reset day
            IsWeeklySelected = false;

            _originalTask = null;
            IsEditMode = false;
            PageTitle = "Add New Task";
            ConfirmButtonText = "Add Task";

            // Important: Notify UI about all property changes after reset
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
            // Notify about collection changes if necessary (usually not needed for reset)
        }
    }

    // Code-behind for the Add/Edit Task Page
    [QueryProperty(nameof(TaskToEdit), "TaskToEdit")]
    public partial class AddTaskPopupPage : ContentPage
    {
        private AddTaskPopupPageViewModel _viewModel;
        // Remove _editingTask from code-behind, let ViewModel manage it
        // private TaskItem? _editingTask = null;

        // This property receives the TaskItem object during navigation for editing
        public TaskItem TaskToEdit
        {
            set
            {
                if (value != null) // If a TaskItem was passed
                {
                    Debug.WriteLine($"AddTaskPopupPage: Received TaskToEdit with ID: {value.Id}");
                    _viewModel.LoadFromTask(value); // Populate the ViewModel
                }
                else // Navigated without a TaskItem (e.g., adding new, or error)
                {
                    Debug.WriteLine("AddTaskPopupPage: Navigated without TaskToEdit (Add Mode).");
                    _viewModel.Reset(); // Reset ViewModel for adding
                }
            }
        }

        // Constructor
        public AddTaskPopupPage()
        {
            InitializeComponent(); // Standard MAUI XAML initialization
            _viewModel = new AddTaskPopupPageViewModel(); // Create the associated ViewModel
            BindingContext = _viewModel; // Set the page's BindingContext to the ViewModel
            // Initial state is handled by ViewModel constructor and Reset/LoadFromTask
        }

        // Remove PopulateFieldsFromTask - ViewModel handles loading now
        // private void PopulateFieldsFromTask(TaskItem task) { ... }

        // Handles the "Confirm" button click
        private async void OnConfirmClicked(object sender, EventArgs e)
        {
            var taskItem = _viewModel.GetTaskItem(); // Get the prepared TaskItem from ViewModel

            if (taskItem == null)
            {
                // Validation failed in ViewModel, show alert
                await DisplayAlert("Validation Error", "Task title cannot be empty.", "OK");
                return;
            }

            if (_viewModel.IsEditMode) // Check ViewModel's mode
            {
                Debug.WriteLine($"Sending UpdateTaskMessage for Task ID: {taskItem.Id}");
                WeakReferenceMessenger.Default.Send(new UpdateTaskMessage(taskItem));
            }
            else
            {
                Debug.WriteLine($"Sending AddTaskMessage for new task: {taskItem.Title}");
                WeakReferenceMessenger.Default.Send(new AddTaskMessage(taskItem));
            }

            // Close the Popup Page
            await Shell.Current.Navigation.PopModalAsync();
        }

        // Handles the "Cancel" button click
        private async void OnCancelClicked(object sender, EventArgs e)
        {
            // Simply close the popup page without saving/sending messages
            await Shell.Current.Navigation.PopModalAsync();
        }

        // *** ADDED METHOD ***
        // Handles the "Delete" button click (only visible in Edit mode)
        private async void OnDeleteClicked(object sender, EventArgs e)
        {
            // Get the task ID from the ViewModel (which holds the _originalTask)
            var taskToDelete = _viewModel.GetTaskItem(); // GetTaskItem returns the *updated* item but with original ID if editing

            if (_viewModel.IsEditMode && taskToDelete != null)
            {
                // Optional but recommended: Ask for confirmation before deleting
                bool confirmed = await DisplayAlert("Confirm Delete", $"Are you sure you want to delete the task '{taskToDelete.Title}'?", "Yes", "No");
                if (!confirmed)
                {
                    return; // User cancelled the deletion
                }

                Debug.WriteLine($"Sending DeleteTaskMessage for Task ID: {taskToDelete.Id}");
                // Send a message to the MainViewModel to delete this task using its ID
                WeakReferenceMessenger.Default.Send(new DeleteTaskMessage(taskToDelete.Id));

                // Close the Popup Page after sending the message
                await Shell.Current.Navigation.PopModalAsync();
            }
            else
            {
                // This case should ideally not happen because the button's visibility
                // is tied to IsEditMode (which implies _originalTask is not null in VM).
                Debug.WriteLine("OnDeleteClicked triggered but not in Edit Mode or Task ID missing. Cannot delete.");
                await DisplayAlert("Error", "Cannot delete task. No task loaded for editing.", "OK");
            }
        }
        // *** END OF ADDED METHOD ***


        // Optional: Called when navigating away from the page
        protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
        {
            base.OnNavigatedFrom(args);
            // Reset the ViewModel when navigating away, so it's clean for the next time
            // unless navigation is due to Confirm/Delete (handled by PopModalAsync)
            // It might be safer to reset only on 'Cancel' or back navigation if needed.
            // For simplicity, we rely on the QueryProperty setter to manage state on next navigation.
            // _viewModel.Reset(); // Uncomment cautiously if needed
            Debug.WriteLine("AddTaskPopupPage: Navigated From.");
        }

        protected override void OnNavigatedTo(NavigatedToEventArgs args)
        {
            base.OnNavigatedTo(args);
            // The QueryProperty setter (TaskToEdit) handles setup based on navigation parameters.
            Debug.WriteLine($"AddTaskPopupPage: Navigated To. Edit Mode: {_viewModel.IsEditMode}");
        }

    } // End of AddTaskPopupPage class
} // End of namespace