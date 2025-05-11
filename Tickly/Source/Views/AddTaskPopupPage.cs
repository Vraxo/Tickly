using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Maui.Markup;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Tickly.Messages;
using Tickly.Models;
using static CommunityToolkit.Maui.Markup.GridRowsColumns;

namespace Tickly.Views;

// Helper class for binding collections to RadioButtons (Remains the same)
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

// ViewModel for the Add/Edit Task Page (Remains the same)
public partial class AddTaskPopupPageViewModel : ObservableObject
{
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private DateTime _dueDate = DateTime.Today;
    [ObservableProperty] private bool _isTimeTypeNone = true;
    [ObservableProperty] private bool _isTimeTypeSpecificDate;
    [ObservableProperty] private bool _isTimeTypeRepeating;
    [ObservableProperty] private ObservableCollection<SelectableOption<TaskRepetitionType>> _repetitionTypeOptions;
    [ObservableProperty] private bool _isWeeklySelected;
    [ObservableProperty] private List<string> _displayDaysOfWeek;
    [ObservableProperty] private string _selectedDisplayDayOfWeek = string.Empty;

    private TaskItem? _originalTask;

    [ObservableProperty] private bool _isEditMode;
    [ObservableProperty] private string _pageTitle = "Add New Task";
    [ObservableProperty] private string _confirmButtonText = "Add Task";

    public AddTaskPopupPageViewModel()
    {
        RepetitionTypeOptions = new ObservableCollection<SelectableOption<TaskRepetitionType>>
        {
            new SelectableOption<TaskRepetitionType>("Daily", TaskRepetitionType.Daily, true),
            new SelectableOption<TaskRepetitionType>("Alternate Day", TaskRepetitionType.AlternateDay),
            new SelectableOption<TaskRepetitionType>("Weekly", TaskRepetitionType.Weekly)
        };

        var culture = CultureInfo.InvariantCulture;
        DisplayDaysOfWeek = culture.DateTimeFormat.DayNames.ToList();
        SelectedDisplayDayOfWeek = culture.DateTimeFormat.GetDayName(DateTime.Today.DayOfWeek);

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
        IsTimeTypeNone = task.TimeType == TaskTimeType.None;
        IsTimeTypeSpecificDate = task.TimeType == TaskTimeType.SpecificDate;
        IsTimeTypeRepeating = task.TimeType == TaskTimeType.Repeating;
        DueDate = task.DueDate ?? DateTime.Today;

        if (task.TimeType == TaskTimeType.Repeating)
        {
            foreach (var option in RepetitionTypeOptions) option.IsSelected = (option.Value == task.RepetitionType);
            if (!RepetitionTypeOptions.Any(o => o.IsSelected)) RepetitionTypeOptions.FirstOrDefault(o => o.Value == TaskRepetitionType.Daily)!.IsSelected = true;
            var culture = CultureInfo.InvariantCulture;
            SelectedDisplayDayOfWeek = culture.DateTimeFormat.GetDayName(task.RepetitionDayOfWeek ?? DateTime.Today.DayOfWeek);
            IsWeeklySelected = RepetitionTypeOptions.FirstOrDefault(o => o.IsSelected)?.Value == TaskRepetitionType.Weekly;
        }
        else
        {
            RepetitionTypeOptions.FirstOrDefault(o => o.Value == TaskRepetitionType.Daily)!.IsSelected = true;
            var culture = CultureInfo.InvariantCulture;
            SelectedDisplayDayOfWeek = culture.DateTimeFormat.GetDayName(DateTime.Today.DayOfWeek);
            IsWeeklySelected = false;
        }
    }

    public TaskItem? GetTaskItem()
    {
        string? currentTitle = Title?.Trim();
        if (string.IsNullOrWhiteSpace(currentTitle))
        {
            return null;
        }

        TaskTimeType timeType = IsTimeTypeSpecificDate ? TaskTimeType.SpecificDate :
                                IsTimeTypeRepeating ? TaskTimeType.Repeating :
                                TaskTimeType.None;

        DateTime? finalDueDate = null;
        if (timeType == TaskTimeType.SpecificDate || timeType == TaskTimeType.Repeating)
        {
            finalDueDate = DueDate;
        }

        TaskRepetitionType? repetitionType = null;
        DayOfWeek? repetitionDayOfWeek = null;
        if (timeType == TaskTimeType.Repeating)
        {
            var selectedRepetitionOption = RepetitionTypeOptions.FirstOrDefault(r => r.IsSelected);
            repetitionType = selectedRepetitionOption?.Value ?? TaskRepetitionType.Daily;
            if (repetitionType == TaskRepetitionType.Weekly) repetitionDayOfWeek = GetDayOfWeekFromDisplayName(SelectedDisplayDayOfWeek);
        }

        if (IsEditMode && _originalTask != null)
        {
            return new TaskItem(currentTitle, timeType, finalDueDate, repetitionType, repetitionDayOfWeek, _originalTask.Order)
            {
                Id = _originalTask.Id,
                Index = _originalTask.Index
            };
        }
        else
        {
            return new TaskItem(currentTitle, timeType, finalDueDate, repetitionType, repetitionDayOfWeek);
        }
    }

    private DayOfWeek? GetDayOfWeekFromDisplayName(string displayName)
    {
        var culture = CultureInfo.InvariantCulture;
        for (int i = 0; i < culture.DateTimeFormat.DayNames.Length; i++)
        {
            if (culture.DateTimeFormat.DayNames[i].Equals(displayName, StringComparison.OrdinalIgnoreCase)) return (DayOfWeek)i;
        }
        return null;
    }

    public void Reset()
    {
        Title = string.Empty;
        IsTimeTypeNone = true;
        IsTimeTypeSpecificDate = false;
        IsTimeTypeRepeating = false;
        DueDate = DateTime.Today;
        RepetitionTypeOptions.FirstOrDefault(o => o.Value == TaskRepetitionType.Daily)!.IsSelected = true;
        var culture = CultureInfo.InvariantCulture;
        SelectedDisplayDayOfWeek = culture.DateTimeFormat.GetDayName(DateTime.Today.DayOfWeek);
        IsWeeklySelected = false;

        _originalTask = null;
        IsEditMode = false;
        PageTitle = "Add New Task";
        ConfirmButtonText = "Add Task";

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
    private readonly AddTaskPopupPageViewModel _viewModel;

    public TaskItem TaskToEdit
    {
        set
        {
            if (value != null)
            {
                _viewModel.LoadFromTask(value);
            }
            else
            {
                _viewModel.Reset();
            }
        }
    }

    public AddTaskPopupPage()
    {
        _viewModel = new AddTaskPopupPageViewModel();
        BindingContext = _viewModel;

        this.Bind(TitleProperty, nameof(AddTaskPopupPageViewModel.PageTitle));
        this.SetDynamicResource(BackgroundColorProperty, "AppBackgroundColor");

        // --- UI Elements ---

        var titleLabel = new Label { Text = "Task Title", Margin = new Thickness(0, 0, 0, 2) };
        titleLabel.SetDynamicResource(Label.TextColorProperty, "AppForegroundColor"); // BaseLabelStyle
        titleLabel.FontSize = 14;

        var titleEntry = new Entry { Placeholder = "Enter task title", Margin = new Thickness(0, 0, 0, 5) };
        titleEntry.SetDynamicResource(Entry.TextColorProperty, "AppForegroundColor");
        titleEntry.SetDynamicResource(Entry.PlaceholderColorProperty, "AppSecondaryTextColor");
        titleEntry.BackgroundColor = Colors.Transparent;
        titleEntry.Bind(Entry.TextProperty, nameof(AddTaskPopupPageViewModel.Title));

        var timeRepetitionLabel = new Label { Text = "Time / Repetition", Margin = new Thickness(0, 0, 0, 2) };
        timeRepetitionLabel.SetDynamicResource(Label.TextColorProperty, "AppForegroundColor");
        timeRepetitionLabel.FontSize = 14;

        var timeTypeNoneRb = new RadioButton { GroupName = "TimeTypeGroup", Content = "None (Any time)" };
        timeTypeNoneRb.SetDynamicResource(RadioButton.TextColorProperty, "AppForegroundColor");
        timeTypeNoneRb.BackgroundColor = Colors.Transparent;
        timeTypeNoneRb.Bind(RadioButton.IsCheckedProperty, nameof(AddTaskPopupPageViewModel.IsTimeTypeNone));

        var timeTypeSpecificDateRb = new RadioButton { GroupName = "TimeTypeGroup", Content = "Specific Date" };
        timeTypeSpecificDateRb.SetDynamicResource(RadioButton.TextColorProperty, "AppForegroundColor");
        timeTypeSpecificDateRb.BackgroundColor = Colors.Transparent;
        timeTypeSpecificDateRb.Bind(RadioButton.IsCheckedProperty, nameof(AddTaskPopupPageViewModel.IsTimeTypeSpecificDate));

        var timeTypeRepeatingRb = new RadioButton { GroupName = "TimeTypeGroup", Content = "Repeating" };
        timeTypeRepeatingRb.SetDynamicResource(RadioButton.TextColorProperty, "AppForegroundColor");
        timeTypeRepeatingRb.BackgroundColor = Colors.Transparent;
        timeTypeRepeatingRb.Bind(RadioButton.IsCheckedProperty, nameof(AddTaskPopupPageViewModel.IsTimeTypeRepeating));

        var dueDatePicker = new DatePicker { Margin = new Thickness(20, 0, 0, 0) };
        dueDatePicker.SetDynamicResource(DatePicker.TextColorProperty, "AppForegroundColor");
        dueDatePicker.SetDynamicResource(DatePicker.BackgroundColorProperty, "AppSurfaceColor");
        dueDatePicker.Bind(DatePicker.DateProperty, nameof(AddTaskPopupPageViewModel.DueDate));
        dueDatePicker.Bind(DatePicker.IsVisibleProperty, nameof(AddTaskPopupPageViewModel.IsTimeTypeSpecificDate));

        var repeatLabel = new Label { Text = "Repeat:", Margin = new Thickness(0, 0, 0, 2) };
        repeatLabel.SetDynamicResource(Label.TextColorProperty, "AppSecondaryTextColor"); // Specific color
        repeatLabel.FontSize = 14;

        var repetitionRadioButtonsLayout = new HorizontalStackLayout { Spacing = 10 };
        foreach (var option in _viewModel.RepetitionTypeOptions)
        {
            var rb = new RadioButton { GroupName = "RepetitionTypeGroup" };
            rb.SetDynamicResource(RadioButton.TextColorProperty, "AppForegroundColor");
            rb.BackgroundColor = Colors.Transparent;
            rb.Bind(RadioButton.IsCheckedProperty, nameof(SelectableOption<TaskRepetitionType>.IsSelected), source: option);
            rb.Bind(RadioButton.ContentProperty, nameof(SelectableOption<TaskRepetitionType>.Name), source: option);
            repetitionRadioButtonsLayout.Children.Add(rb);
        }

        var onLabel = new Label { Text = "On:", VerticalOptions = LayoutOptions.Center, Margin = new Thickness(0, 0, 0, 2) };
        onLabel.SetDynamicResource(Label.TextColorProperty, "AppSecondaryTextColor");
        onLabel.FontSize = 14;

        var dayOfWeekPicker = new Picker { Title = "Select Day", WidthRequest = 150 };
        dayOfWeekPicker.SetDynamicResource(Picker.TextColorProperty, "AppForegroundColor");
        dayOfWeekPicker.SetDynamicResource(Picker.TitleColorProperty, "AppSecondaryTextColor");
        dayOfWeekPicker.SetDynamicResource(Picker.BackgroundColorProperty, "AppSurfaceColor");
        dayOfWeekPicker.Bind(Picker.ItemsSourceProperty, nameof(AddTaskPopupPageViewModel.DisplayDaysOfWeek));
        dayOfWeekPicker.Bind(Picker.SelectedItemProperty, nameof(AddTaskPopupPageViewModel.SelectedDisplayDayOfWeek));

        var dayOfWeekLayout = new StackLayout
        {
            Orientation = StackOrientation.Horizontal,
            Spacing = 5,
            Margin = new Thickness(0, 5, 0, 0),
            Children = { onLabel, dayOfWeekPicker }
        };
        dayOfWeekLayout.Bind(StackLayout.IsVisibleProperty, nameof(AddTaskPopupPageViewModel.IsWeeklySelected));

        var repetitionOptionsLayout = new VerticalStackLayout
        {
            Spacing = 10,
            Margin = new Thickness(20, 5, 0, 0),
            Children = { repeatLabel, repetitionRadioButtonsLayout, dayOfWeekLayout }
        };
        repetitionOptionsLayout.Bind(VerticalStackLayout.IsVisibleProperty, nameof(AddTaskPopupPageViewModel.IsTimeTypeRepeating));

        var deleteButton = new Button { Text = "Delete", HorizontalOptions = LayoutOptions.Start };
        deleteButton.SetDynamicResource(Button.BackgroundColorProperty, "NordAurora0"); // Assuming NordAurora0 is globally available
        deleteButton.SetDynamicResource(Button.TextColorProperty, "NordPolarNight0");   // Assuming NordPolarNight0 is globally available
        deleteButton.Clicked += OnDeleteClicked;
        deleteButton.Bind(Button.IsVisibleProperty, nameof(AddTaskPopupPageViewModel.IsEditMode));

        var cancelButton = new Button { Text = "Cancel", HorizontalOptions = LayoutOptions.End };
        cancelButton.SetDynamicResource(Button.BackgroundColorProperty, "AppSurfaceColor");
        cancelButton.SetDynamicResource(Button.TextColorProperty, "AppForegroundColor");
        cancelButton.Clicked += OnCancelClicked;

        var confirmButton = new Button { HorizontalOptions = LayoutOptions.End };
        confirmButton.SetDynamicResource(Button.BackgroundColorProperty, "AppPrimaryActionBackgroundColor");
        confirmButton.SetDynamicResource(Button.TextColorProperty, "AppPrimaryActionForegroundColor");
        confirmButton.Bind(Button.TextProperty, nameof(AddTaskPopupPageViewModel.ConfirmButtonText));
        confirmButton.Clicked += OnConfirmClicked;

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 20,
                Spacing = 15,
                Children =
                {
                    titleLabel,
                    titleEntry,
                    timeRepetitionLabel,
                    new VerticalStackLayout
                    {
                        Spacing = 5,
                        Children = { timeTypeNoneRb, timeTypeSpecificDateRb, timeTypeRepeatingRb }
                    },
                    dueDatePicker,
                    repetitionOptionsLayout,
                    new Grid
                    {
                        Margin = new Thickness(0,20,0,0), ColumnSpacing = 10,
                        ColumnDefinitions = Columns.Define(GridRowsColumns.Auto, GridRowsColumns.Star, GridRowsColumns.Auto, GridRowsColumns.Auto),
                        Children =
                        {
                            deleteButton.Column(0),
                            cancelButton.Column(2),
                            confirmButton.Column(3)
                        }
                    }
                }
            }
        };
    }

    private async void OnConfirmClicked(object? sender, EventArgs e)
    {
        TaskItem? taskItem = _viewModel.GetTaskItem();
        
        if (taskItem == null)
        {
            await DisplayAlert("Validation Error", "Task title cannot be empty.", "OK");
            return;
        }

        if (_viewModel.IsEditMode)
        {
            WeakReferenceMessenger.Default.Send(new UpdateTaskMessage(taskItem));
        }
        else
        {
            WeakReferenceMessenger.Default.Send(new AddTaskMessage(taskItem));
        }

        await Shell.Current.Navigation.PopModalAsync();
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        await Shell.Current.Navigation.PopModalAsync();
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        var taskToDelete = _viewModel.GetTaskItem();
        if (_viewModel.IsEditMode && taskToDelete != null)
        {
            bool confirmed = await DisplayAlert("Confirm Delete", $"Are you sure you want to delete the task '{taskToDelete.Title}'?", "Yes", "No");
            
            if (!confirmed)
            {
                return;
            }

            WeakReferenceMessenger.Default.Send(new DeleteTaskMessage(taskToDelete.Id));
            await Shell.Current.Navigation.PopModalAsync();
        }
        else
        {
            await DisplayAlert("Error", "Cannot delete task. No task loaded for editing.", "OK");
        }
    }

    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        base.OnNavigatedFrom(args);
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
    }
}