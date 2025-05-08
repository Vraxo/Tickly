using CommunityToolkit.Mvvm.Messaging.Messages;
using Tickly.Models;

namespace Tickly.Messages;

public class CalendarSettingsChangedMessage(CalendarSystemType value) : ValueChangedMessage<CalendarSystemType>(value)
{

}
