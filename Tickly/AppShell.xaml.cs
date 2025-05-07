// AppShell.xaml.cs
using Tickly.Views; // Add this using

namespace Tickly;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Register the route for the modal popup page
        Routing.RegisterRoute(nameof(AddTaskPopupPage), typeof(AddTaskPopupPage));
    }
}