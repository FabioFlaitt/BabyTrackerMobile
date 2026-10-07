using BabyTrackerMobile.Views;

namespace BabyTrackerMobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        // Página de Conquistas é aberta por cima (não é uma aba).
        Routing.RegisterRoute("milestones", typeof(MilestonesPage));
    }
}
