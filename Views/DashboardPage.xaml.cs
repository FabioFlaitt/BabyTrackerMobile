using BabyTrackerMobile.ViewModels;

namespace BabyTrackerMobile.Views;

public partial class DashboardPage : ContentPage
{
    private readonly DashboardViewModel _vm;
    private IDispatcherTimer? _timer;
    private Window? _window;

    public DashboardPage(DashboardViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.LoadCommand.Execute(null);

        // Confere a virada do dia a cada 30 s enquanto o Painel está visível...
        _timer ??= Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(30);
        _timer.Tick -= OnTick;
        _timer.Tick += OnTick;
        _timer.Start();

        // ...e também assim que o app volta do segundo plano.
        if (_window != null) _window.Resumed -= OnResumed;
        _window = Window;
        if (_window != null) _window.Resumed += OnResumed;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _timer?.Stop();
        if (_window != null)
        {
            _window.Resumed -= OnResumed;
            _window = null;
        }
    }

    private void OnTick(object? sender, EventArgs e) => _vm.CheckDayRollover();

    private void OnResumed(object? sender, EventArgs e) => _vm.CheckDayRollover();
}
