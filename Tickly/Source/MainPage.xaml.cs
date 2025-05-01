// Assuming your page is named MainPage and uses MainViewModel
namespace Tickly.Views; // Or wherever your Views namespace is

using Tickly.ViewModels; // Make sure the ViewModel namespace is included

public partial class MainPage : ContentPage
{
    // Constructor now takes MainViewModel injected by the DI container
    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel; // Set the BindingContext to the injected instance
    }

    // Remove any OnAppearing or other methods that might have
    // been manually creating or assigning the ViewModel previously,
    // unless they have other specific logic. The BindingContext
    // is now set once via DI.
}