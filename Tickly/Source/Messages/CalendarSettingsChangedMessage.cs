// Messages/CalendarSettingChangedMessage.cs
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Tickly.Messages;

public class CalendarSettingChangedMessage : ValueChangedMessage<bool>
{
    public CalendarSettingChangedMessage() : base(true)
    {

    }
}