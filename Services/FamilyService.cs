using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;

namespace BabyTrackerMobile.Services
{
    /// <summary>
    /// Gerencia a "família": um código que liga duas (ou mais) contas aos mesmos
    /// dados do bebê. Guarda o código localmente (Preferences) e em
    /// /users/{uid}/familyId no Firebase.
    /// </summary>
    public class FamilyService
    {
        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };
        private readonly FirebaseAuthService _auth;
        private const string PrefKey = "family_id";

        public FamilyService(FirebaseAuthService auth) => _auth = auth;

        public string? FamilyId
        {
            get { try { var v = Preferences.Get(PrefKey, ""); return string.IsNullOrEmpty(v) ? null : v; } catch { return null; } }
            private set { try { if (value == null) Preferences.Remove(PrefKey); else Preferences.Set(PrefKey, value); } catch { } }
        }

        public bool HasFamily => !string.IsNullOrEmpty(FamilyId);

        /// <summary>Sincroniza o código de família com a nuvem após o login.</summary>
        public async Task EnsureFromCloudAsync()
        {
            if (!FirebaseConfig.IsConfigured || !_auth.IsLoggedIn) return;
            var token = await _auth.GetValidTokenAsync();
            var uid = _auth.Current?.LocalId;
            if (token == null || string.IsNullOrEmpty(uid)) return;
            try
            {
                var url = $"{FirebaseConfig.DatabaseUrl}/users/{uid}/familyId.json?auth={token}";
                var json = await Http.GetStringAsync(url);
                if (!string.IsNullOrWhiteSpace(json) && json != "null")
                {
                    using var doc = JsonDocument.Parse(json);
                    var cloudFid = doc.RootElement.GetString();
                    if (!string.IsNullOrEmpty(cloudFid)) { FamilyId = cloudFid; return; }
                }
                // Nuvem não tem, mas temos local → envia.
                if (HasFamily) await WriteMembershipAsync(FamilyId!, token, uid);
            }
            catch { }
        }

        public async Task<string?> CreateFamilyAsync()
        {
            if (!FirebaseConfig.IsConfigured || !_auth.IsLoggedIn) return null;
            var token = await _auth.GetValidTokenAsync();
            var uid = _auth.Current?.LocalId;
            if (token == null || string.IsNullOrEmpty(uid)) return null;

            var code = GenerateCode();
            FamilyId = code;
            await WriteMembershipAsync(code, token, uid);
            return code;
        }

        public async Task<(bool ok, string? error)> JoinFamilyAsync(string code)
        {
            code = (code ?? "").Trim().ToUpperInvariant();
            if (code.Length < 4) return (false, "Código inválido.");
            if (!FirebaseConfig.IsConfigured || !_auth.IsLoggedIn) return (false, "Entre na sua conta primeiro.");
            var token = await _auth.GetValidTokenAsync();
            var uid = _auth.Current?.LocalId;
            if (token == null || string.IsNullOrEmpty(uid)) return (false, "Sessão inválida.");

            FamilyId = code;
            await WriteMembershipAsync(code, token, uid);
            return (true, null);
        }

        public void Leave() => FamilyId = null;

        private async Task WriteMembershipAsync(string code, string token, string uid)
        {
            try
            {
                await Http.PutAsync(
                    $"{FirebaseConfig.DatabaseUrl}/users/{uid}/familyId.json?auth={token}",
                    new StringContent(JsonSerializer.Serialize(code), Encoding.UTF8, "application/json"));
                await Http.PutAsync(
                    $"{FirebaseConfig.DatabaseUrl}/families/{code}/members/{uid}.json?auth={token}",
                    new StringContent("true", Encoding.UTF8, "application/json"));
            }
            catch { }
        }

        private static string GenerateCode()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // sem O/0/I/1
            var rnd = new Random();
            var sb = new StringBuilder();
            for (var i = 0; i < 6; i++) sb.Append(chars[rnd.Next(chars.Length)]);
            return sb.ToString();
        }
    }
}
