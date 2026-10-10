using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;

namespace BabyTrackerMobile.Services
{
    public class AuthUser
    {
        public string LocalId { get; set; } = "";
        public string Email { get; set; } = "";
        public string IdToken { get; set; } = "";
        public string RefreshToken { get; set; } = "";
        public DateTime ExpiresAt { get; set; }
    }

    /// <summary>
    /// Login/cadastro via Firebase Authentication (REST, email+senha).
    /// As senhas são tratadas pelo Firebase; o app nunca as armazena.
    /// </summary>
    public class FirebaseAuthService
    {
        private static readonly HttpClient Http = new();
        private const string IdentityBase = "https://identitytoolkit.googleapis.com/v1/accounts";
        private const string TokenBase = "https://securetoken.googleapis.com/v1/token";

        public AuthUser? Current { get; private set; }
        public bool IsLoggedIn => Current != null && !string.IsNullOrEmpty(Current.IdToken);
        public event Action? AuthChanged;

        /// <summary>Tenta restaurar a sessão salva (renovando o token se preciso).</summary>
        public async Task TryRestoreAsync()
        {
            try
            {
                var refresh = await SecureStorage.GetAsync("fb_refresh");
                var localId = await SecureStorage.GetAsync("fb_localid");
                var email = await SecureStorage.GetAsync("fb_email");
                if (string.IsNullOrEmpty(refresh)) return;

                var ok = await RefreshAsync(refresh);
                if (ok && Current != null)
                {
                    Current.LocalId = localId ?? Current.LocalId;
                    Current.Email = email ?? Current.Email;
                    AuthChanged?.Invoke();
                }
            }
            catch { /* sessão não restaurada; segue deslogado */ }
        }

        public Task<(bool ok, string? error)> SignUpAsync(string email, string password)
            => AuthenticateAsync("signUp", email, password);

        public Task<(bool ok, string? error)> SignInAsync(string email, string password)
            => AuthenticateAsync("signInWithPassword", email, password);

        private async Task<(bool ok, string? error)> AuthenticateAsync(string action, string email, string password)
        {
            if (!FirebaseConfig.IsConfigured)
                return (false, "O Firebase ainda não foi configurado no app.");

            try
            {
                var url = $"{IdentityBase}:{action}?key={FirebaseConfig.ApiKey}";
                var body = $"{{\"email\":{JsonSerializer.Serialize(email)},\"password\":{JsonSerializer.Serialize(password)},\"returnSecureToken\":true}}";
                using var resp = await Http.PostAsync(url, new StringContent(body, Encoding.UTF8, "application/json"));
                var json = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!resp.IsSuccessStatusCode)
                {
                    var code = root.TryGetProperty("error", out var err) && err.TryGetProperty("message", out var msg)
                        ? msg.GetString() ?? "" : "";
                    return (false, MapError(code));
                }

                var expiresIn = int.TryParse(GetStr(root, "expiresIn"), out var s) ? s : 3600;
                Current = new AuthUser
                {
                    LocalId = GetStr(root, "localId"),
                    Email = GetStr(root, "email"),
                    IdToken = GetStr(root, "idToken"),
                    RefreshToken = GetStr(root, "refreshToken"),
                    ExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn - 60)
                };
                await PersistAsync();
                AuthChanged?.Invoke();
                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, $"Falha de conexão: {ex.Message}");
            }
        }

        private async Task<bool> RefreshAsync(string refreshToken)
        {
            if (!FirebaseConfig.IsConfigured) return false;
            try
            {
                var url = $"{TokenBase}?key={FirebaseConfig.ApiKey}";
                var form = new FormUrlEncodedContent(new[]
                {
                    new System.Collections.Generic.KeyValuePair<string, string>("grant_type", "refresh_token"),
                    new System.Collections.Generic.KeyValuePair<string, string>("refresh_token", refreshToken),
                });
                using var resp = await Http.PostAsync(url, form);
                var json = await resp.Content.ReadAsStringAsync();
                if (!resp.IsSuccessStatusCode) return false;
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var expiresIn = int.TryParse(GetStr(root, "expires_in"), out var s) ? s : 3600;
                Current = new AuthUser
                {
                    LocalId = GetStr(root, "user_id"),
                    Email = Current?.Email ?? "",
                    IdToken = GetStr(root, "id_token"),
                    RefreshToken = GetStr(root, "refresh_token"),
                    ExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn - 60)
                };
                await PersistAsync();
                return true;
            }
            catch { return false; }
        }

        /// <summary>Retorna um idToken válido, renovando se estiver perto de expirar.</summary>
        public async Task<string?> GetValidTokenAsync()
        {
            if (Current == null) return null;
            if (DateTime.UtcNow >= Current.ExpiresAt && !string.IsNullOrEmpty(Current.RefreshToken))
                await RefreshAsync(Current.RefreshToken);
            return Current?.IdToken;
        }

        public async Task SignOutAsync()
        {
            Current = null;
            try
            {
                SecureStorage.Remove("fb_refresh");
                SecureStorage.Remove("fb_localid");
                SecureStorage.Remove("fb_email");
            }
            catch { }
            AuthChanged?.Invoke();
            await Task.CompletedTask;
        }

        private async Task PersistAsync()
        {
            if (Current == null) return;
            try
            {
                await SecureStorage.SetAsync("fb_refresh", Current.RefreshToken);
                await SecureStorage.SetAsync("fb_localid", Current.LocalId);
                await SecureStorage.SetAsync("fb_email", Current.Email);
            }
            catch { }
        }

        private static string GetStr(JsonElement root, string prop)
            => root.TryGetProperty(prop, out var v) ? (v.GetString() ?? "") : "";

        private static string MapError(string code) => code switch
        {
            "EMAIL_EXISTS" => "Este e-mail já tem conta. Faça login.",
            "INVALID_LOGIN_CREDENTIALS" => "E-mail ou senha incorretos.",
            "INVALID_PASSWORD" => "Senha incorreta.",
            "EMAIL_NOT_FOUND" => "E-mail não cadastrado.",
            "INVALID_EMAIL" => "E-mail inválido.",
            "WEAK_PASSWORD : Password should be at least 6 characters" => "A senha deve ter ao menos 6 caracteres.",
            "USER_DISABLED" => "Esta conta foi desativada.",
            _ when code.StartsWith("TOO_MANY_ATTEMPTS") => "Muitas tentativas. Tente de novo mais tarde.",
            _ when code.StartsWith("WEAK_PASSWORD") => "A senha deve ter ao menos 6 caracteres.",
            _ => string.IsNullOrEmpty(code) ? "Não foi possível autenticar." : code
        };
    }
}
