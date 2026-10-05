using BabyTrackerMobile.ViewModels;

namespace BabyTrackerMobile.Views;

public partial class DiaperPage : ContentPage
{
    private readonly DiaperViewModel _vm;

    public DiaperPage(DiaperViewModel vm)
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
