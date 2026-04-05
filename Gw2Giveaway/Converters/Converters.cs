using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Gw2Giveaway.Converters
{
    public class BoolToBrushConverter : IValueConverter
    {
        public Brush TrueBrush { get; set; }
        public Brush FalseBrush { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (bool)value ? TrueBrush : FalseBrush;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class BoolToThicknessConverter : IValueConverter
    {
        public double TrueThickness { get; set; }
        public double FalseThickness { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (bool)value ? TrueThickness : FalseThickness;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class BoolToDoubleConverter : IValueConverter
    {
        public double TrueValue { get; set; }
        public double FalseValue { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (bool)value ? TrueValue : FalseValue;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
    public class RarityToBrushConverter : IValueConverter
    {
        private static readonly Dictionary<string, Brush> RarityBrushes = new()
    {
        { "Junk",        new SolidColorBrush(Color.FromRgb(170, 170, 170)) },     // Gray
        { "Basic",       Brushes.White },
        { "Fine",        new SolidColorBrush(Color.FromRgb(98, 164, 218)) },     // Blue
        { "Masterwork",  new SolidColorBrush(Color.FromRgb(30, 255, 0)) },       // Green
        { "Rare",        new SolidColorBrush(Color.FromRgb(255, 208, 0)) },      // Yellow
        { "Exotic",      new SolidColorBrush(Color.FromRgb(255, 164, 5)) },      // Orange
        { "Ascended",    new SolidColorBrush(Color.FromRgb(251, 62, 141)) },     // Pink
        { "Legendary",   new SolidColorBrush(Color.FromRgb(76, 19, 157)) }       // Purple
    };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string rarity && RarityBrushes.TryGetValue(rarity, out var brush))
                return brush;

            // For custom prizes or empty → Gold color
            return new SolidColorBrush(Colors.Gold);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (bool)value ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
    public class PrizeRarityToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is PrizeRarity r)
            {
                return r.ToString()[0].ToString(); // C, U, R, S
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Not needed for one-way binding (badge text is read-only)
            throw new NotImplementedException();
        }
    }

    public class PrizeRarityToBrushConverter : IValueConverter
    {
        private static readonly Dictionary<PrizeRarity, Brush> Brushes = new()
    {
        { PrizeRarity.Common,    new SolidColorBrush(Colors.White) },
        { PrizeRarity.Uncommon,  new SolidColorBrush(Colors.LimeGreen) },
        { PrizeRarity.Rare,      new SolidColorBrush(Colors.RoyalBlue) },
        { PrizeRarity.SuperRare, new SolidColorBrush(Colors.Purple) }
    };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is PrizeRarity rarity && Brushes.TryGetValue(rarity, out var brush))
                return brush;
            return Brushes[PrizeRarity.Common];
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Not needed for one-way binding
            throw new NotImplementedException();
        }
    }
    public class ActionToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (ChannelPointAction)value == ChannelPointAction.InstantGoldWin ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
    public class PercentageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double size && double.TryParse(parameter?.ToString(), out double percent))
                return size * percent;
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
    public class HexToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string hexString && !string.IsNullOrWhiteSpace(hexString))
            {
                try
                {
                    var color = (Color)ColorConverter.ConvertFromString(hexString);
                    return new SolidColorBrush(color);
                }
                catch
                {
                    // Invalid hex → fall back to transparent
                }
            }
            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SolidColorBrush brush)
            {
                return brush.Color.ToString(); // Returns #AARRGGBB
            }
            return "#FF000000";
        }
    }
    public class ColorToHexStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Color color)
            {
                return $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
            }
            return "#FF000000"; // Fallback
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string hex && !string.IsNullOrWhiteSpace(hex))
            {
                try
                {
                    string clean = hex.TrimStart('#').ToUpperInvariant();
                    if (clean.Length == 6) clean = "FF" + clean; // Add full opacity if no alpha
                    if (clean.Length == 8)
                    {
                        byte a = System.Convert.ToByte(clean.Substring(0, 2), 16);
                        byte r = System.Convert.ToByte(clean.Substring(2, 2), 16);
                        byte g = System.Convert.ToByte(clean.Substring(4, 2), 16);
                        byte b = System.Convert.ToByte(clean.Substring(6, 2), 16);
                        return Color.FromArgb(a, r, g, b);
                    }
                }
                catch { }
            }
            return Colors.Transparent;
        }
    }
    public class InvertedBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    public class TimeLeftSecondsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string timeStr && int.TryParse(timeStr.Replace("s", ""), out int seconds))
            {
                return seconds < 10;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
