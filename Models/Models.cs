using System;
using System.Collections.Generic;
using Microsoft.Maui.Graphics;
using SQLite;

namespace BabyTrackerMobile.Models
{
    public enum FeedingType { Breast, Formula, Mixed }
    public enum BreastSide { Left, Right, Both, Bottle }
    public enum StoolColor { Meconium, DarkGreen, Yellow, Brown, White, Red }
    public enum StoolConsistency { Liquid, Seedy, Soft, Hard }
    public enum HealthStatus { Normal, Attention, Alert }

    [Table("Babies")]
    public class Baby
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime BirthDate { get; set; } = DateTime.Today;
        public double BirthWeightGrams { get; set; } = 3000;
        public FeedingType FeedingType { get; set; } = FeedingType.Breast;
    }

    [Table("Feedings")]
    public class FeedingRecord
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public int BabyId { get; set; }
        public DateTime StartTime { get; set; } = DateTime.Now;
        public int DurationMinutes { get; set; } = 15;
        public BreastSide Side { get; set; } = BreastSide.Left;
        public double? BottleAmountMl { get; set; }
        public string Notes { get; set; } = string.Empty;
    }

    [Table("Diapers")]
    public class DiaperRecord
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public int BabyId { get; set; }
        public DateTime Time { get; set; } = DateTime.Now;
        public bool IsWet { get; set; }
        public bool IsDirty { get; set; }
        public StoolColor? Color { get; set; }
        public StoolConsistency? Consistency { get; set; }
        public string Notes { get; set; } = string.Empty;
    }

    [Table("Weights")]
    public class WeightRecord
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public int BabyId { get; set; }
        public DateTime Date { get; set; } = DateTime.Today;
        public double WeightGrams { get; set; }
    }

    [Table("MotherWaters")]
    public class MotherWaterRecord
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public int BabyId { get; set; }
        public DateTime Time { get; set; } = DateTime.Now;
        public int VolumeMl { get; set; }
    }

    [Table("VitaminSchedules")]
    public class VitaminSchedule
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public int BabyId { get; set; }
        public string Name { get; set; } = "Vitamina D";
        /// <summary>Horário diário armazenado como ticks de TimeSpan.</summary>
        public long TimeOfDayTicks { get; set; }
        public bool IsActive { get; set; } = true;

        [Ignore]
        public TimeSpan TimeOfDay
        {
            get => TimeSpan.FromTicks(TimeOfDayTicks);
            set => TimeOfDayTicks = value.Ticks;
        }

        [Ignore]
        public string TimeDisplay => TimeOfDay.ToString(@"hh\:mm");
    }

    /// <summary>Registro de que a vitamina foi tomada em determinado dia.</summary>
    [Table("VitaminLogs")]
    public class VitaminLog
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public int VitaminId { get; set; }
        public DateTime Date { get; set; } = DateTime.Today;
        public DateTime TakenAt { get; set; } = DateTime.Now;
    }

    public class GuidelineRange
    {
        public int Minimum { get; set; }
        public int Maximum { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    public class DailyAnalysis
    {
        public DateTime Date { get; set; }
        public int DayOfLife { get; set; }
        public int FeedingCount { get; set; }
        public int TotalFeedingMinutes { get; set; }
        public int WetDiaperCount { get; set; }
        public int DirtyDiaperCount { get; set; }
        public GuidelineRange ExpectedFeedings { get; set; } = new();
        public GuidelineRange ExpectedWetDiapers { get; set; } = new();
        public GuidelineRange ExpectedDirtyDiapers { get; set; } = new();
        public HealthStatus FeedingStatus { get; set; }
        public HealthStatus WetDiaperStatus { get; set; }
        public HealthStatus DirtyDiaperStatus { get; set; }
        public List<AlertInfo> Alerts { get; set; } = new();
        public TimeSpan? TimeSinceLastFeeding { get; set; }
    }

    public class AlertInfo
    {
        public HealthStatus Severity { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Icon => Severity switch {
            HealthStatus.Alert => "🚨",
            HealthStatus.Attention => "⚠️",
            _ => "ℹ️"
        };
        public Color SeverityColor => Severity switch {
            HealthStatus.Alert => Color.FromArgb("#F44336"),
            HealthStatus.Attention => Color.FromArgb("#FF9800"),
            _ => Color.FromArgb("#4CAF50")
        };
    }
}
