using System;
using BabyTrackerMobile.Services;
using BabyTrackerMobile.ViewModels;

namespace BabyTrackerMobile.Views;

public partial class ChatPage : ContentPage
{
    private readonly ChatViewModel _vm;
    private readonly FirebaseAuthService _auth;
    private readonly ChatNotificationService _notifier;
    private IDispatcherTimer? _timer;

    public ChatPage(ChatViewModel vm, FirebaseAuthService auth, ChatNotificationService notifier)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _auth = auth;
        _notifier = notifier;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Precisa estar logado para o chat.
        if (!_auth.IsLoggedIn)
        {
            await Shell.Current.GoToAsync("//dashboard");
            await Shell.Current.GoToAsync("login");
            return;
        }

        _vm.LoadNickname();
        if (!_vm.HasNickname)
            await AskNicknameAsync();

        // Estou vendo o chat: não notifica, zera o badge e marca tudo como visto.
        ChatNotificationService.ChatOpen = true;
        NotificationService.ClearBadge();
        await _notifier.MarkSeenAsync();

        await _vm.RefreshAsync();
        ScrollToEnd();

        // Atualiza a sala a cada 3s enquanto o chat está aberto.
        _timer ??= Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(3);
        _timer.Tick -= OnTick;
        _timer.Tick += OnTick;
        _timer.Start();
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        _timer?.Stop();
        ChatNotificationService.ChatOpen = false;
        await _notifier.MarkSeenAsync();
    }

    private async void OnTick(object? sender, EventArgs e)
    {
        var before = _vm.Messages.Count;
        await _vm.RefreshAsync();
        if (_vm.Messages.Count != before) ScrollToEnd();
    }

    private async void OnChangeNick(object? sender, EventArgs e) => await AskNicknameAsync();

    private async System.Threading.Tasks.Task AskNicknameAsync()
    {
        var current = _vm.Nickname;
        var nick = await DisplayPromptAsync(
            "Seu apelido no chat",
            "Como você quer aparecer para os outros?",
            "OK", "Cancelar",
            string.IsNullOrEmpty(current) ? "Ex: Mamãe do Antonio" : current,
            maxLength: 24,
            initialValue: current);

        if (!string.IsNullOrWhiteSpace(nick))
            _vm.SaveNickname(nick);
    }

    private void ScrollToEnd()
    {
        if (_vm.Messages.Count == 0) return;
        try { MessagesList.ScrollTo(_vm.Messages.Count - 1, animate: true); } catch { }
    }
}
