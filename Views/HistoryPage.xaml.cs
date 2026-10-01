using BabyTrackerMobile.ViewModels;

namespace BabyTrackerMobile.Views;

public partial class HistoryPage : ContentPage
{
    private readonly HistoryViewModel _vm;
    
    public HistoryPage(HistoryViewModel vm)
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
