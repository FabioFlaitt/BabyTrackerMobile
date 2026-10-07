using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using BabyTrackerMobile.Data;
using BabyTrackerMobile.Models;

namespace BabyTrackerMobile.Services
{
    /// <summary>
    /// Monta um resumo do dia em texto e abre a share sheet nativa
    /// (que permite enviar para WhatsApp, e-mail, SMS etc.).
    /// </summary>
    public static class ExportService
    {
        public record VitaminLine(string Name, string Time, bool Taken);

        public static async Task ShareDayAsync(
            Baby baby,
            DateTime date,
            IEnumerable<FeedingRecord> feedings,
            IEnumerable<DiaperRecord> diapers,
            IEnumerable<MotherWaterRecord> waters)
        {
            // Sono do dia
            var sleeps = await DatabaseService.GetSleepsForDateAsync(baby.Id, date);

            // Conquistas registradas nesta data
            var milestones = await DatabaseService.GetMilestonesAchievedOnAsync(baby.Id, date);

            // Vitaminas ativas e quais foram marcadas como tomadas no dia
            var schedules = await DatabaseService.GetVitaminsAsync(baby.Id);
            var takenIds = await DatabaseService.GetTakenVitaminIdsAsync(date);
            var babyVit = new List<VitaminLine>();
            var motherVit = new List<VitaminLine>();
            foreach (var v in schedules)
            {
                if (!v.IsActive) continue;
                var line = new VitaminLine(v.Name, v.TimeDisplay, takenIds.Contains(v.Id));
                if (v.IsMother) motherVit.Add(line); else babyVit.Add(line);
            }

            var text = BuildText(baby, date, feedings, diapers, waters, sleeps, babyVit, motherVit, milestones);
            await Share.Default.RequestAsync(new ShareTextRequest
            {
                Title = $"Resumo {baby.Name} - {date:dd/MM/yyyy}",
                Text = text,
                Subject = $"BabyAntonio - {date:dd/MM/yyyy}"
            });
        }

        public static string BuildText(
            Baby baby,
            DateTime date,
            IEnumerable<FeedingRecord> feedings,
            IEnumerable<DiaperRecord> diapers,
            IEnumerable<MotherWaterRecord> waters,
            IEnumerable<SleepRecord>? sleeps = null,
            IEnumerable<VitaminLine>? babyVitamins = null,
            IEnumerable<VitaminLine>? motherVitamins = null,
            IEnumerable<Milestone>? milestones = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"👶 *{baby.Name}* — {date:dd/MM/yyyy}");
            var dayOfLife = (date.Date - baby.BirthDate.Date).Days + 1;
            sb.AppendLine($"Dia {dayOfLife} de vida");
            sb.AppendLine();

            // Mamadas (com agrupamento peito x mamadeira)
            var feedList = new List<FeedingRecord>(feedings);
            var peito = 0; var mamadeira = 0; double totalMl = 0;
            foreach (var f in feedList)
            {
                if (f.Side == BreastSide.Bottle) { mamadeira++; totalMl += f.BottleAmountMl ?? 0; }
                else peito++;
            }
            sb.AppendLine($"🍼 *Mamadas: {feedList.Count}*");
            var resumoMamada = $"🤱 Peito: {peito} • 🍼 Mamadeira: {mamadeira}";
            if (totalMl > 0) resumoMamada += $" ({totalMl:0} ml)";
            sb.AppendLine(resumoMamada);
            foreach (var f in feedList)
            {
                var side = f.Side switch
                {
                    BreastSide.Left => "Esquerdo",
                    BreastSide.Right => "Direito",
                    BreastSide.Both => "Ambos",
                    BreastSide.Bottle => "Mamadeira",
                    _ => f.Side.ToString()
                };
                var detail = f.Side == BreastSide.Bottle && f.BottleAmountMl.HasValue
                    ? $"{side} • {f.BottleAmountMl:0} ml"
                    : $"{side} • {f.DurationMinutes} min";
                sb.AppendLine($"  • {f.StartTime:HH:mm} — {detail}");
                if (!string.IsNullOrWhiteSpace(f.Notes))
                    sb.AppendLine($"     📝 {f.Notes}");
            }
            sb.AppendLine();

            // Fraldas
            var diaperList = new List<DiaperRecord>(diapers);
            var wet = 0; var dirty = 0;
            foreach (var d in diaperList) { if (d.IsWet) wet++; if (d.IsDirty) dirty++; }
            sb.AppendLine($"🧷 *Fraldas: {diaperList.Count}* (💧 {wet} xixi, 💩 {dirty} cocô)");
            foreach (var d in diaperList)
            {
                var tipo = new List<string>();
                if (d.IsWet) tipo.Add("Xixi");
                if (d.IsDirty)
                {
                    var color = d.Color switch
                    {
                        StoolColor.Meconium => "Mecônio",
                        StoolColor.DarkGreen => "Verde escuro",
                        StoolColor.Yellow => "Amarelo",
                        StoolColor.Brown => "Marrom",
                        StoolColor.White => "Branco",
                        StoolColor.Red => "Vermelho",
                        _ => ""
                    };
                    tipo.Add($"Cocô ({color})");
                }
                sb.AppendLine($"  • {d.Time:HH:mm} — {string.Join(" + ", tipo)}");
                if (!string.IsNullOrWhiteSpace(d.Notes))
                    sb.AppendLine($"     📝 {d.Notes}");
            }
            sb.AppendLine();

            // Sono
            if (sleeps != null)
            {
                var sleepList = new List<SleepRecord>(sleeps);
                var totalMin = 0;
                foreach (var s in sleepList) totalMin += (int)s.Duration.TotalMinutes;
                sb.AppendLine($"😴 *Sono: {totalMin / 60}h{totalMin % 60:00} em {sleepList.Count} soneca(s)*");
                sb.AppendLine("   Recém-nascido: 14–17h/dia (NSF/AAP)");
                foreach (var s in sleepList)
                    sb.AppendLine($"  • {s.RangeText} — {s.DurationText}");
                sb.AppendLine();
            }

            // Água da mãe
            var waterList = new List<MotherWaterRecord>(waters);
            var totalWater = 0;
            foreach (var w in waterList) totalWater += w.VolumeMl;
            sb.AppendLine($"💧 *Água da Mamãe: {totalWater} ml*");
            foreach (var w in waterList)
                sb.AppendLine($"  • {w.Time:HH:mm} — {w.VolumeMl} ml");
            sb.AppendLine();

            // Vitaminas do bebê
            AppendVitamins(sb, "💊 *Vitamina do bebê*", babyVitamins);
            // Vitaminas da mãe
            AppendVitamins(sb, "💊 *Vitamina da mãe*", motherVitamins);

            // Conquistas do dia
            if (milestones != null)
            {
                var mlist = new List<Milestone>(milestones);
                if (mlist.Count > 0)
                {
                    sb.AppendLine("🏆 *Conquista(s) de hoje!*");
                    foreach (var m in mlist)
                        sb.AppendLine($"  • {m.Icon} {m.Title}");
                    sb.AppendLine();
                }
            }

            sb.AppendLine("_Enviado pelo app BabyAntonio_");
            return sb.ToString();
        }

        private static void AppendVitamins(StringBuilder sb, string header, IEnumerable<VitaminLine>? vitamins)
        {
            if (vitamins == null) return;
            var list = new List<VitaminLine>(vitamins);
            if (list.Count == 0) return;
            sb.AppendLine(header);
            foreach (var v in list)
                sb.AppendLine($"  • {v.Name} ({v.Time}) — {(v.Taken ? "✅ tomou" : "⬜ não marcada")}");
            sb.AppendLine();
        }
    }
}
