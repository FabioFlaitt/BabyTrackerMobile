using BabyTrackerMobile.ViewModels;

namespace BabyTrackerMobile.Views;

public partial class SleepPage : ContentPage
{
    private readonly SleepViewModel _vm;

    public SleepPage(SleepViewModel vm)
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
