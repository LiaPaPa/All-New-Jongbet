// OrderTypeToBrushConverter.cs
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace All_New_Jongbet
{
    public class OrderTypeToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string orderType)
            {
                if (orderType.Contains("매수"))
                {
                    return Application.Current.FindResource("BuyOrderCardBrush") as Brush;
                }
                if (orderType.Contains("매도"))
                {
                    return Application.Current.FindResource("SellOrderCardBrush") as Brush;
                }
                if (orderType.Contains("취소"))
                {
                    return Application.Current.FindResource("CancelOrderCardBrush") as Brush;
                }
            }
            return Application.Current.FindResource("DefaultOrderCardBrush") as Brush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}