using CommunityToolkit.Mvvm.ComponentModel;

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

    [ObservableProperty]
    private Color _positionColor = Colors.Transparent;

    public TaskItem()
    {
        Id = Guid.NewGuid();
        Title = string.Empty;
        IsFadingOut = false;
        Index = -1;
    }

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