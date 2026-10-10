using System;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;

namespace BabyTrackerMobile.Services
{
    /// <summary>
    /// Verifica o chat em segundo plano (enquanto o app está ativo) e dispara
    /// uma notificação local quando chega mensagem de outra pessoa.
    /// Push com o app totalmente fechado exige FCM + servidor (não incluso).
    /// </summary>
    public class ChatNotificationService
    {
        private readonly FirebaseAuthService _auth;
        private readonly ChatService _chat;

        public ChatNotificationService(FirebaseAuthService auth, ChatService chat)
        {
            _auth = auth;
            _chat = chat;
        }

        /// <summary>Quando a tela de chat está aberta, não precisa notificar.</summary>
        public static bool ChatOpen;

        private const string SeenKey = "chat_last_seen_key";

        private static string LastSeen
        {
            get { try { return Preferences.Get(SeenKey, ""); } catch { return ""; } }
            set { try { Preferences.Set(SeenKey, value); } catch { } }
        }

        public async Task CheckAsync()
        {
            if (!FirebaseConfig.IsConfigured || !_auth.IsLoggedIn) return;

            var list = await _chat.GetMessagesAsync();
            if (list.Count == 0) return;

            var myUid = _auth.Current?.LocalId ?? "";
            var lastSeen = LastSeen;
            var latestKey = list[list.Count - 1].Key;

            // Primeira vez: só memoriza, não notifica histórico.
            if (string.IsNullOrEmpty(lastSeen))
            {
                LastSeen = latestKey;
                return;
            }

            var idx = list.FindIndex(m => m.Key == lastSeen);
            var startFrom = idx >= 0 ? idx + 1 : 0;

            for (var i = startFrom; i < list.Count; i++)
            {
                var m = list[i];
                if (m.Uid == myUid) continue;   // minhas mensagens não notificam
                if (m.IsBot) continue;          // respostas do assistente não notificam
                if (ChatOpen) continue;         // já estou vendo o chat

                var title = m.IsNudge ? "🔔 Chamando a atenção!" : $"💬 {m.Nickname}";
                var body = m.IsNudge ? $"{m.Nickname} quer sua atenção no chat." : m.Text;
                var id = 2000 + (Math.Abs(m.Key.GetHashCode()) % 1000);
                try { await NotificationService.ShowMessageAsync(id, title, body); } catch { }
            }

            LastSeen = latestKey;
        }

        /// <summary>Marca tudo como visto (ao abrir o chat), evitando notificar o que já apareceu.</summary>
        public async Task MarkSeenAsync()
        {
            try
            {
                var list = await _chat.GetMessagesAsync();
                if (list.Count > 0) LastSeen = list[list.Count - 1].Key;
            }
            catch { }
        }
    }
}
