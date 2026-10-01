using BabyTrackerMobile.ViewModels;

namespace BabyTrackerMobile.Views;

public partial class FeedingPage : ContentPage
{
    private readonly FeedingViewModel _vm;
    
    public FeedingPage(FeedingViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }
}
