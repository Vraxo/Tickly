// Views/AddTaskPopupPage.xaml.cs
using CommunityToolkit.Mvvm.ComponentModel; // For ObservableObject base if needed
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Maui.Controls;
using Tickly.Messages;
using Tickly.Models;

namespace Tickly.Views;

// Simple class to help with RadioButton binding in collections
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

// Use ObservableObject for binding properties controlling visibility etc.
public partial class AddTaskPopupPageViewModel : ObservableObject
{
    [ObservableProperty]
    private DateTime _dueDate = DateTime.Today;

    [ObservableProperty]
    private bool _isTimeTypeNone = true;
    [ObservableProperty]
    private bool _isTimeTypeSpecificDate;
    [ObservableProperty]
    private bool _isTimeTypeRepeating;

    [ObservableProperty]
    private ObservableCollection<SelectableOption<TaskPriority>> _priorityOptions;

    [ObservableProperty]
    private ObservableCollection<SelectableOption<TaskRepetitionType>> _repetitionTypeOptions;

    [ObservableProperty]
    private bool _isWeeklySelected; // To show/hide DayOfWeekPicker

    [ObservableProperty]
    private List<DayOfWeek> _daysOfWeek = Enum.GetValues(typeof(DayOfWeek)).Cast<DayOfWeek>().ToList();

    [ObservableProperty]
    private DayOfWeek _selectedDayOfWeek = DateTime.Today.DayOfWeek; // Default to today


    public AddTaskPopupPageViewModel()
    {
        PriorityOptions = new ObservableCollection<SelectableOption<TaskPriority>>
        {
            new SelectableOption<TaskPriority>("High (Red)", TaskPriority.High),
            new SelectableOption<TaskPriority>("Medium (Orange)", TaskPriority.Medium, true), // Default selected
            new SelectableOption<TaskPriority>("Low (Green)", TaskPriority.Low)
        };

        RepetitionTypeOptions = new ObservableCollection<SelectableOption<TaskRepetitionType>>
        {
            new SelectableOption<TaskRepetitionType>("Daily", TaskRepetitionType.Daily, true), // Default selected
            new SelectableOption<TaskRepetitionType>("Alternate Day", TaskRepetitionType.AlternateDay),
            new SelectableOption<TaskRepetitionType>("Weekly", TaskRepetitionType.Weekly)
        };

        // Update IsWeeklySelected when repetition options change
        foreach (var option in RepetitionTypeOptions)
        {
            option.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(SelectableOption<TaskRepetitionType>.IsSelected))
                {
                    var changedOption = sender as SelectableOption<TaskRepetitionType>;
                    // If this option became selected and it's Weekly, update IsWeeklySelected
                    if (changedOption != null && changedOption.IsSelected && changedOption.Value == TaskRepetitionType.Weekly)
                    {
                        IsWeeklySelected = true;
                    }
                    // If this specific option is Weekly and it became *un*selected,
                    // check if *any* other weekly option is selected (shouldn't happen with RadioButtons, but safe check)
                    // More simply: if the selected one is NOT weekly, set IsWeeklySelected to false.
                    else if (changedOption != null && changedOption.IsSelected && changedOption.Value != TaskRepetitionType.Weekly)
                    {
                        IsWeeklySelected = false;
                    }
                    // Handle the initial default selection case for Weekly
                    else if (RepetitionTypeOptions.FirstOrDefault(o => o.IsSelected)?.Value != TaskRepetitionType.Weekly)
                    {
                        IsWeeklySelected = false;
                    }
                }
            };
        }
        // Initial check
        IsWeeklySelected = RepetitionTypeOptions.FirstOrDefault(o => o.IsSelected)?.Value == TaskRepetitionType.Weekly;

        // Also react to main Time Type radio buttons changing
        this.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(IsTimeTypeRepeating))
            {
                // Recalculate IsWeeklySelected visibility when Repeating section is shown/hidden
                IsWeeklySelected = IsTimeTypeRepeating && (RepetitionTypeOptions.FirstOrDefault(o => o.IsSelected)?.Value == TaskRepetitionType.Weekly);
            }
            else if (args.PropertyName == nameof(IsTimeTypeSpecificDate) || args.PropertyName == nameof(IsTimeTypeNone))
            {
                // Ensure IsWeeklySelected is false if not repeating
                if (!IsTimeTypeRepeating) IsWeeklySelected = false;
            }
        };
    }
}


public partial class AddTaskPopupPage : ContentPage
{
    private AddTaskPopupPageViewModel _viewModel;

    public AddTaskPopupPage()
    {
        InitializeComponent();
        _viewModel = new AddTaskPopupPageViewModel();
        BindingContext = _viewModel;

        // Manually set initial DatePicker dates if needed, though binding should handle it
        DueDatePicker.Date = _viewModel.DueDate;
        RepeatStartDatePicker.Date = _viewModel.DueDate;
        DayOfWeekPicker.SelectedItem = _viewModel.SelectedDayOfWeek;
    }

    private async void OnConfirmClicked(object sender, EventArgs e)
    {
        string title = TitleEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            await DisplayAlert("Validation Error", "Task title cannot be empty.", "OK");
            return;
        }

        // Get selected priority
        var selectedPriorityOption = _viewModel.PriorityOptions.FirstOrDefault(p => p.IsSelected);
        TaskPriority priority = selectedPriorityOption?.Value ?? TaskPriority.Medium; // Default if somehow none selected

        // Determine TimeType and related data
        TaskTimeType timeType = TaskTimeType.None;
        DateTime? dueDate = null;
        TaskRepetitionType? repetitionType = null;
        DayOfWeek? repetitionDayOfWeek = null;

        if (_viewModel.IsTimeTypeSpecificDate)
        {
            timeType = TaskTimeType.SpecificDate;
            dueDate = DueDatePicker.Date;
        }
        else if (_viewModel.IsTimeTypeRepeating)
        {
            timeType = TaskTimeType.Repeating;
            dueDate = RepeatStartDatePicker.Date; // Use the start date picker for repeating tasks
            var selectedRepetitionOption = _viewModel.RepetitionTypeOptions.FirstOrDefault(r => r.IsSelected);
            repetitionType = selectedRepetitionOption?.Value ?? TaskRepetitionType.Daily; // Default

            if (repetitionType == TaskRepetitionType.Weekly)
            {
                repetitionDayOfWeek = (DayOfWeek?)DayOfWeekPicker.SelectedItem ?? _viewModel.SelectedDayOfWeek; // Get selected day or default
            }
        }

        // Create the task item (Order will be set by the MainViewModel)
        var newTask = new TaskItem(
            title,
            priority,
            timeType,
            dueDate,
            repetitionType,
            repetitionDayOfWeek
        );

        // Send message and close popup
        WeakReferenceMessenger.Default.Send(new AddTaskMessage(newTask));
        await Shell.Current.Navigation.PopModalAsync();
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await Shell.Current.Navigation.PopModalAsync();
    }
}