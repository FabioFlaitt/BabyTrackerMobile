using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Graphics;
using BabyTrackerMobile.Models;

namespace BabyTrackerMobile.Services
{
    public static class MedicalGuidelinesService
    {
        public static GuidelineRange GetExpectedFeedingRange(int dayOfLife, FeedingType feedingType)
        {
            var range = new GuidelineRange();
            if (dayOfLife == 1)
            {
                range.Minimum = 1;
                range.Maximum = 3;
                range.Description = "Primeiras mamadas de colostro (OMS)";
            }
            else if (dayOfLife == 2)
            {
                range.Minimum = 4;
                range.Maximum = 8;
                range.Description = "Aumentando frequência (AAP)";
            }
            else if (dayOfLife <= 42)
            {
                range.Minimum = 8;
                range.Maximum = 12;
                range.Description = "8-12 mamadas/dia recomendado (OMS/AAP)";
            }
            else
            {
                range.Minimum = 6;
                range.Maximum = 10;
                range.Description = "Frequência pode reduzir gradualmente (SBP)";
            }

            if (feedingType == FeedingType.Formula)
            {
                range.Minimum = Math.Max(0, range.Minimum - 2);
                range.Maximum = Math.Max(0, range.Maximum - 2);
            }

            return range;
        }

        public static GuidelineRange GetExpectedWetDiaperRange(int dayOfLife)
        {
            if (dayOfLife == 1) return new GuidelineRange { Minimum = 1, Maximum = 2, Description = "Pelo menos 1 fralda molhada (AAP)" };
            if (dayOfLife == 2) return new GuidelineRange { Minimum = 2, Maximum = 3, Description = "Pelo menos 2 fraldas molhadas (AAP)" };
            if (dayOfLife == 3) return new GuidelineRange { Minimum = 3, Maximum = 5, Description = "Pelo menos 3 fraldas molhadas (AAP)" };
            if (dayOfLife == 4) return new GuidelineRange { Minimum = 4, Maximum = 6, Description = "Pelo menos 4 fraldas molhadas (AAP)" };
            if (dayOfLife <= 6) return new GuidelineRange { Minimum = 5, Maximum = 8, Description = "Pelo menos 5 fraldas molhadas (AAP)" };
            return new GuidelineRange { Minimum = 6, Maximum = 10, Description = "Pelo menos 6 fraldas molhadas/dia (AAP/OMS)" };
        }

        public static GuidelineRange GetExpectedDirtyDiaperRange(int dayOfLife, FeedingType feedingType)
        {
            if (dayOfLife <= 2) return new GuidelineRange { Minimum = 1, Maximum = 3, Description = "Mecônio esperado (OMS)" };
            if (dayOfLife <= 4) return new GuidelineRange { Minimum = 2, Maximum = 4, Description = "Fezes de transição (AAP)" };
            if (dayOfLife <= 42)
            {
                if (feedingType == FeedingType.Formula) return new GuidelineRange { Minimum = 1, Maximum = 4, Description = "Frequência normal para fórmula (AAP)" };
                return new GuidelineRange { Minimum = 3, Maximum = 8, Description = "Fezes frequentes normais em amamentação exclusiva (SBP)" };
            }
            else
            {
                if (feedingType == FeedingType.Formula) return new GuidelineRange { Minimum = 1, Maximum = 3, Description = "Frequência regular esperada (AAP)" };
                return new GuidelineRange { Minimum = 0, Maximum = 5, Description = "Pode ir dias sem evacuar após 6 semanas (SBP)" };
            }
        }

        public static DailyAnalysis AnalyzeDay(Baby baby, DateTime date, List<FeedingRecord> feedings, List<DiaperRecord> diapers)
        {
            int dayOfLife = (date.Date - baby.BirthDate.Date).Days + 1;
            var analysis = new DailyAnalysis
            {
                Date = date.Date,
                DayOfLife = dayOfLife,
                FeedingCount = feedings.Count,
                TotalFeedingMinutes = feedings.Sum(f => f.DurationMinutes),
                WetDiaperCount = diapers.Count(d => d.IsWet),
                DirtyDiaperCount = diapers.Count(d => d.IsDirty),
                ExpectedFeedings = GetExpectedFeedingRange(dayOfLife, baby.FeedingType),
                ExpectedWetDiapers = GetExpectedWetDiaperRange(dayOfLife),
                ExpectedDirtyDiapers = GetExpectedDirtyDiaperRange(dayOfLife, baby.FeedingType)
            };

            analysis.FeedingStatus = analysis.FeedingCount >= analysis.ExpectedFeedings.Minimum ? HealthStatus.Normal : (analysis.FeedingCount == analysis.ExpectedFeedings.Minimum - 1 ? HealthStatus.Attention : HealthStatus.Alert);
            analysis.WetDiaperStatus = analysis.WetDiaperCount >= analysis.ExpectedWetDiapers.Minimum ? HealthStatus.Normal : (analysis.WetDiaperCount == analysis.ExpectedWetDiapers.Minimum - 1 ? HealthStatus.Attention : HealthStatus.Alert);
            analysis.DirtyDiaperStatus = analysis.DirtyDiaperCount >= analysis.ExpectedDirtyDiapers.Minimum ? HealthStatus.Normal : (analysis.DirtyDiaperCount == analysis.ExpectedDirtyDiapers.Minimum - 1 ? HealthStatus.Attention : HealthStatus.Alert);

            if (date.Date == DateTime.Today && feedings.Any())
            {
                analysis.TimeSinceLastFeeding = DateTime.Now - feedings.Max(f => f.StartTime);
            }
            else
            {
                analysis.TimeSinceLastFeeding = null;
            }

            CheckAlerts(baby, dayOfLife, feedings, diapers, analysis);

            return analysis;
        }

        public static void CheckAlerts(Baby baby, int dayOfLife, List<FeedingRecord> feedings, List<DiaperRecord> diapers, DailyAnalysis analysis)
        {
            if (analysis.TimeSinceLastFeeding.HasValue && analysis.TimeSinceLastFeeding.Value.TotalHours > 4 && dayOfLife <= 14)
            {
                analysis.Alerts.Add(new AlertInfo { Severity = HealthStatus.Alert, Title = "Intervalo longo entre mamadas", Message = "Recém-nascidos nas primeiras 2 semanas devem mamar a cada 2-3 horas. Considere acordar o bebê. (AAP)", Source = "System" });
            }
            else if (analysis.TimeSinceLastFeeding.HasValue && analysis.TimeSinceLastFeeding.Value.TotalHours > 3 && dayOfLife <= 7)
            {
                analysis.Alerts.Add(new AlertInfo { Severity = HealthStatus.Alert, Title = "Intervalo longo", Message = "Na 1ª semana, mamar a cada 2-3h estimula a produção de leite. (OMS)", Source = "System" });
            }

            if (diapers.Any(d => d.Color == StoolColor.White))
            {
                analysis.Alerts.Add(new AlertInfo { Severity = HealthStatus.Alert, Title = "Fezes acólicas ⚠️", Message = "Fezes brancas podem indicar problema hepático (atresia biliar). Procure atendimento URGENTE. (SBP)", Source = "System" });
            }

            if (diapers.Any(d => d.Color == StoolColor.Red))
            {
                analysis.Alerts.Add(new AlertInfo { Severity = HealthStatus.Alert, Title = "Sangue nas fezes", Message = "Presença de sangue nas fezes. Consulte o pediatra. (AAP)", Source = "System" });
            }

            if (diapers.Any(d => d.Consistency == StoolConsistency.Hard))
            {
                analysis.Alerts.Add(new AlertInfo { Severity = HealthStatus.Attention, Title = "Fezes endurecidas", Message = "Fezes duras podem indicar constipação. (SBP)", Source = "System" });
            }

            if (analysis.WetDiaperCount < analysis.ExpectedWetDiapers.Minimum && dayOfLife >= 4)
            {
                analysis.Alerts.Add(new AlertInfo { Severity = HealthStatus.Alert, Title = "Poucas fraldas molhadas", Message = "Pode indicar desidratação. Consulte o pediatra. (OMS/AAP)", Source = "System" });
            }

            if (dayOfLife > 3 && diapers.Any(d => d.Color == StoolColor.Meconium))
            {
                analysis.Alerts.Add(new AlertInfo { Severity = HealthStatus.Attention, Title = "Mecônio persistente", Message = "Mecônio após o 3º dia pode indicar ingesta insuficiente. (AAP)", Source = "System" });
            }

            if (analysis.FeedingCount == 0 && analysis.Date == DateTime.Today)
            {
                analysis.Alerts.Add(new AlertInfo { Severity = HealthStatus.Attention, Title = "Nenhuma mamada hoje", Message = "Registre as mamadas para acompanhar a alimentação.", Source = "System" });
            }
        }

        public static string GetFeedingTypeDisplayName(FeedingType type) => type switch {
            FeedingType.Breast => "Amamentação",
            FeedingType.Formula => "Fórmula",
            FeedingType.Mixed => "Misto",
            _ => type.ToString()
        };

        public static string GetBreastSideDisplayName(BreastSide side) => side switch {
            BreastSide.Left => "Esquerdo",
            BreastSide.Right => "Direito",
            BreastSide.Both => "Ambos",
            BreastSide.Bottle => "Mamadeira",
            _ => side.ToString()
        };

        public static string GetStoolColorDisplayName(StoolColor c) => c switch {
            StoolColor.Meconium => "Mecônio (escuro)",
            StoolColor.DarkGreen => "Verde escuro",
            StoolColor.Yellow => "Amarelo",
            StoolColor.Brown => "Marrom",
            StoolColor.White => "Branco/Acólico ⚠️",
            StoolColor.Red => "Vermelho ⚠️",
            _ => c.ToString()
        };

        public static string GetStoolConsistencyDisplayName(StoolConsistency c) => c switch {
            StoolConsistency.Liquid => "Líquido",
            StoolConsistency.Seedy => "Grumoso",
            StoolConsistency.Soft => "Pastoso",
            StoolConsistency.Hard => "Duro",
            _ => c.ToString()
        };

        public static string GetDayOfLifeDescription(int dayOfLife)
        {
            if (dayOfLife <= 0) return "Ainda não nasceu";
            if (dayOfLife == 1) return "1º dia de vida";
            if (dayOfLife <= 7) return $"{dayOfLife}º dia (1ª semana)";
            if (dayOfLife <= 14) return $"{dayOfLife}º dia (2ª semana)";
            
            if (dayOfLife <= 42)
            {
                int week = ((dayOfLife - 1) / 7) + 1;
                return $"{dayOfLife}º dia ({week}ª semana)";
            }

            int months = dayOfLife / 30;
            return $"{dayOfLife}º dia ({months} meses)";
        }

        public static Color GetStatusColor(HealthStatus s) => s switch {
            HealthStatus.Alert => Color.FromArgb("#F44336"),
            HealthStatus.Attention => Color.FromArgb("#FF9800"),
            _ => Color.FromArgb("#4CAF50")
        };

        public static string GetStatusIcon(HealthStatus s) => s switch {
            HealthStatus.Alert => "🚨",
            HealthStatus.Attention => "⚠️",
            _ => "✅"
        };
    }
}
