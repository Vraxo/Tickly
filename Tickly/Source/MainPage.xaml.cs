using Tickly.ViewModels; // Add this using statement

namespace Tickly;

public partial class MainPage : ContentPage
{
    public MainPage(MainViewModel viewModel) // Inject the ViewModel
    {
        InitializeComponent();
        BindingContext = viewModel; // Set the BindingContext
    }
}