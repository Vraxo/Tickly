using CommunityToolkit.Mvvm.Messaging.Messages;
using Tickly.Models;

namespace Tickly.Messages;

public class AddTaskMessage(TaskItem value) : ValueChangedMessage<TaskItem>(value)
{

}