using CommunityToolkit.Mvvm.Messaging.Messages;
using Tickly.Models;

namespace Tickly.Messages;

public class AddTaskMessage : ValueChangedMessage<TaskItem>
{
    public AddTaskMessage(TaskItem value) : base(value) { }
}