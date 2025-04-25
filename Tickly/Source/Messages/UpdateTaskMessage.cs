using CommunityToolkit.Mvvm.Messaging.Messages;
using Tickly.Models;

namespace Tickly.Messages;

public class UpdateTaskMessage(TaskItem value) : ValueChangedMessage<TaskItem>(value)
{

}