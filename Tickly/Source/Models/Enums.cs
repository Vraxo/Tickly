namespace Tickly.Models;

public enum TaskPriority
{
    High,   // Red
    Medium, // Orange
    Low     // Green
}

public enum TaskTimeType
{
    None,         // One-time, any time
    SpecificDate, // Specific date
    Repeating     // Daily, Alternate, Weekly
}

public enum TaskRepetitionType
{
    Daily,
    AlternateDay, // Every other day from the start date
    Weekly
}