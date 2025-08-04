// StyleNameToStyleConverter.cs
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace All_New_Jongbet
{
    public class StyleNameToStyleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string styleKey)
            {
                return Application.Current.FindResource(styleKey);
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}