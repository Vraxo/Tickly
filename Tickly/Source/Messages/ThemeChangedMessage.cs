using CommunityToolkit.Mvvm.Messaging.Messages;
using Tickly.Models;

namespace Tickly.Messages;

public class ThemeChangedMessage(ThemeType value) : ValueChangedMessage<ThemeType>(value)
{

}