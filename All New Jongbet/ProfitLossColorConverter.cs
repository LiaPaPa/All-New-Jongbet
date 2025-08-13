// ProfitLossColorConverter.cs
using System;
using System.Globalization;
using System.Windows;
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

    // [NEW] XAML에서 리소스 키(문자열)를 실제 Brush 객체로 변환하기 위한 컨버터
    public class ResourceKeyToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string resourceKey && !string.IsNullOrEmpty(resourceKey))
            {
                // App.xaml에 정의된 리소스를 찾아서 반환
                return Application.Current.TryFindResource(resourceKey) as Brush;
            }
            // 키가 없거나 잘못된 경우 기본 브러시 반환
            return Application.Current.TryFindResource("DefaultOrderCardBrush") as Brush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
