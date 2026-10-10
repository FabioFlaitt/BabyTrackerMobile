using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using BabyTrackerMobile.Data;
using BabyTrackerMobile.Services;

namespace BabyTrackerMobile;

public partial class App : Application
{
	private bool _started;
	private ChatNotificationService? _chatNotifier;
	private IDispatcherTimer? _chatTimer;

	public App()
	{
		InitializeComponent();
		MainPage = new AppShell();
	}

	protected override void OnStart()
	{
		base.OnStart();

		NotificationService.ClearBadge();

		if (!_started)
		{
			_started = true;
			var sp = IPlatformApplication.Current?.Services;
			var auth = sp?.GetService<FirebaseAuthService>();
			var family = sp?.GetService<FamilyService>();
			var cloud = sp?.GetService<CloudSyncService>();
			_chatNotifier = sp?.GetService<ChatNotificationService>();

			DatabaseService.Cloud = cloud;
			_ = InitAsync(auth, family, cloud);
		}

		StartChatPolling();
	}

	protected override void OnResume()
	{
		base.OnResume();
		NotificationService.ClearBadge();
		StartChatPolling();
	}

	protected override void OnSleep()
	{
		base.OnSleep();
		_chatTimer?.Stop();
	}

	private void StartChatPolling()
	{
		if (_chatNotifier == null) return;
		_chatTimer ??= Dispatcher.CreateTimer();
		_chatTimer.Interval = TimeSpan.FromSeconds(12);
		_chatTimer.Tick -= OnChatTick;
		_chatTimer.Tick += OnChatTick;
		_chatTimer.Start();
	}

	private async void OnChatTick(object? sender, EventArgs e)
	{
		if (_chatNotifier != null)
		{
			try { await _chatNotifier.CheckAsync(); } catch { }
		}
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
