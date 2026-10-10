using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;
using SQLite;
using BabyTrackerMobile.Models;
using BabyTrackerMobile.Services;

namespace BabyTrackerMobile.Data
{
    public class DatabaseService
    {
        private static SQLiteAsyncConnection? _database;

        /// <summary>Motor de sincronização online. Setado na inicialização do app.</summary>
        public static CloudSyncService? Cloud;

        private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        private static void Stamp(ISyncEntity e)
        {
            if (string.IsNullOrEmpty(e.CloudId)) e.CloudId = Guid.NewGuid().ToString("N");
            e.UpdatedAt = Now();
        }

        private static async Task<SQLiteAsyncConnection> GetDatabaseAsync()
        {
            if (_database == null)
            {
                var dbPath = Path.Combine(FileSystem.AppDataDirectory, "babytracker.db");
                _database = new SQLiteAsyncConnection(dbPath, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);
                await _database.CreateTableAsync<Baby>();
                await _database.CreateTableAsync<FeedingRecord>();
                await _database.CreateTableAsync<DiaperRecord>();
                await _database.CreateTableAsync<WeightRecord>();
                await _database.CreateTableAsync<MotherWaterRecord>();
                await _database.CreateTableAsync<SleepRecord>();
                await _database.CreateTableAsync<VitaminSchedule>();
                await _database.CreateTableAsync<VitaminLog>();
                await _database.CreateTableAsync<Milestone>();
            }
            return _database;
        }

        /// <summary>Dá CloudId a registros antigos que ainda não têm (migração).</summary>
        public static async Task BackfillCloudIdsAsync()
        {
            var db = await GetDatabaseAsync();
            await BackfillAsync(await db.Table<Baby>().ToListAsync(), db);
            await BackfillAsync(await db.Table<FeedingRecord>().ToListAsync(), db);
            await BackfillAsync(await db.Table<DiaperRecord>().ToListAsync(), db);
            await BackfillAsync(await db.Table<MotherWaterRecord>().ToListAsync(), db);
            await BackfillAsync(await db.Table<SleepRecord>().ToListAsync(), db);
            await BackfillAsync(await db.Table<VitaminSchedule>().ToListAsync(), db);
            await BackfillAsync(await db.Table<Milestone>().ToListAsync(), db);

            // VitaminLog precisa também do CloudId da vitamina.
            var logs = await db.Table<VitaminLog>().ToListAsync();
            foreach (var l in logs)
            {
                if (!string.IsNullOrEmpty(l.CloudId)) continue;
                var vit = await db.FindAsync<VitaminSchedule>(l.VitaminId);
                l.VitaminCloudId = vit?.CloudId ?? "";
                l.CloudId = LogCloudId(l.VitaminCloudId, l.VitaminId, l.Date);
                l.UpdatedAt = Now();
                await db.UpdateAsync(l);
            }
        }

        private static string LogCloudId(string vitaminCloudId, int vitaminId, DateTime date)
            => !string.IsNullOrEmpty(vitaminCloudId)
                ? $"{vitaminCloudId}_{date:yyyyMMdd}"
                : $"local{vitaminId}_{date:yyyyMMdd}";

        private static async Task BackfillAsync<T>(List<T> rows, SQLiteAsyncConnection db) where T : ISyncEntity
        {
            foreach (var r in rows)
            {
                if (string.IsNullOrEmpty(r.CloudId))
                {
                    Stamp(r);
                    await db.UpdateAsync(r);
                }
            }
        }

        // ---------------- Baby ----------------
        public static async Task<Baby?> GetFirstBabyAsync()
        {
            var db = await GetDatabaseAsync();
            return await db.Table<Baby>().Where(b => !b.IsDeleted).FirstOrDefaultAsync();
        }

        public static async Task<Baby> GetOrCreateBabyAsync()
        {
            var baby = await GetFirstBabyAsync();
            if (baby != null) return baby;

            baby = new Baby
            {
                Name = "Antonio",
                BirthDate = DateTime.Today,
                BirthWeightGrams = 3000,
                FeedingType = FeedingType.Breast
            };
            Stamp(baby);
            var db = await GetDatabaseAsync();
            await db.InsertAsync(baby);
            return baby;
        }

        public static async Task SaveBabyAsync(Baby baby)
        {
            var db = await GetDatabaseAsync();
            Stamp(baby);
            if (baby.Id > 0)
                await db.UpdateAsync(baby);
            else
                await db.InsertAsync(baby);
            if (Cloud != null) await Cloud.PushBabyAsync(baby);
        }

        public static async Task UpsertBabyFromCloudAsync(Baby incoming)
        {
            if (string.IsNullOrEmpty(incoming.CloudId)) return;
            var db = await GetDatabaseAsync();
            var local = await db.Table<Baby>().FirstOrDefaultAsync();
            if (local == null) { incoming.Id = 0; await db.InsertAsync(incoming); return; }
            if (incoming.UpdatedAt >= local.UpdatedAt) { incoming.Id = local.Id; await db.UpdateAsync(incoming); }
        }

        // ---------------- Feedings ----------------
        public static async Task AddFeedingAsync(FeedingRecord record)
        {
            var db = await GetDatabaseAsync();
            Stamp(record);
            await db.InsertAsync(record);
            if (Cloud != null) await Cloud.PushFeedingAsync(record);
        }

        public static async Task UpdateFeedingAsync(FeedingRecord record)
        {
            var db = await GetDatabaseAsync();
            Stamp(record);
            await db.UpdateAsync(record);
            if (Cloud != null) await Cloud.PushFeedingAsync(record);
        }

        public static async Task DeleteFeedingAsync(int id)
        {
            var db = await GetDatabaseAsync();
            var r = await db.FindAsync<FeedingRecord>(id);
            if (r == null) return;
            r.IsDeleted = true; Stamp(r);
            await db.UpdateAsync(r);
            if (Cloud != null) await Cloud.PushFeedingAsync(r);
        }

        public static async Task<List<FeedingRecord>> GetFeedingsForDateAsync(int babyId, DateTime date)
        {
            var db = await GetDatabaseAsync();
            var start = date.Date;
            var end = date.Date.AddDays(1);
            return await db.Table<FeedingRecord>()
                .Where(f => f.BabyId == babyId && !f.IsDeleted && f.StartTime >= start && f.StartTime < end)
                .OrderByDescending(f => f.StartTime)
                .ToListAsync();
        }

        public static async Task<List<FeedingRecord>> GetAllFeedingsAsync()
            => await (await GetDatabaseAsync()).Table<FeedingRecord>().ToListAsync();

        public static async Task UpsertFeedingAsync(FeedingRecord incoming, int localBabyId)
        {
            if (string.IsNullOrEmpty(incoming.CloudId)) return;
            var db = await GetDatabaseAsync();
            incoming.BabyId = localBabyId;
            var existing = await db.Table<FeedingRecord>().Where(x => x.CloudId == incoming.CloudId).FirstOrDefaultAsync();
            if (existing == null) { incoming.Id = 0; await db.InsertAsync(incoming); }
            else if (incoming.UpdatedAt >= existing.UpdatedAt) { incoming.Id = existing.Id; await db.UpdateAsync(incoming); }
        }

        // ---------------- Diapers ----------------
        public static async Task AddDiaperAsync(DiaperRecord record)
        {
            var db = await GetDatabaseAsync();
            Stamp(record);
            await db.InsertAsync(record);
            if (Cloud != null) await Cloud.PushDiaperAsync(record);
        }

        public static async Task UpdateDiaperAsync(DiaperRecord record)
        {
            var db = await GetDatabaseAsync();
            Stamp(record);
            await db.UpdateAsync(record);
            if (Cloud != null) await Cloud.PushDiaperAsync(record);
        }

        public static async Task DeleteDiaperAsync(int id)
        {
            var db = await GetDatabaseAsync();
            var r = await db.FindAsync<DiaperRecord>(id);
            if (r == null) return;
            r.IsDeleted = true; Stamp(r);
            await db.UpdateAsync(r);
            if (Cloud != null) await Cloud.PushDiaperAsync(r);
        }

        public static async Task<List<DiaperRecord>> GetDiapersForDateAsync(int babyId, DateTime date)
        {
            var db = await GetDatabaseAsync();
            var start = date.Date;
            var end = date.Date.AddDays(1);
            return await db.Table<DiaperRecord>()
                .Where(d => d.BabyId == babyId && !d.IsDeleted && d.Time >= start && d.Time < end)
                .OrderByDescending(d => d.Time)
                .ToListAsync();
        }

        public static async Task<List<DiaperRecord>> GetAllDiapersAsync()
            => await (await GetDatabaseAsync()).Table<DiaperRecord>().ToListAsync();

        public static async Task UpsertDiaperAsync(DiaperRecord incoming, int localBabyId)
        {
            if (string.IsNullOrEmpty(incoming.CloudId)) return;
            var db = await GetDatabaseAsync();
            incoming.BabyId = localBabyId;
            var existing = await db.Table<DiaperRecord>().Where(x => x.CloudId == incoming.CloudId).FirstOrDefaultAsync();
            if (existing == null) { incoming.Id = 0; await db.InsertAsync(incoming); }
            else if (incoming.UpdatedAt >= existing.UpdatedAt) { incoming.Id = existing.Id; await db.UpdateAsync(incoming); }
        }

        // ---------------- Weight (local) ----------------
        public static async Task AddWeightAsync(WeightRecord record)
        {
            var db = await GetDatabaseAsync();
            await db.InsertAsync(record);
        }

        public static async Task<List<WeightRecord>> GetWeightsForBabyAsync(int babyId)
        {
            var db = await GetDatabaseAsync();
            return await db.Table<WeightRecord>()
                .Where(w => w.BabyId == babyId)
                .OrderByDescending(w => w.Date)
                .ToListAsync();
        }

        // ---------------- Mother water ----------------
        public static async Task AddMotherWaterAsync(MotherWaterRecord record)
        {
            var db = await GetDatabaseAsync();
            Stamp(record);
            await db.InsertAsync(record);
            if (Cloud != null) await Cloud.PushWaterAsync(record);
        }

        public static async Task<List<MotherWaterRecord>> GetMotherWatersForDateAsync(int babyId, DateTime date)
        {
            var db = await GetDatabaseAsync();
            var start = date.Date;
            var end = date.Date.AddDays(1);
            return await db.Table<MotherWaterRecord>()
                .Where(w => w.BabyId == babyId && !w.IsDeleted && w.Time >= start && w.Time < end)
                .OrderByDescending(w => w.Time)
                .ToListAsync();
        }

        public static async Task DeleteMotherWaterAsync(int id)
        {
            var db = await GetDatabaseAsync();
            var r = await db.FindAsync<MotherWaterRecord>(id);
            if (r == null) return;
            r.IsDeleted = true; Stamp(r);
            await db.UpdateAsync(r);
            if (Cloud != null) await Cloud.PushWaterAsync(r);
        }

        public static async Task<List<MotherWaterRecord>> GetAllWatersAsync()
            => await (await GetDatabaseAsync()).Table<MotherWaterRecord>().ToListAsync();

        public static async Task UpsertWaterAsync(MotherWaterRecord incoming, int localBabyId)
        {
            if (string.IsNullOrEmpty(incoming.CloudId)) return;
            var db = await GetDatabaseAsync();
            incoming.BabyId = localBabyId;
            var existing = await db.Table<MotherWaterRecord>().Where(x => x.CloudId == incoming.CloudId).FirstOrDefaultAsync();
            if (existing == null) { incoming.Id = 0; await db.InsertAsync(incoming); }
            else if (incoming.UpdatedAt >= existing.UpdatedAt) { incoming.Id = existing.Id; await db.UpdateAsync(incoming); }
        }

        // ---------------- Sono ----------------
        public static async Task AddSleepAsync(SleepRecord record)
        {
            var db = await GetDatabaseAsync();
            Stamp(record);
            await db.InsertAsync(record);
            if (Cloud != null) await Cloud.PushSleepAsync(record);
        }

        public static async Task UpdateSleepAsync(SleepRecord record)
        {
            var db = await GetDatabaseAsync();
            Stamp(record);
            await db.UpdateAsync(record);
            if (Cloud != null) await Cloud.PushSleepAsync(record);
        }

        public static async Task DeleteSleepAsync(int id)
        {
            var db = await GetDatabaseAsync();
            var r = await db.FindAsync<SleepRecord>(id);
            if (r == null) return;
            r.IsDeleted = true; Stamp(r);
            await db.UpdateAsync(r);
            if (Cloud != null) await Cloud.PushSleepAsync(r);
        }

        public static async Task<List<SleepRecord>> GetSleepsForDateAsync(int babyId, DateTime date)
        {
            var db = await GetDatabaseAsync();
            var start = date.Date;
            var end = date.Date.AddDays(1);
            return await db.Table<SleepRecord>()
                .Where(s => s.BabyId == babyId && !s.IsDeleted && s.Start >= start && s.Start < end)
                .OrderByDescending(s => s.Start)
                .ToListAsync();
        }

        public static async Task<List<SleepRecord>> GetAllSleepsAsync()
            => await (await GetDatabaseAsync()).Table<SleepRecord>().ToListAsync();

        public static async Task UpsertSleepAsync(SleepRecord incoming, int localBabyId)
        {
            if (string.IsNullOrEmpty(incoming.CloudId)) return;
            var db = await GetDatabaseAsync();
            incoming.BabyId = localBabyId;
            var existing = await db.Table<SleepRecord>().Where(x => x.CloudId == incoming.CloudId).FirstOrDefaultAsync();
            if (existing == null) { incoming.Id = 0; await db.InsertAsync(incoming); }
            else if (incoming.UpdatedAt >= existing.UpdatedAt) { incoming.Id = existing.Id; await db.UpdateAsync(incoming); }
        }

        // ---------------- Conquistas ----------------
        private static readonly (string Icon, string Title)[] DefaultMilestones =
        {
            ("😊", "Primeiro sorriso"),
            ("🙆", "Firmou o pescoço"),
            ("🔄", "Primeira rolada"),
            ("🪑", "Sentou sozinho"),
            ("🐛", "Primeira engatinhada"),
            ("🦷", "Primeiro dentinho"),
            ("🧍", "Ficou em pé"),
            ("🗣️", "Primeira palavra"),
            ("👣", "Primeira andada"),
        };

        public static async Task EnsureMilestonesSeededAsync(int babyId)
        {
            var db = await GetDatabaseAsync();
            var count = await db.Table<Milestone>().Where(m => m.BabyId == babyId).CountAsync();
            if (count > 0) return;
            var order = 0;
            foreach (var (icon, title) in DefaultMilestones)
            {
                // CloudId determinístico para os marcos padrão, para os dois celulares
                // não duplicarem ao semear a mesma lista.
                var m = new Milestone
                {
                    BabyId = babyId,
                    Icon = icon,
                    Title = title,
                    SortOrder = order,
                    IsCustom = false,
                    CloudId = $"seed{order}",
                    UpdatedAt = Now()
                };
                await db.InsertAsync(m);
                if (Cloud != null) await Cloud.PushMilestoneAsync(m);
                order++;
            }
        }

        public static async Task<List<Milestone>> GetMilestonesAsync(int babyId)
        {
            var db = await GetDatabaseAsync();
            return await db.Table<Milestone>()
                .Where(m => m.BabyId == babyId && !m.IsDeleted)
                .OrderBy(m => m.SortOrder)
                .ToListAsync();
        }

        public static async Task<List<Milestone>> GetMilestonesAchievedOnAsync(int babyId, DateTime date)
        {
            var db = await GetDatabaseAsync();
            var start = date.Date;
            var end = start.AddDays(1);
            return await db.Table<Milestone>()
                .Where(m => m.BabyId == babyId && !m.IsDeleted && m.AchievedDate >= start && m.AchievedDate < end)
                .ToListAsync();
        }

        public static async Task SaveMilestoneAsync(Milestone milestone)
        {
            var db = await GetDatabaseAsync();
            Stamp(milestone);
            if (milestone.Id > 0)
                await db.UpdateAsync(milestone);
            else
                await db.InsertAsync(milestone);
            if (Cloud != null) await Cloud.PushMilestoneAsync(milestone);
        }

        public static async Task DeleteMilestoneAsync(int id)
        {
            var db = await GetDatabaseAsync();
            var r = await db.FindAsync<Milestone>(id);
            if (r == null) return;
            r.IsDeleted = true; Stamp(r);
            await db.UpdateAsync(r);
            if (Cloud != null) await Cloud.PushMilestoneAsync(r);
        }

        public static async Task<int> CountAchievedMilestonesAsync(int babyId)
        {
            var db = await GetDatabaseAsync();
            return await db.Table<Milestone>()
                .Where(m => m.BabyId == babyId && !m.IsDeleted && m.AchievedDate != null)
                .CountAsync();
        }

        public static async Task<int> CountMilestonesAsync(int babyId)
        {
            var db = await GetDatabaseAsync();
            return await db.Table<Milestone>().Where(m => m.BabyId == babyId && !m.IsDeleted).CountAsync();
        }

        public static async Task<List<Milestone>> GetAllMilestonesAsync()
            => await (await GetDatabaseAsync()).Table<Milestone>().ToListAsync();

        public static async Task UpsertMilestoneAsync(Milestone incoming, int localBabyId)
        {
            if (string.IsNullOrEmpty(incoming.CloudId)) return;
            var db = await GetDatabaseAsync();
            incoming.BabyId = localBabyId;
            var existing = await db.Table<Milestone>().Where(x => x.CloudId == incoming.CloudId).FirstOrDefaultAsync();
            if (existing == null) { incoming.Id = 0; await db.InsertAsync(incoming); }
            else if (incoming.UpdatedAt >= existing.UpdatedAt) { incoming.Id = existing.Id; await db.UpdateAsync(incoming); }
        }

        // ---------------- Vitaminas ----------------
        public static async Task<List<VitaminSchedule>> GetVitaminsAsync(int babyId)
        {
            var db = await GetDatabaseAsync();
            return await db.Table<VitaminSchedule>()
                .Where(v => v.BabyId == babyId && !v.IsDeleted)
                .OrderBy(v => v.TimeOfDayTicks)
                .ToListAsync();
        }

        public static async Task<int> SaveVitaminAsync(VitaminSchedule vitamin)
        {
            var db = await GetDatabaseAsync();
            Stamp(vitamin);
            if (vitamin.Id > 0)
                await db.UpdateAsync(vitamin);
            else
                await db.InsertAsync(vitamin);
            if (Cloud != null) await Cloud.PushVitaminAsync(vitamin);
            return vitamin.Id;
        }

        public static async Task DeleteVitaminAsync(int id)
        {
            var db = await GetDatabaseAsync();
            var r = await db.FindAsync<VitaminSchedule>(id);
            if (r == null) return;
            r.IsDeleted = true; Stamp(r);
            await db.UpdateAsync(r);
            await db.ExecuteAsync("DELETE FROM VitaminLogs WHERE VitaminId = ?", id);
            if (Cloud != null) await Cloud.PushVitaminAsync(r);
        }

        public static async Task<List<VitaminSchedule>> GetAllVitaminsAsync()
            => await (await GetDatabaseAsync()).Table<VitaminSchedule>().ToListAsync();

        public static async Task UpsertVitaminAsync(VitaminSchedule incoming, int localBabyId)
        {
            if (string.IsNullOrEmpty(incoming.CloudId)) return;
            var db = await GetDatabaseAsync();
            incoming.BabyId = localBabyId;
            var existing = await db.Table<VitaminSchedule>().Where(x => x.CloudId == incoming.CloudId).FirstOrDefaultAsync();
            if (existing == null) { incoming.Id = 0; await db.InsertAsync(incoming); }
            else if (incoming.UpdatedAt >= existing.UpdatedAt) { incoming.Id = existing.Id; await db.UpdateAsync(incoming); }
        }

        // ---- "vitamina tomada hoje" (sincronizado) ----
        public static async Task<HashSet<int>> GetTakenVitaminIdsAsync(DateTime date)
        {
            var db = await GetDatabaseAsync();
            var start = date.Date;
            var end = start.AddDays(1);
            var logs = await db.Table<VitaminLog>()
                .Where(l => l.Date >= start && l.Date < end && !l.IsDeleted)
                .ToListAsync();
            var set = new HashSet<int>();
            foreach (var l in logs) set.Add(l.VitaminId);
            return set;
        }

        public static async Task SetVitaminTakenAsync(int vitaminId, DateTime date, bool taken)
        {
            var db = await GetDatabaseAsync();
            var day = date.Date;
            var vit = await db.FindAsync<VitaminSchedule>(vitaminId);
            var vitCloud = vit?.CloudId ?? "";
            var cloudId = LogCloudId(vitCloud, vitaminId, day);

            var existing = await db.Table<VitaminLog>().Where(l => l.CloudId == cloudId).FirstOrDefaultAsync();
            if (existing == null)
                existing = new VitaminLog { Date = day, CloudId = cloudId };

            existing.VitaminId = vitaminId;
            existing.VitaminCloudId = vitCloud;
            existing.TakenAt = DateTime.Now;
            existing.IsDeleted = !taken;
            existing.UpdatedAt = Now();

            if (existing.Id > 0) await db.UpdateAsync(existing);
            else await db.InsertAsync(existing);

            if (Cloud != null) await Cloud.PushVitaminLogAsync(existing);
        }

        public static async Task<List<VitaminLog>> GetAllVitaminLogsAsync()
            => await (await GetDatabaseAsync()).Table<VitaminLog>().ToListAsync();

        public static async Task UpsertVitaminLogAsync(VitaminLog incoming)
        {
            if (string.IsNullOrEmpty(incoming.CloudId)) return;
            var db = await GetDatabaseAsync();
            if (!string.IsNullOrEmpty(incoming.VitaminCloudId))
            {
                var vit = await db.Table<VitaminSchedule>().Where(v => v.CloudId == incoming.VitaminCloudId).FirstOrDefaultAsync();
                if (vit != null) incoming.VitaminId = vit.Id;
            }
            var existing = await db.Table<VitaminLog>().Where(l => l.CloudId == incoming.CloudId).FirstOrDefaultAsync();
            if (existing == null) { incoming.Id = 0; await db.InsertAsync(incoming); }
            else if (incoming.UpdatedAt >= existing.UpdatedAt) { incoming.Id = existing.Id; await db.UpdateAsync(incoming); }
        }
    }
}
