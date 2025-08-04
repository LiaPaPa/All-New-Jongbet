using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows; // <-- 오류 해결을 위해 추가되었습니다.
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using LiveCharts;
using LiveCharts.Wpf;

namespace All_New_Jongbet
{
    public partial class DashboardPage : Page, INotifyPropertyChanged
    {
        public SeriesCollection SeriesCollection { get; set; }
        public string[] Labels { get; set; }
        public Func<double, string> YFormatter { get; set; }

        private string _totalAssetText;
        public string TotalAssetText { get => _totalAssetText; set { _totalAssetText = value; OnPropertyChanged(nameof(TotalAssetText)); } }
        private string _totalProfitLossText;
        public string TotalProfitLossText { get => _totalProfitLossText; set { _totalProfitLossText = value; OnPropertyChanged(nameof(TotalProfitLossText)); } }
        private string _dailyChangeText;
        public string DailyChangeText { get => _dailyChangeText; set { _dailyChangeText = value; OnPropertyChanged(nameof(DailyChangeText)); } }
        private string _dailyChangeRateText;
        public string DailyChangeRateText { get => _dailyChangeRateText; set { _dailyChangeRateText = value; OnPropertyChanged(nameof(DailyChangeRateText)); } }
        private Brush _profitLossColor;
        public Brush ProfitLossColor { get => _profitLossColor; set { _profitLossColor = value; OnPropertyChanged(nameof(ProfitLossColor)); } }
        private Brush _dailyChangeColor;
        public Brush DailyChangeColor { get => _dailyChangeColor; set { _dailyChangeColor = value; OnPropertyChanged(nameof(DailyChangeColor)); } }
        public ObservableCollection<HoldingStockViewModel> AllHoldings { get; set; }

        private List<DailyAssetInfo> _fullPeriodAggregatedAssets;
        public event PropertyChangedEventHandler PropertyChanged;
        public ObservableCollection<OrderHistoryItem> OrderQueList { get; set; }


        public DashboardPage(ObservableCollection<AccountInfo> accounts, ObservableCollection<OrderHistoryItem> orderQue)
        {
            InitializeComponent();
            SeriesCollection = new SeriesCollection();
            YFormatter = value => value.ToString("N0");
            AllHoldings = new ObservableCollection<HoldingStockViewModel>();
            OrderQueList = orderQue;

            this.DataContext = this;
        }

        public void UpdateFullPeriodData(ObservableCollection<AccountInfo> accounts)
        {
            if (accounts == null || !accounts.Any()) return;

            // KPI 카드 데이터 계산
            double totalAssets = accounts.Sum(acc => acc.EstimatedDepositAsset);
            double totalProfitLoss = accounts.Sum(acc => acc.TotalEvaluationProfitLoss);
            _fullPeriodAggregatedAssets = accounts
                .Where(acc => acc.DailyAssetList != null)
                .SelectMany(acc => acc.DailyAssetList)
                .GroupBy(d => d.Date)
                .Select(g => new DailyAssetInfo { Date = g.Key, EstimatedAsset = g.Sum(d => d.EstimatedAsset) })
                .OrderBy(d => d.Date)
                .ToList();
            double dailyChange = 0;
            double dailyChangeRate = 0;
            if (_fullPeriodAggregatedAssets.Count >= 2)
            {
                var today = _fullPeriodAggregatedAssets.Last();
                var yesterday = _fullPeriodAggregatedAssets[_fullPeriodAggregatedAssets.Count - 2];
                dailyChange = today.EstimatedAsset - yesterday.EstimatedAsset;
                if (yesterday.EstimatedAsset != 0)
                {
                    dailyChangeRate = (dailyChange / yesterday.EstimatedAsset) * 100;
                }
            }
            Dispatcher.Invoke(() =>
            {
                TotalAssetText = totalAssets.ToString("N0");
                TotalProfitLossText = totalProfitLoss.ToString("N0");
                DailyChangeText = dailyChange.ToString("N0");
                DailyChangeRateText = $"{dailyChangeRate:F2}%";
                ProfitLossColor = totalProfitLoss >= 0 ? Brushes.IndianRed : Brushes.CornflowerBlue;
                DailyChangeColor = dailyChange >= 0 ? Brushes.IndianRed : Brushes.CornflowerBlue;
            });

            // 차트 데이터 계산
            for (int i = 0; i < _fullPeriodAggregatedAssets.Count; i++)
            {
                if (i > 0 && _fullPeriodAggregatedAssets[i - 1].EstimatedAsset != 0)
                    _fullPeriodAggregatedAssets[i].ProfitRate = (_fullPeriodAggregatedAssets[i].EstimatedAsset / _fullPeriodAggregatedAssets[i - 1].EstimatedAsset - 1) * 100;
                else
                    _fullPeriodAggregatedAssets[i].ProfitRate = 0;
            }

            // Holdings 그리드 데이터 생성
            AllHoldings.Clear();
            foreach (var account in accounts)
            {
                if (account.HoldingStockList != null)
                {
                    foreach (var stock in account.HoldingStockList)
                    {
                        AllHoldings.Add(new HoldingStockViewModel
                        {
                            AccountNumber = account.AccountNumber,
                            StockCode = stock.StockCode,
                            StockName = stock.StockName,
                            EvaluationProfitLoss = stock.EvaluationProfitLoss,
                            ProfitRate = stock.ProfitRate,
                            PurchasePrice = stock.PurchasePrice,
                            CurrentPrice = stock.CurrentPrice,
                            PreviousClosePrice = stock.PreviousClosePrice,
                            TradableQuantity = stock.TradableQuantity,
                            HoldingQuantity = stock.HoldingQuantity,
                            EvaluationAmount = stock.EvaluationAmount,
                            PurchaseAmount = stock.PurchaseAmount
                        });
                    }
                }
            }

            FilterAndDisplayChart(30);
            Btn1m.IsChecked = true;
        }

        private void FilterAndDisplayChart(int days)
        {
            if (_fullPeriodAggregatedAssets == null || !_fullPeriodAggregatedAssets.Any()) return;
            var filteredData = _fullPeriodAggregatedAssets.Skip(Math.Max(0, _fullPeriodAggregatedAssets.Count - days)).ToList();
            var assetValues = new ChartValues<double>(filteredData.Select(d => d.EstimatedAsset));
            var profitRateValues = new ChartValues<DailyAssetInfo>(filteredData);
            var profitRateMapper = new LiveCharts.Configurations.CartesianMapper<DailyAssetInfo>()
                .Y(point => point.ProfitRate)
                .Fill(point => point.ProfitRate >= 0 ? Brushes.IndianRed : Brushes.CornflowerBlue);
            SeriesCollection.Clear();
            SeriesCollection.Add(new LineSeries { Title = "총 자산", Values = assetValues, ScalesYAt = 0, PointGeometry = null, Fill = new SolidColorBrush(Colors.LightGray) { Opacity = 0.4 } });
            SeriesCollection.Add(new ColumnSeries { Title = "일별 수익률", Values = profitRateValues, Configuration = profitRateMapper, ScalesYAt = 1 });
            Labels = filteredData.Select(d => DateTime.ParseExact(d.Date, "yyyyMMdd", null).ToString("MM-dd")).ToArray();
            OnPropertyChanged(nameof(Labels));
        }

        private void PeriodButton_Click(object sender, RoutedEventArgs e)
        {
            var clickedButton = sender as ToggleButton;
            if (clickedButton == null) return;
            if (clickedButton.IsChecked == false)
            {
                clickedButton.IsChecked = true;
                return;
            }
            var buttons = new[] { Btn1m, Btn3m, Btn6m };
            foreach (var button in buttons)
            {
                if (button != clickedButton)
                {
                    button.IsChecked = false;
                }
            }
            int days = 30;
            if (clickedButton.Name == "Btn3m") days = 90;
            else if (clickedButton.Name == "Btn6m") days = 180;
            FilterAndDisplayChart(days);
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
