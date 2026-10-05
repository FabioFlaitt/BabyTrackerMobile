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

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.LoadCommand.Execute(null);
    }
}
