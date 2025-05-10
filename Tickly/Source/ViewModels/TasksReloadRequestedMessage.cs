using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Tickly.Messages;

public class TasksReloadRequestedMessage : ValueChangedMessage<bool>
{
    public TasksReloadRequestedMessage() : base(true)
    {

    }
}