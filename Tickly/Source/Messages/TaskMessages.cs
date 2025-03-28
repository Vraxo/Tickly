// Messages/TaskMessages.cs (Or add to existing Messages file)
using CommunityToolkit.Mvvm.Messaging.Messages;
using Tickly.Models;
using System; // Needed for Guid

namespace Tickly.Messages;

// Existing AddTaskMessage
public class AddTaskMessage : ValueChangedMessage<TaskItem>
{
    public AddTaskMessage(TaskItem value) : base(value) { }
}

// New UpdateTaskMessage
public class UpdateTaskMessage : ValueChangedMessage<TaskItem>
{
    public UpdateTaskMessage(TaskItem value) : base(value) { }
}

// New DeleteTaskMessage (Sending Guid is sufficient)
public class DeleteTaskMessage : ValueChangedMessage<Guid>
{
    public DeleteTaskMessage(Guid taskId) : base(taskId) { }
}