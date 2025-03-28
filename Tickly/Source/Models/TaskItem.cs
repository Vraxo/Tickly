// Models/TaskItem.cs
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using Tickly.Models; // Make sure enum namespace is referenced if separate

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
    private DateTime? _dueDate; // Base/current due date or start date for repeating

    [ObservableProperty]
    private TaskRepetitionType? _repetitionType;

    [ObservableProperty]
    private DayOfWeek? _repetitionDayOfWeek;

    [ObservableProperty]
    private int _order;

    // --- NEW Property for Animation ---
    [ObservableProperty]
    private bool _isFadingOut; // Flag to trigger fade-out animation before removal

    // Parameterless constructor for JSON deserialization
    public TaskItem()
    {
        Id = Guid.NewGuid();
        Title = string.Empty;
        // Default TimeType is TaskTimeType.None (0)
        IsFadingOut = false; // Default state
    }

    // Full constructor
    public TaskItem(
        string title,
        TaskPriority priority,
        TaskTimeType timeType,
        DateTime? dueDate,
        TaskRepetitionType? repetitionType,
        DayOfWeek? repetitionDayOfWeek,
        int order = 0)
    {
        Id = Guid.NewGuid();
        Title = title;
        Priority = priority;
        TimeType = timeType;
        DueDate = dueDate;
        RepetitionType = repetitionType;
        RepetitionDayOfWeek = repetitionDayOfWeek;
        Order = order;
        IsFadingOut = false; // Default state
    }
}