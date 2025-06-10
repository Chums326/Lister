using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ChumsLister.WPF.Views.Wizards
{
    public class StatusToVisibilityConverter : IValueConverter
    {
        public static StatusToVisibilityConverter Loading { get; } = new StatusToVisibilityConverter { TargetStatus = ImageStatus.Loading };
        public static StatusToVisibilityConverter Loaded { get; } = new StatusToVisibilityConverter { TargetStatus = ImageStatus.Loaded };
        public static StatusToVisibilityConverter Error { get; } = new StatusToVisibilityConverter { TargetStatus = ImageStatus.Error };

        public ImageStatus TargetStatus { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ImageStatus status)
            {
                return status == TargetStatus ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToImageTypeConverter : IValueConverter
    {
        public static BoolToImageTypeConverter Instance { get; } = new BoolToImageTypeConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isUrl)
            {
                return isUrl ? "🌐 URL" : "📁 Local";
            }
            return "Unknown";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class StringToVisibilityConverter : IValueConverter
    {
        public static StringToVisibilityConverter Instance { get; } = new StringToVisibilityConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}