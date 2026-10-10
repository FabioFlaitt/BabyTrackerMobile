using Microsoft.Extensions.DependencyInjection;
using BabyTrackerMobile.Services;

namespace BabyTrackerMobile;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();

		// Restaura a sessão de login salva (se houver), sem travar a inicialização.
		var auth = IPlatformApplication.Current?.Services?.GetService<FirebaseAuthService>();
		if (auth != null)
			_ = auth.TryRestoreAsync();

		MainPage = new AppShell();
	}
}
