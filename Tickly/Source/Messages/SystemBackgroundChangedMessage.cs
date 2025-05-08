using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Tickly.Messages;

// Message to signal that the system background preference has changed
public sealed class SystemBackgroundChangedMessage(bool useSystemBackground) : ValueChangedMessage<bool>(useSystemBackground)
{

}