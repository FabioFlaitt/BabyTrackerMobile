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
        public static async Task ShareDayAsync(
            Baby baby,
            DateTime date,
            IEnumerable<FeedingRecord> feedings,
            IEnumerable<DiaperRecord> diapers,
            IEnumerable<MotherWaterRecord> waters)
        {
            // Vitaminas ativas e quais foram marcadas como tomadas no dia
            var schedules = await DatabaseService.GetVitaminsAsync(baby.Id);
            var takenIds = await DatabaseService.GetTakenVitaminIdsAsync(date);
            var vitamins = new List<(string Name, string Time, bool Taken)>();
            foreach (var v in schedules)
                if (v.IsActive) vitamins.Add((v.Name, v.TimeDisplay, takenIds.Contains(v.Id)));

            var text = BuildText(baby, date, feedings, diapers, waters, vitamins);
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
            IEnumerable<(string Name, string Time, bool Taken)>? vitamins = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"👶 *{baby.Name}* — {date:dd/MM/yyyy}");
            var dayOfLife = (date.Date - baby.BirthDate.Date).Days + 1;
            sb.AppendLine($"Dia {dayOfLife} de vida");
            sb.AppendLine();

            // Mamadas
            var feedList = new List<FeedingRecord>(feedings);
            sb.AppendLine($"🍼 *Mamadas: {feedList.Count}*");
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
            }
            sb.AppendLine();

            // Água da mãe
            var waterList = new List<MotherWaterRecord>(waters);
            var totalMl = 0;
            foreach (var w in waterList) totalMl += w.VolumeMl;
            sb.AppendLine($"💧 *Água da Mamãe: {totalMl} ml*");
            foreach (var w in waterList)
                sb.AppendLine($"  • {w.Time:HH:mm} — {w.VolumeMl} ml");
            sb.AppendLine();

            // Vitaminas
            if (vitamins != null)
            {
                var vitList = new List<(string Name, string Time, bool Taken)>(vitamins);
                if (vitList.Count > 0)
                {
                    sb.AppendLine("💊 *Vitamina*");
                    foreach (var v in vitList)
                        sb.AppendLine($"  • {v.Name} ({v.Time}) — {(v.Taken ? "✅ tomou" : "⬜ não marcada")}");
                    sb.AppendLine();
                }
            }

            sb.AppendLine("_Enviado pelo app BabyAntonio_");
            return sb.ToString();
        }
    }
}
