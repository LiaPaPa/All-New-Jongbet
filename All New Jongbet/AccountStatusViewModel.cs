using LiveCharts;
using System.ComponentModel;
using System.Runtime.CompilerServices;

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
            set { _dailyProfitLoss = value; OnPropertyChanged(); }
        }

        private double _assetUsageRatio;
        public double AssetUsageRatio
        {
            get => _assetUsageRatio;
            set { _assetUsageRatio = value; OnPropertyChanged(); }
        }

        // [NEW] PieChart를 위한 SeriesCollection 속성 추가
        public SeriesCollection PieChartSeries { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}