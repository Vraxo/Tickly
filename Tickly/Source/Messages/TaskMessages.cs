// File: Messages/TaskMessages.cs (Add to this existing file)
using CommunityToolkit.Mvvm.Messaging.Messages;
using Tickly.Models;
using System; // Needed for Guid

namespace Tickly.Messages;

// Existing AddTaskMessage
public class AddTaskMessage : ValueChangedMessage<TaskItem>
{
    public AddTaskMessage(TaskItem value) : base(value) { }
}

// Existing UpdateTaskMessage
public class UpdateTaskMessage : ValueChangedMessage<TaskItem>
{
    public UpdateTaskMessage(TaskItem value) : base(value) { }
}

// Existing DeleteTaskMessage
public class DeleteTaskMessage : ValueChangedMessage<Guid>
{
    public DeleteTaskMessage(Guid taskId) : base(taskId) { }
}

// *** NEW MESSAGE ***
/// <summary>
/// Signals that the underlying task data source may have changed (e.g., after import)
/// and the main view should reload its tasks.
/// </summary>
public class TasksReloadRequestedMessage : RequestMessage<bool> // Can just use RequestMessage
{
    // No specific data needed, just the signal
}
// *** END NEW MESSAGE ***