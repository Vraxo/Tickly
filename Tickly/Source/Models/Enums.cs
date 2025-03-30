namespace Tickly.Models;

public enum TaskPriority
{
    High,
    Medium,
    Low
}

public enum TaskTimeType
{
    None,
    SpecificDate,
    Repeating
}

public enum TaskRepetitionType
{
    Daily,
    AlternateDay,
    Weekly
}

public enum SortOrderType
{
    Manual,
    PriorityHighFirst,
    PriorityLowFirst
}