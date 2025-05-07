using CommunityToolkit.Mvvm.Messaging.Messages; // Assuming it might need messaging base later

namespace Tickly.Messages;

public class TasksReloadRequestedMessage : ValueChangedMessage<bool> // Example: Inherit if needed, otherwise just a simple class
{
    public TasksReloadRequestedMessage() : base(true) // Simple constructor, value doesn't matter much here
    {
    }
}