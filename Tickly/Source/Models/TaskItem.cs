// Models/TaskItem.cs
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using Microsoft.Maui.Graphics; // <<== ADD THIS USING DIRECTIVE
using Tickly.Models;

namespace Tickly.Models;

public partial class TaskItem : ObservableObject
{
    [ObservableProperty]
    private Guid _id;

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private TaskTimeType _timeType;

    [ObservableProperty]
    private DateTime? _dueDate;

    [ObservableProperty]
    private TaskRepetitionType? _repetitionType;

    [ObservableProperty]
    private DayOfWeek? _repetitionDayOfWeek;

    [ObservableProperty]
    private int _order;

    [ObservableProperty]
    private bool _isFadingOut;

    [ObservableProperty]
    private int _index;

    // --- NEW Color Property ---
    [ObservableProperty]
    private Color _positionColor = Colors.Transparent; // Initialize with a default

    // Parameterless constructor
    public TaskItem()
    {
        Id = Guid.NewGuid();
        Title = string.Empty;
        IsFadingOut = false;
        Index = -1;
    }

    // Full constructor
    public TaskItem(
        string title,
        TaskTimeType timeType,
        DateTime? dueDate,
        TaskRepetitionType? repetitionType,
        DayOfWeek? repetitionDayOfWeek,
        int order = 0)
    {
        Id = Guid.NewGuid();
        Title = title;
        TimeType = timeType;
        DueDate = dueDate;
        RepetitionType = repetitionType;
        RepetitionDayOfWeek = repetitionDayOfWeek;
        Order = order;
        IsFadingOut = false;
        Index = -1;
    }
}