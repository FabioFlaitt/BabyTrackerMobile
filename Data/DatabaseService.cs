using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;
using SQLite;
using BabyTrackerMobile.Models;

namespace BabyTrackerMobile.Data
{
    public class DatabaseService
    {
        private static SQLiteAsyncConnection? _database;

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
                await _database.CreateTableAsync<VitaminSchedule>();
                await _database.CreateTableAsync<VitaminLog>();
            }
            return _database;
        }

        public static async Task<Baby?> GetFirstBabyAsync()
        {
            var db = await GetDatabaseAsync();
            return await db.Table<Baby>().FirstOrDefaultAsync();
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
            var db = await GetDatabaseAsync();
            await db.InsertAsync(baby);
            return baby;
        }

        public static async Task SaveBabyAsync(Baby baby)
        {
            var db = await GetDatabaseAsync();
            if (baby.Id > 0)
                await db.UpdateAsync(baby);
            else
                await db.InsertAsync(baby);
        }

        public static async Task AddFeedingAsync(FeedingRecord record)
        {
            var db = await GetDatabaseAsync();
            await db.InsertAsync(record);
        }

        public static async Task<List<FeedingRecord>> GetFeedingsForDateAsync(int babyId, DateTime date)
        {
            var db = await GetDatabaseAsync();
            var start = date.Date;
            var end = date.Date.AddDays(1);
            return await db.Table<FeedingRecord>()
                .Where(f => f.BabyId == babyId && f.StartTime >= start && f.StartTime < end)
                .OrderByDescending(f => f.StartTime)
                .ToListAsync();
        }

        public static async Task UpdateFeedingAsync(FeedingRecord record)
        {
            var db = await GetDatabaseAsync();
            await db.UpdateAsync(record);
        }

        public static async Task UpdateDiaperAsync(DiaperRecord record)
        {
            var db = await GetDatabaseAsync();
            await db.UpdateAsync(record);
        }

        public static async Task DeleteFeedingAsync(int id)
        {
            var db = await GetDatabaseAsync();
            await db.DeleteAsync<FeedingRecord>(id);
        }

        public static async Task AddDiaperAsync(DiaperRecord record)
        {
            var db = await GetDatabaseAsync();
            await db.InsertAsync(record);
        }

        public static async Task<List<DiaperRecord>> GetDiapersForDateAsync(int babyId, DateTime date)
        {
            var db = await GetDatabaseAsync();
            var start = date.Date;
            var end = date.Date.AddDays(1);
            return await db.Table<DiaperRecord>()
                .Where(d => d.BabyId == babyId && d.Time >= start && d.Time < end)
                .OrderByDescending(d => d.Time)
                .ToListAsync();
        }

        public static async Task DeleteDiaperAsync(int id)
        {
            var db = await GetDatabaseAsync();
            await db.DeleteAsync<DiaperRecord>(id);
        }

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

        public static async Task AddMotherWaterAsync(MotherWaterRecord record)
        {
            var db = await GetDatabaseAsync();
            await db.InsertAsync(record);
        }

        public static async Task<List<MotherWaterRecord>> GetMotherWatersForDateAsync(int babyId, DateTime date)
        {
            var db = await GetDatabaseAsync();
            var start = date.Date;
            var end = date.Date.AddDays(1);
            return await db.Table<MotherWaterRecord>()
                .Where(w => w.BabyId == babyId && w.Time >= start && w.Time < end)
                .OrderByDescending(w => w.Time)
                .ToListAsync();
        }

        public static async Task DeleteMotherWaterAsync(int id)
        {
            var db = await GetDatabaseAsync();
            await db.DeleteAsync<MotherWaterRecord>(id);
        }

        public static async Task<List<VitaminSchedule>> GetVitaminsAsync(int babyId)
        {
            var db = await GetDatabaseAsync();
            return await db.Table<VitaminSchedule>()
                .Where(v => v.BabyId == babyId)
                .OrderBy(v => v.TimeOfDayTicks)
                .ToListAsync();
        }

        public static async Task<int> SaveVitaminAsync(VitaminSchedule vitamin)
        {
            var db = await GetDatabaseAsync();
            if (vitamin.Id > 0)
            {
                await db.UpdateAsync(vitamin);
                return vitamin.Id;
            }
            await db.InsertAsync(vitamin);
            return vitamin.Id;
        }

        public static async Task DeleteVitaminAsync(int id)
        {
            var db = await GetDatabaseAsync();
            await db.DeleteAsync<VitaminSchedule>(id);
            await db.ExecuteAsync("DELETE FROM VitaminLogs WHERE VitaminId = ?", id);
        }

        /// <summary>Ids das vitaminas já marcadas como tomadas no dia.</summary>
        public static async Task<HashSet<int>> GetTakenVitaminIdsAsync(DateTime date)
        {
            var db = await GetDatabaseAsync();
            var day = date.Date;
            var logs = await db.Table<VitaminLog>().Where(l => l.Date == day).ToListAsync();
            var set = new HashSet<int>();
            foreach (var l in logs) set.Add(l.VitaminId);
            return set;
        }

        public static async Task SetVitaminTakenAsync(int vitaminId, DateTime date, bool taken)
        {
            var db = await GetDatabaseAsync();
            var day = date.Date;
            var existing = await db.Table<VitaminLog>()
                .Where(l => l.VitaminId == vitaminId && l.Date == day)
                .ToListAsync();

            if (taken)
            {
                if (existing.Count == 0)
                    await db.InsertAsync(new VitaminLog { VitaminId = vitaminId, Date = day, TakenAt = DateTime.Now });
            }
            else
            {
                foreach (var l in existing) await db.DeleteAsync<VitaminLog>(l.Id);
            }
        }
    }
}
