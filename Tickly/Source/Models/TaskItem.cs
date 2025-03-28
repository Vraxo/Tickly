// Models/TaskItem.cs
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using Tickly.Models; // Make sure TaskTimeType enum namespace is included if needed

namespace Tickly.Models;

public partial class TaskItem : ObservableObject
{
    [ObservableProperty]
    private Guid _id;

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private TaskPriority _priority;

    // --- THIS IS THE PROPERTY TO CHECK ---
    [ObservableProperty]
    private TaskTimeType _timeType; // Ensure [ObservableProperty] is present!

    [ObservableProperty]
    private DateTime? _dueDate;

    [ObservableProperty]
    private TaskRepetitionType? _repetitionType;

    [ObservableProperty]
    private DayOfWeek? _repetitionDayOfWeek;

    [ObservableProperty]
    private int _order;

    // Parameterless constructor for JSON deserialization
    public TaskItem()
    {
        Id = Guid.NewGuid();
        Title = string.Empty;
        // IMPORTANT: What is the default TimeType here?
        // Enums default to 0 if not explicitly set, which is TaskTimeType.None.
        // This is usually fine for deserialization as the JSON value should override it.
        // _timeType = TaskTimeType.None; // Explicitly setting it doesn't hurt
    }

    // Full constructor
    public TaskItem(
        string title,
        TaskPriority priority,
        TaskTimeType timeType, // Ensure this parameter exists and is used
        DateTime? dueDate,
        TaskRepetitionType? repetitionType,
        DayOfWeek? repetitionDayOfWeek,
        int order = 0)
    {
        Id = Guid.NewGuid();
        Title = title;
        Priority = priority;
        TimeType = timeType; // Ensure it's assigned here
        DueDate = dueDate;
        RepetitionType = repetitionType;
        RepetitionDayOfWeek = repetitionDayOfWeek;
        Order = order;
    }

    // --- Generated Property (by ObservableProperty) ---
    // public TaskTimeType TimeType { get => _timeType; set => SetProperty(ref _timeType, value); }
    // You don't write this, but verify the [ObservableProperty] is on the _timeType field.
}