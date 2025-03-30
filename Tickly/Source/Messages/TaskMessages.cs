using CommunityToolkit.Mvvm.Messaging.Messages;
using Tickly.Models;
using System;

namespace Tickly.Messages;

public class AddTaskMessage : ValueChangedMessage<TaskItem>
{
    public AddTaskMessage(TaskItem value) : base(value) { }
}

public class UpdateTaskMessage : ValueChangedMessage<TaskItem>
{
    public UpdateTaskMessage(TaskItem value) : base(value) { }
}

public class DeleteTaskMessage : ValueChangedMessage<Guid>
{
    public DeleteTaskMessage(Guid taskId) : base(taskId) { }
}

public class TasksReloadRequestedMessage : RequestMessage<bool>
{
}