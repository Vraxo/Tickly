// MainPage.xaml.cs
using Tickly.Models; // Needed for TaskItem
using Tickly.ViewModels; // Needed for MainViewModel

namespace Tickly;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
        // BindingContext is set in XAML to MainViewModel instance
    }

    // --- NEW EVENT HANDLER for CheckBox ---
    private void CheckBox_CheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        // Only trigger the command when the checkbox becomes *checked* by the user click
        // We use OneWay binding for IsChecked, so the ViewModel controls the state.
        // This event fires when the *user* interacts. We only care about the "checked" action.
        if (e.Value) // If the checkbox was just checked
        {
            // Get the CheckBox that triggered the event
            if (sender is CheckBox checkBox)
            {
                // Get the TaskItem associated with this CheckBox
                if (checkBox.BindingContext is TaskItem taskItem)
                {
                    // Get the MainViewModel instance from the page's BindingContext
                    if (this.BindingContext is MainViewModel viewModel)
                    {
                        // Check if the command can execute and then execute it
                        if (viewModel.MarkTaskCompleteCommand.CanExecute(taskItem))
                        {
                            viewModel.MarkTaskCompleteCommand.Execute(taskItem);
                        }
                    }
                }
            }
        }
        // We ignore the e.Value == false case because unchecking is either
        // not allowed or handled differently by the ViewModel logic (e.g., resetting on load).
        // To prevent the CheckBox from visually unchecking immediately if the command logic
        // keeps IsCompleted=true, we use Mode=OneWay binding.
    }
}