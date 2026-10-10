using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace BabyTrackerMobile.Converters
{
    /// <summary>Fundo do balão de chat: lilás claro para o bot, branco para pessoas.</summary>
    public class BotBackgroundConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => (value is bool b && b) ? Color.FromArgb("#EDE7F6") : Colors.White;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
