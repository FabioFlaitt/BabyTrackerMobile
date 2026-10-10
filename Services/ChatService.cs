using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace BabyTrackerMobile.Services
{
    public class ChatMessage
    {
        public string Key { get; set; } = "";
        public string Uid { get; set; } = "";
        public string Nickname { get; set; } = "";
        public string Text { get; set; } = "";
        public long Ts { get; set; }
        public bool IsBot { get; set; }

        public DateTime LocalTime => DateTimeOffset.FromUnixTimeMilliseconds(Ts).LocalDateTime;
        public string TimeText => LocalTime.ToString("HH:mm");
    }

    /// <summary>
    /// Chat em sala única via Firebase Realtime Database (REST).
    /// Caminho: /chat/global/messages
    /// </summary>
    public class ChatService
    {
        private static readonly HttpClient Http = new();
        private readonly FirebaseAuthService _auth;

        public ChatService(FirebaseAuthService auth) => _auth = auth;

        private const string RoomPath = "chat/global/messages";

        public async Task<(bool ok, string? error)> SendAsync(string nickname, string text, bool isBot = false)
        {
            if (!FirebaseConfig.IsConfigured) return (false, "Firebase não configurado.");
            var token = await _auth.GetValidTokenAsync();
            if (token == null) return (false, "Você precisa estar logado.");
            var uid = _auth.Current?.LocalId ?? "";
            try
            {
                var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var body = "{" +
                    $"\"uid\":{JsonSerializer.Serialize(uid)}," +
                    $"\"nickname\":{JsonSerializer.Serialize(nickname)}," +
                    $"\"text\":{JsonSerializer.Serialize(text)}," +
                    $"\"ts\":{ts}," +
                    $"\"isBot\":{(isBot ? "true" : "false")}" +
                    "}";
                var url = $"{FirebaseConfig.DatabaseUrl}/{RoomPath}.json?auth={token}";
                using var resp = await Http.PostAsync(url, new StringContent(body, Encoding.UTF8, "application/json"));
                if (!resp.IsSuccessStatusCode)
                {
                    var err = await resp.Content.ReadAsStringAsync();
                    return (false, $"Erro ao enviar ({(int)resp.StatusCode}): {err}");
                }
                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, $"Falha de conexão: {ex.Message}");
            }
        }

        /// <summary>Busca as mensagens da sala, ordenadas por horário.</summary>
        public async Task<List<ChatMessage>> GetMessagesAsync()
        {
            var result = new List<ChatMessage>();
            if (!FirebaseConfig.IsConfigured) return result;
            var token = await _auth.GetValidTokenAsync();
            if (token == null) return result;
            try
            {
                var url = $"{FirebaseConfig.DatabaseUrl}/{RoomPath}.json?auth={token}";
                var json = await Http.GetStringAsync(url);
                if (string.IsNullOrWhiteSpace(json) || json == "null") return result;

                using var doc = JsonDocument.Parse(json);
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    var e = prop.Value;
                    result.Add(new ChatMessage
                    {
                        Key = prop.Name,
                        Uid = Str(e, "uid"),
                        Nickname = Str(e, "nickname"),
                        Text = Str(e, "text"),
                        Ts = e.TryGetProperty("ts", out var t) && t.TryGetInt64(out var ms) ? ms : 0,
                        IsBot = e.TryGetProperty("isBot", out var b) && b.ValueKind == JsonValueKind.True
                    });
                }
                result.Sort((a, b) => a.Ts.CompareTo(b.Ts));
                // Mantém apenas as últimas 300 para a lista não crescer sem limite na tela.
                if (result.Count > 300) result = result.GetRange(result.Count - 300, 300);
            }
            catch { /* rede instável: devolve o que tiver */ }
            return result;
        }

        private static string Str(JsonElement e, string prop)
            => e.TryGetProperty(prop, out var v) ? (v.GetString() ?? "") : "";
    }
}
