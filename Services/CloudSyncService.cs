using System;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BabyTrackerMobile.Data;
using BabyTrackerMobile.Models;

namespace BabyTrackerMobile.Services
{
    /// <summary>
    /// Sincroniza os dados do bebê com o Firebase Realtime Database, por família.
    /// Caminho: /families/{familyId}/{tabela}/{cloudId}
    /// "Última edição vence" (UpdatedAt). Exclusão é lógica (IsDeleted).
    /// </summary>
    public class CloudSyncService
    {
        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(6) };
        private readonly FirebaseAuthService _auth;
        private readonly FamilyService _family;
        private DateTime _lastPull = DateTime.MinValue;

        public CloudSyncService(FirebaseAuthService auth, FamilyService family)
        {
            _auth = auth;
            _family = family;
        }

        public bool Enabled => FirebaseConfig.IsConfigured && _auth.IsLoggedIn && _family.HasFamily;

        // ---------- PUSH ----------
        private async Task PutAsync(string path, string json)
        {
            if (!Enabled) return;
            var token = await _auth.GetValidTokenAsync();
            if (token == null) return;
            try
            {
                var url = $"{FirebaseConfig.DatabaseUrl}/families/{_family.FamilyId}/{path}.json?auth={token}";
                await Http.PutAsync(url, new StringContent(json, Encoding.UTF8, "application/json"));
            }
            catch { }
        }

        public Task PushBabyAsync(Baby b) => PutAsync("baby", BabyJson(b));
        public Task PushFeedingAsync(FeedingRecord r) => PutAsync($"feedings/{r.CloudId}", FeedingJson(r));
        public Task PushDiaperAsync(DiaperRecord r) => PutAsync($"diapers/{r.CloudId}", DiaperJson(r));
        public Task PushWaterAsync(MotherWaterRecord r) => PutAsync($"waters/{r.CloudId}", WaterJson(r));
        public Task PushSleepAsync(SleepRecord r) => PutAsync($"sleeps/{r.CloudId}", SleepJson(r));
        public Task PushVitaminAsync(VitaminSchedule r) => PutAsync($"vitamins/{r.CloudId}", VitaminJson(r));
        public Task PushMilestoneAsync(Milestone r) => PutAsync($"milestones/{r.CloudId}", MilestoneJson(r));
        public Task PushVitaminLogAsync(VitaminLog r) => PutAsync($"vitaminlogs/{r.CloudId}", VitaminLogJson(r));

        /// <summary>Envia todos os dados locais (usado ao criar/entrar numa família).</summary>
        public async Task PushAllLocalAsync()
        {
            if (!Enabled) return;
            try
            {
                var baby = await DatabaseService.GetOrCreateBabyAsync();
                await PushBabyAsync(baby);
                foreach (var r in await DatabaseService.GetAllFeedingsAsync()) await PushFeedingAsync(r);
                foreach (var r in await DatabaseService.GetAllDiapersAsync()) await PushDiaperAsync(r);
                foreach (var r in await DatabaseService.GetAllWatersAsync()) await PushWaterAsync(r);
                foreach (var r in await DatabaseService.GetAllSleepsAsync()) await PushSleepAsync(r);
                foreach (var r in await DatabaseService.GetAllVitaminsAsync()) await PushVitaminAsync(r);
                foreach (var r in await DatabaseService.GetAllVitaminLogsAsync()) await PushVitaminLogAsync(r);
                foreach (var r in await DatabaseService.GetAllMilestonesAsync()) await PushMilestoneAsync(r);
            }
            catch { }
        }

        // ---------- PULL ----------
        public async Task PullAllAsync(bool force = false)
        {
            if (!Enabled) return;
            if (!force && (DateTime.UtcNow - _lastPull).TotalSeconds < 2) return;
            _lastPull = DateTime.UtcNow;

            var token = await _auth.GetValidTokenAsync();
            if (token == null) return;
            var fid = _family.FamilyId;

            try
            {
                var babyJson = await GetAsync($"families/{fid}/baby.json?auth={token}");
                if (babyJson == null) return; // rede indisponível: não trava, usa o que já tem local
                if (HasContent(babyJson))
                {
                    using var d = JsonDocument.Parse(babyJson);
                    var b = ParseBaby(d.RootElement);
                    if (b != null) await DatabaseService.UpsertBabyFromCloudAsync(b);
                }

                var baby = await DatabaseService.GetOrCreateBabyAsync();
                var localBabyId = baby.Id;

                await PullTableAsync($"families/{fid}/feedings.json?auth={token}",
                    el => { var r = ParseFeeding(el); return r != null ? DatabaseService.UpsertFeedingAsync(r, localBabyId) : Task.CompletedTask; });
                await PullTableAsync($"families/{fid}/diapers.json?auth={token}",
                    el => { var r = ParseDiaper(el); return r != null ? DatabaseService.UpsertDiaperAsync(r, localBabyId) : Task.CompletedTask; });
                await PullTableAsync($"families/{fid}/waters.json?auth={token}",
                    el => { var r = ParseWater(el); return r != null ? DatabaseService.UpsertWaterAsync(r, localBabyId) : Task.CompletedTask; });
                await PullTableAsync($"families/{fid}/sleeps.json?auth={token}",
                    el => { var r = ParseSleep(el); return r != null ? DatabaseService.UpsertSleepAsync(r, localBabyId) : Task.CompletedTask; });
                await PullTableAsync($"families/{fid}/vitamins.json?auth={token}",
                    el => { var r = ParseVitamin(el); return r != null ? DatabaseService.UpsertVitaminAsync(r, localBabyId) : Task.CompletedTask; });
                // Logs depois das vitaminas, para mapear o VitaminCloudId ao id local.
                await PullTableAsync($"families/{fid}/vitaminlogs.json?auth={token}",
                    el => { var r = ParseVitaminLog(el); return r != null ? DatabaseService.UpsertVitaminLogAsync(r) : Task.CompletedTask; });
                await PullTableAsync($"families/{fid}/milestones.json?auth={token}",
                    el => { var r = ParseMilestone(el); return r != null ? DatabaseService.UpsertMilestoneAsync(r, localBabyId) : Task.CompletedTask; });
            }
            catch { }
        }

        private async Task PullTableAsync(string path, Func<JsonElement, Task> upsert)
        {
            var json = await GetAsync(path);
            if (!HasContent(json)) return;
            try
            {
                using var doc = JsonDocument.Parse(json);
                foreach (var prop in doc.RootElement.EnumerateObject())
                    await upsert(prop.Value);
            }
            catch { }
        }

        private static async Task<string?> GetAsync(string path)
        {
            try { return await Http.GetStringAsync($"{FirebaseConfig.DatabaseUrl}/{path}"); }
            catch { return null; }
        }

        private static bool HasContent(string? json)
            => !string.IsNullOrWhiteSpace(json) && json != "null";

        // ---------- JSON helpers ----------
        private static string S(string? v) => JsonSerializer.Serialize(v ?? "");
        private static string B(bool v) => v ? "true" : "false";
        private static string Num(double v) => v.ToString(CultureInfo.InvariantCulture);

        private static string GetStr(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? (v.GetString() ?? "") : "";
        private static long GetLong(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.TryGetInt64(out var n) ? n : 0;
        private static int GetInt(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.TryGetInt32(out var n) ? n : 0;
        private static bool GetBool(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.True;
        private static double? GetNullableDouble(JsonElement e, string p)
            => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var n) ? n : (double?)null;
        private static long? GetNullableLong(JsonElement e, string p)
            => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n) ? n : (long?)null;
        private static int? GetNullableInt(JsonElement e, string p)
            => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n) ? n : (int?)null;

        // ---------- serialização por tabela ----------
        private static string BabyJson(Baby b) =>
            "{" + $"\"name\":{S(b.Name)},\"birthTicks\":{b.BirthDate.Ticks},\"weight\":{Num(b.BirthWeightGrams)}," +
            $"\"feedingType\":{(int)b.FeedingType},\"cloudId\":{S(b.CloudId)},\"updatedAt\":{b.UpdatedAt},\"isDeleted\":{B(b.IsDeleted)}" + "}";

        private static Baby? ParseBaby(JsonElement e)
        {
            if (e.ValueKind != JsonValueKind.Object) return null;
            return new Baby
            {
                Name = GetStr(e, "name"),
                BirthDate = new DateTime(GetLong(e, "birthTicks")),
                BirthWeightGrams = GetNullableDouble(e, "weight") ?? 3000,
                FeedingType = (FeedingType)GetInt(e, "feedingType"),
                CloudId = GetStr(e, "cloudId"),
                UpdatedAt = GetLong(e, "updatedAt"),
                IsDeleted = GetBool(e, "isDeleted")
            };
        }

        private static string FeedingJson(FeedingRecord r) =>
            "{" + $"\"cloudId\":{S(r.CloudId)},\"updatedAt\":{r.UpdatedAt},\"isDeleted\":{B(r.IsDeleted)}," +
            $"\"startTicks\":{r.StartTime.Ticks},\"duration\":{r.DurationMinutes},\"side\":{(int)r.Side}," +
            $"\"bottleMl\":{(r.BottleAmountMl.HasValue ? Num(r.BottleAmountMl.Value) : "null")},\"notes\":{S(r.Notes)}" + "}";

        private static FeedingRecord? ParseFeeding(JsonElement e)
        {
            if (e.ValueKind != JsonValueKind.Object) return null;
            return new FeedingRecord
            {
                CloudId = GetStr(e, "cloudId"),
                UpdatedAt = GetLong(e, "updatedAt"),
                IsDeleted = GetBool(e, "isDeleted"),
                StartTime = new DateTime(GetLong(e, "startTicks")),
                DurationMinutes = GetInt(e, "duration"),
                Side = (BreastSide)GetInt(e, "side"),
                BottleAmountMl = GetNullableDouble(e, "bottleMl"),
                Notes = GetStr(e, "notes")
            };
        }

        private static string DiaperJson(DiaperRecord r) =>
            "{" + $"\"cloudId\":{S(r.CloudId)},\"updatedAt\":{r.UpdatedAt},\"isDeleted\":{B(r.IsDeleted)}," +
            $"\"timeTicks\":{r.Time.Ticks},\"isWet\":{B(r.IsWet)},\"isDirty\":{B(r.IsDirty)}," +
            $"\"color\":{(r.Color.HasValue ? ((int)r.Color.Value).ToString() : "null")}," +
            $"\"consistency\":{(r.Consistency.HasValue ? ((int)r.Consistency.Value).ToString() : "null")},\"notes\":{S(r.Notes)}" + "}";

        private static DiaperRecord? ParseDiaper(JsonElement e)
        {
            if (e.ValueKind != JsonValueKind.Object) return null;
            var color = GetNullableInt(e, "color");
            var cons = GetNullableInt(e, "consistency");
            return new DiaperRecord
            {
                CloudId = GetStr(e, "cloudId"),
                UpdatedAt = GetLong(e, "updatedAt"),
                IsDeleted = GetBool(e, "isDeleted"),
                Time = new DateTime(GetLong(e, "timeTicks")),
                IsWet = GetBool(e, "isWet"),
                IsDirty = GetBool(e, "isDirty"),
                Color = color.HasValue ? (StoolColor)color.Value : (StoolColor?)null,
                Consistency = cons.HasValue ? (StoolConsistency)cons.Value : (StoolConsistency?)null,
                Notes = GetStr(e, "notes")
            };
        }

        private static string WaterJson(MotherWaterRecord r) =>
            "{" + $"\"cloudId\":{S(r.CloudId)},\"updatedAt\":{r.UpdatedAt},\"isDeleted\":{B(r.IsDeleted)}," +
            $"\"timeTicks\":{r.Time.Ticks},\"volumeMl\":{r.VolumeMl}" + "}";

        private static MotherWaterRecord? ParseWater(JsonElement e)
        {
            if (e.ValueKind != JsonValueKind.Object) return null;
            return new MotherWaterRecord
            {
                CloudId = GetStr(e, "cloudId"),
                UpdatedAt = GetLong(e, "updatedAt"),
                IsDeleted = GetBool(e, "isDeleted"),
                Time = new DateTime(GetLong(e, "timeTicks")),
                VolumeMl = GetInt(e, "volumeMl")
            };
        }

        private static string SleepJson(SleepRecord r) =>
            "{" + $"\"cloudId\":{S(r.CloudId)},\"updatedAt\":{r.UpdatedAt},\"isDeleted\":{B(r.IsDeleted)}," +
            $"\"startTicks\":{r.Start.Ticks},\"endTicks\":{r.End.Ticks},\"isOngoing\":{B(r.IsOngoing)}" + "}";

        private static SleepRecord? ParseSleep(JsonElement e)
        {
            if (e.ValueKind != JsonValueKind.Object) return null;
            return new SleepRecord
            {
                CloudId = GetStr(e, "cloudId"),
                UpdatedAt = GetLong(e, "updatedAt"),
                IsDeleted = GetBool(e, "isDeleted"),
                Start = new DateTime(GetLong(e, "startTicks")),
                End = new DateTime(GetLong(e, "endTicks")),
                IsOngoing = GetBool(e, "isOngoing")
            };
        }

        private static string VitaminJson(VitaminSchedule r) =>
            "{" + $"\"cloudId\":{S(r.CloudId)},\"updatedAt\":{r.UpdatedAt},\"isDeleted\":{B(r.IsDeleted)}," +
            $"\"name\":{S(r.Name)},\"timeTicks\":{r.TimeOfDayTicks},\"isActive\":{B(r.IsActive)},\"isMother\":{B(r.IsMother)}" + "}";

        private static VitaminSchedule? ParseVitamin(JsonElement e)
        {
            if (e.ValueKind != JsonValueKind.Object) return null;
            return new VitaminSchedule
            {
                CloudId = GetStr(e, "cloudId"),
                UpdatedAt = GetLong(e, "updatedAt"),
                IsDeleted = GetBool(e, "isDeleted"),
                Name = GetStr(e, "name"),
                TimeOfDayTicks = GetLong(e, "timeTicks"),
                IsActive = GetBool(e, "isActive"),
                IsMother = GetBool(e, "isMother")
            };
        }

        private static string MilestoneJson(Milestone r) =>
            "{" + $"\"cloudId\":{S(r.CloudId)},\"updatedAt\":{r.UpdatedAt},\"isDeleted\":{B(r.IsDeleted)}," +
            $"\"icon\":{S(r.Icon)},\"title\":{S(r.Title)}," +
            $"\"achievedTicks\":{(r.AchievedDate.HasValue ? r.AchievedDate.Value.Ticks.ToString() : "null")}," +
            $"\"notes\":{S(r.Notes)},\"sortOrder\":{r.SortOrder},\"isCustom\":{B(r.IsCustom)}" + "}";

        private static string VitaminLogJson(VitaminLog r) =>
            "{" + $"\"cloudId\":{S(r.CloudId)},\"updatedAt\":{r.UpdatedAt},\"isDeleted\":{B(r.IsDeleted)}," +
            $"\"vitaminCloudId\":{S(r.VitaminCloudId)},\"dateTicks\":{r.Date.Ticks},\"takenTicks\":{r.TakenAt.Ticks}" + "}";

        private static VitaminLog? ParseVitaminLog(JsonElement e)
        {
            if (e.ValueKind != JsonValueKind.Object) return null;
            return new VitaminLog
            {
                CloudId = GetStr(e, "cloudId"),
                UpdatedAt = GetLong(e, "updatedAt"),
                IsDeleted = GetBool(e, "isDeleted"),
                VitaminCloudId = GetStr(e, "vitaminCloudId"),
                Date = new DateTime(GetLong(e, "dateTicks")),
                TakenAt = new DateTime(GetLong(e, "takenTicks"))
            };
        }

        private static Milestone? ParseMilestone(JsonElement e)
        {
            if (e.ValueKind != JsonValueKind.Object) return null;
            var achieved = GetNullableLong(e, "achievedTicks");
            return new Milestone
            {
                CloudId = GetStr(e, "cloudId"),
                UpdatedAt = GetLong(e, "updatedAt"),
                IsDeleted = GetBool(e, "isDeleted"),
                Icon = GetStr(e, "icon"),
                Title = GetStr(e, "title"),
                AchievedDate = achieved.HasValue ? new DateTime(achieved.Value) : (DateTime?)null,
                Notes = GetStr(e, "notes"),
                SortOrder = GetInt(e, "sortOrder"),
                IsCustom = GetBool(e, "isCustom")
            };
        }
    }
}
