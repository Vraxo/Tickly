// Models/TaskItem.cs
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using Tickly.Models; // Needed for enums

namespace Tickly.Models;

public partial class TaskItem : ObservableObject
{
    [ObservableProperty]
    private Guid _id;

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private TaskPriority _priority;

    [ObservableProperty]
    private TaskTimeType _timeType;

    [ObservableProperty]
    private DateTime? _dueDate; // For SpecificDate and the *current* due date of Repeating tasks

    [ObservableProperty]
    private TaskRepetitionType? _repetitionType;

    [ObservableProperty]
    private DayOfWeek? _repetitionDayOfWeek;

    [ObservableProperty]
    private int _order;

    // --- NEW PROPERTY ---
    // Tracks if a repeating task has been marked complete for its *current* DueDate cycle.
    // Gets reset when the DueDate passes.
    [ObservableProperty]
    private bool _isCompleted; // Defaults to false

    // Parameterless constructor for JSON deserialization
    public TaskItem()
    {
        Id = Guid.NewGuid();
        Title = string.Empty;
        // Defaults for other properties (TimeType=None, IsCompleted=false) are usually fine
    }

    // Full constructor
    public TaskItem(
        string title,
        TaskPriority priority,
        TaskTimeType timeType,
        DateTime? dueDate,
        TaskRepetitionType? repetitionType,
        DayOfWeek? repetitionDayOfWeek,
        int order = 0,
        bool isCompleted = false) // Add optional IsCompleted
    {
        Id = Guid.NewGuid();
        Title = title;
        Priority = priority;
        TimeType = timeType;
        DueDate = dueDate;
        RepetitionType = repetitionType;
        RepetitionDayOfWeek = repetitionDayOfWeek;
        Order = order;
        IsCompleted = isCompleted; // Initialize IsCompleted
    }
}