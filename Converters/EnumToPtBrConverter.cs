using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using BabyTrackerMobile.Models;

namespace BabyTrackerMobile.Converters
{
    /// <summary>
    /// Converte enums (BreastSide, StoolColor, StoolConsistency) para o texto em
    /// português exibido na UI. Também aceita um bool "incluir ícone" via parameter.
    /// </summary>
    public class EnumToPtBrConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value switch
            {
                BreastSide s => s switch
                {
                    BreastSide.Left => "Esquerdo",
                    BreastSide.Right => "Direito",
                    BreastSide.Both => "Ambos",
                    BreastSide.Bottle => "Mamadeira",
                    _ => s.ToString()
                },
                StoolColor c => c switch
                {
                    StoolColor.Meconium => "Mecônio",
                    StoolColor.DarkGreen => "Verde escuro",
                    StoolColor.Yellow => "Amarelo",
                    StoolColor.Brown => "Marrom",
                    StoolColor.White => "Branco ⚠️",
                    StoolColor.Red => "Vermelho ⚠️",
                    _ => c.ToString()
                },
                StoolConsistency k => k switch
                {
                    StoolConsistency.Liquid => "Líquido",
                    StoolConsistency.Seedy => "Grumoso",
                    StoolConsistency.Soft => "Pastoso",
                    StoolConsistency.Hard => "Duro",
                    _ => k.ToString()
                },
                FeedingType f => f switch
                {
                    FeedingType.Breast => "Amamentação",
                    FeedingType.Formula => "Fórmula",
                    FeedingType.Mixed => "Misto",
                    _ => f.ToString()
                },
                null => string.Empty,
                _ => value.ToString() ?? string.Empty
            };
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>Mostra o resumo da mamada em texto único.</summary>
    public class FeedingDetailConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not FeedingRecord r) return string.Empty;
            var ptBr = new EnumToPtBrConverter();
            var sideText = (string)ptBr.Convert(r.Side, typeof(string), null, culture);
            var dur = $"{r.DurationMinutes} min";
            if (r.Side == BreastSide.Bottle && r.BottleAmountMl.HasValue)
                return $"{sideText} • {r.BottleAmountMl:0} ml";
            return $"{sideText} • {dur}";
        }
        public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotImplementedException();
    }

    /// <summary>Resumo visual da fralda em texto curto.</summary>
    public class DiaperDetailConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not DiaperRecord d) return string.Empty;
            var parts = new System.Collections.Generic.List<string>();
            if (d.IsWet) parts.Add("💧 Xixi");
            if (d.IsDirty)
            {
                var ptBr = new EnumToPtBrConverter();
                var colorText = d.Color.HasValue ? (string)ptBr.Convert(d.Color.Value, typeof(string), null, culture) : "";
                parts.Add($"💩 {colorText}");
            }
            return string.Join(" + ", parts);
        }
        public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotImplementedException();
    }
}
