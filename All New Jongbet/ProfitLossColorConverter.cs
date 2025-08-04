// ProfitLossColorConverter.cs
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace All_New_Jongbet
{
    public class ProfitLossColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double number)
            {
                if (number > 0)
                {
                    return Brushes.IndianRed; // 양수: 빨간색
                }
                if (number < 0)
                {
                    return Brushes.CornflowerBlue; // 음수: 파란색
                }
            }
            // 0 또는 변환 불가 시 기본 색상
            return new SolidColorBrush(Color.FromRgb(0x32, 0x32, 0x5d));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}