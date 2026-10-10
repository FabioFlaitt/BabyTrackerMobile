using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using BabyTrackerMobile.Data;
using BabyTrackerMobile.Services;

namespace BabyTrackerMobile;

public partial class App : Application
{
	private bool _started;

	public App()
	{
		InitializeComponent();
		MainPage = new AppShell();
	}

	protected override void OnStart()
	{
		base.OnStart();
		if (_started) return;
		_started = true;

		var sp = IPlatformApplication.Current?.Services;
		var auth = sp?.GetService<FirebaseAuthService>();
		var family = sp?.GetService<FamilyService>();
		var cloud = sp?.GetService<CloudSyncService>();

		DatabaseService.Cloud = cloud;
		_ = InitAsync(auth, family, cloud);
	}

	private static async Task InitAsync(FirebaseAuthService? auth, FamilyService? family, CloudSyncService? cloud)
	{
		try
		{
			await DatabaseService.BackfillCloudIdsAsync();
			if (auth != null) await auth.TryRestoreAsync();
			if (family != null) await family.EnsureFromCloudAsync();
			if (cloud != null) await cloud.PullAllAsync(force: true);
		}
		catch { }
	}
}
