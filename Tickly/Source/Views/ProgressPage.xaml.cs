using Tickly.ViewModels;

namespace Tickly.Views;

public partial class ProgressPage : ContentPage
{
    public ProgressPage(ProgressViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
