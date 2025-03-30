// File: Models\Enums.cs
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

// *** ADDED ENUM ***
public enum SortOrderType
{
    Manual, // Default order as saved/reordered by user
    PriorityHighFirst, // High -> Low -> Title
    PriorityLowFirst // Low -> High -> Title
}
// *** END ADDED ENUM ***