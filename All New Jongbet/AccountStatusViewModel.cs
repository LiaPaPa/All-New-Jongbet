using LiveCharts;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

namespace All_New_Jongbet
{
    public class AccountStatusViewModel : INotifyPropertyChanged
    {
        private string _accountNumber;
        public string AccountNumber
        {
            get => _accountNumber;
            set { _accountNumber = value; OnPropertyChanged(); }
        }

        private double _dailyProfitRate;
        public double DailyProfitRate
        {
            get => _dailyProfitRate;
            set { _dailyProfitRate = value; OnPropertyChanged(); }
        }

        private double _dailyProfitLoss;
        public double DailyProfitLoss
        {
            get => _dailyProfitLoss;
            set
            {
                _dailyProfitLoss = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DailyProfitLossColor));
            }
        }

        private double _assetUsageRatio;
        public double AssetUsageRatio
        {
            get => _assetUsageRatio;
            set { _assetUsageRatio = value; OnPropertyChanged(); }
        }

        public SeriesCollection PieChartSeries { get; set; }

        // [CHANGED] 수정된 색상 키 사용
        public Brush DailyProfitLossColor
        {
            get
            {
                if (DailyProfitLoss > 0)
                {
                    return Application.Current.FindResource("PositiveRedBrush") as Brush;
                }
                if (DailyProfitLoss < 0)
                {
                    return Application.Current.FindResource("NegativeBlueBrush") as Brush;
                }
                return Application.Current.FindResource("PrimaryText") as Brush;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
