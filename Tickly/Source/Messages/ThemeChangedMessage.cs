using CommunityToolkit.Mvvm.Messaging.Messages;
using Tickly.Models;

namespace Tickly.Messages;

public class ThemeChangedMessage : ValueChangedMessage<ThemeType>
{
    public ThemeChangedMessage(ThemeType value) : base(value) { }
}