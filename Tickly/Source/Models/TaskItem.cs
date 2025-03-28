// Models/TaskItem.cs
using CommunityToolkit.Mvvm.ComponentModel;
// using Microsoft.VisualBasic; // REMOVED
using System;
// using static Android.Icu.Text.CaseMap; // REMOVED
// using static Android.Icu.Text.TimeZoneFormat; // REMOVED

namespace Tickly.Models;

// Use ObservableObject for potential future editing features
// and for the Order property update during drag/drop.
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
    private DateTime? _dueDate; // Used for SpecificDate and as the base for Repeating

    [ObservableProperty]
    private TaskRepetitionType? _repetitionType; // Nullable if TimeType is not Repeating

    [ObservableProperty]
    private DayOfWeek? _repetitionDayOfWeek; // Nullable if RepetitionType is not Weekly

    // This property is crucial for saving the drag/drop order
    [ObservableProperty]
    private int _order;

    // Parameterless constructor for JSON deserialization
    public TaskItem()
    {
        Id = Guid.NewGuid(); // Assign a default Guid
        Title = string.Empty; // Initialize string
    }

    public TaskItem(
        string title,
        TaskPriority priority,
        TaskTimeType timeType,
        DateTime? dueDate,
        TaskRepetitionType? repetitionType,
        DayOfWeek? repetitionDayOfWeek,
        int order = 0) // Default order
    {
        Id = Guid.NewGuid();
        Title = title;
        Priority = priority;
        TimeType = timeType;
        DueDate = dueDate;
        RepetitionType = repetitionType;
        RepetitionDayOfWeek = repetitionDayOfWeek;
        Order = order;
    }
}