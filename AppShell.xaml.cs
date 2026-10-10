using BabyTrackerMobile.Views;

namespace BabyTrackerMobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        // Páginas abertas por cima (não são abas).
        Routing.RegisterRoute("milestones", typeof(MilestonesPage));
        Routing.RegisterRoute("login", typeof(LoginPage));
        Routing.RegisterRoute("chat", typeof(ChatPage));
    }
}
