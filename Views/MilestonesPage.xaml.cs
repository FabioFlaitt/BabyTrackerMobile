using BabyTrackerMobile.ViewModels;

namespace BabyTrackerMobile.Views;

public partial class MilestonesPage : ContentPage
{
    private readonly MilestonesViewModel _vm;

    public MilestonesPage(MilestonesViewModel vm)
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
