// Messages/CalendarSettingChangedMessage.cs
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Tickly.Messages;

/// <summary>
/// A simple message indicating that the calendar system setting has changed.
/// </summary>
public class CalendarSettingChangedMessage : ValueChangedMessage<bool> // Value isn't strictly needed, but fits pattern
{
    // We don't really need to pass a value, but ValueChangedMessage requires one.
    // We can just pass true to indicate a change occurred.
    public CalendarSettingChangedMessage() : base(true)
    {
    }
}