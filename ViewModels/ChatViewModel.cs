using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Storage;
using BabyTrackerMobile.Services;

namespace BabyTrackerMobile.ViewModels
{
    public partial class ChatViewModel : ObservableObject
    {
        private readonly FirebaseAuthService _auth;
        private readonly ChatService _chat;

        public ChatViewModel(FirebaseAuthService auth, ChatService chat)
        {
            _auth = auth;
            _chat = chat;
        }

        [ObservableProperty]
        private ObservableCollection<ChatMessage> messages = new();

        [ObservableProperty]
        private string draft = "";

        [ObservableProperty]
        private string nickname = "";

        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private string statusText = "";

        public bool IsLoggedIn => _auth.IsLoggedIn;
        public bool HasNickname => !string.IsNullOrWhiteSpace(Nickname);

        private string NickKey => $"chat_nick_{_auth.Current?.LocalId}";

        public void LoadNickname()
        {
            try { Nickname = Preferences.Get(NickKey, ""); } catch { Nickname = ""; }
            OnPropertyChanged(nameof(HasNickname));
        }

        public void SaveNickname(string nick)
        {
            Nickname = nick.Trim();
            try { Preferences.Set(NickKey, Nickname); } catch { }
            OnPropertyChanged(nameof(HasNickname));
        }

        /// <summary>Recarrega as mensagens da sala (chamado em intervalos pela página).</summary>
        public async Task RefreshAsync()
        {
            if (!_auth.IsLoggedIn) return;
            var list = await _chat.GetMessagesAsync();
            // Reconstrói só se mudou a quantidade ou a última chave, para não "piscar".
            if (list.Count == Messages.Count &&
                (list.Count == 0 || list[list.Count - 1].Key == Messages[Messages.Count - 1].Key))
                return;

            var col = new ObservableCollection<ChatMessage>();
            foreach (var m in list) col.Add(m);
            Messages = col;
        }

        [RelayCommand]
        private async Task SendAsync()
        {
            var text = (Draft ?? "").Trim();
            if (string.IsNullOrEmpty(text)) return;
            if (!HasNickname)
            {
                StatusText = "Escolha um nickname primeiro.";
                return;
            }

            IsBusy = true;
            try
            {
                Draft = "";
                var (ok, error) = await _chat.SendAsync(Nickname, text);
                if (!ok) { StatusText = error ?? "Erro ao enviar."; Draft = text; return; }
                StatusText = "";
                await RefreshAsync();

                // Assistente responde, se reconhecer o assunto.
                var answer = BabyAssistantBot.TryAnswer(text);
                if (answer != null)
                {
                    await Task.Delay(600);
                    await _chat.SendAsync(BabyAssistantBot.Nickname, answer, isBot: true);
                    await RefreshAsync();
                }
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        private async Task NudgeAsync()
        {
            if (!HasNickname) { StatusText = "Escolha um nickname primeiro."; return; }
            IsBusy = true;
            try
            {
                var (ok, error) = await _chat.SendAsync(Nickname, $"🔔 {Nickname} está chamando a atenção!", isNudge: true);
                if (!ok) { StatusText = error ?? "Erro ao enviar."; return; }
                StatusText = "";
                await RefreshAsync();
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        private async Task SignOutAsync()
        {
            await _auth.SignOutAsync();
            await Microsoft.Maui.Controls.Shell.Current.GoToAsync("//dashboard");
        }
    }
}
