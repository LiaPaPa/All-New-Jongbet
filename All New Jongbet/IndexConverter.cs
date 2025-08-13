// IndexConverter.cs
using System;
using System.Globalization;
using System.Windows.Data;

namespace All_New_Jongbet
{
    public class IndexConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int index)
            {
                return index + 1; // 0부터 시작하는 인덱스를 1부터 시작하도록 변환
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}