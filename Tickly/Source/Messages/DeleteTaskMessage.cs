using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Tickly;

public class DeleteTaskMessage(Guid taskId) : ValueChangedMessage<Guid>(taskId)
{

}