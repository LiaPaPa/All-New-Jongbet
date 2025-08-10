using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Controls;
using LiveCharts;
using LiveCharts.Wpf;
using System.Windows.Media;
using LiveCharts.Configurations;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;

namespace All_New_Jongbet
{
    public partial class DashboardPage : UserControl, INotifyPropertyChanged
    {
        // Asset Trend 차트용 속성
        private List<DailyAssetInfo> _fullPeriodAggregatedAssets;
        public SeriesCollection SeriesCollection { get; set; }
        public string[] Labels { get; set; }
        public Func<double, string> YFormatter { get; set; }

        // 상단 카드 및 원형 그래프용 속성
        public ObservableCollection<AccountStatusViewModel> AccountStatuses { get; set; }
        private string _totalAssetText;
        public string TotalAssetText { get => _totalAssetText; set { _totalAssetText = value; OnPropertyChanged(); } }
        private string _totalProfitLossText;
        public string TotalProfitLossText { get => _totalProfitLossText; set { _totalProfitLossText = value; OnPropertyChanged(); } }
        private string _dailyChangeText;
        public string DailyChangeText { get => _dailyChangeText; set { _dailyChangeText = value; OnPropertyChanged(); } }
        private string _dailyChangeRateText;
        public string DailyChangeRateText { get => _dailyChangeRateText; set { _dailyChangeRateText = value; OnPropertyChanged(); } }
        private Brush _profitLossColor;
        public Brush ProfitLossColor { get => _profitLossColor; set { _profitLossColor = value; OnPropertyChanged(); } }
        private Brush _dailyChangeColor;
        public Brush DailyChangeColor { get => _dailyChangeColor; set { _dailyChangeColor = value; OnPropertyChanged(); } }

        // Today's Orders용 속성
        public ObservableCollection<OrderHistoryItem> OrderQueList { get; set; }

        public DashboardPage(ObservableCollection<AccountInfo> accounts, ObservableCollection<OrderHistoryItem> orderQue)
        {
            InitializeComponent();
            SeriesCollection = new SeriesCollection();
            AccountStatuses = new ObservableCollection<AccountStatusViewModel>();
            YFormatter = value => value.ToString("N0");
            OrderQueList = orderQue;

            this.DataContext = this;
        }

        public void UpdateFullPeriodData(ObservableCollection<AccountInfo> accounts)
        {
            if (accounts == null || !accounts.Any()) return;

            // 1. 상단 카드 데이터 계산 (기존 로직 유지)
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

            TotalAssetText = totalAssets.ToString("N0");
            TotalProfitLossText = totalProfitLoss.ToString("N0");
            DailyChangeText = dailyChange.ToString("N0");
            DailyChangeRateText = $"{dailyChangeRate:F2}%";
            ProfitLossColor = totalProfitLoss >= 0 ? (Brush)FindResource("PositiveGreen") : (Brush)FindResource("NegativeRed");
            DailyChangeColor = dailyChange >= 0 ? (Brush)FindResource("PositiveGreen") : (Brush)FindResource("NegativeRed");

            for (int i = 0; i < _fullPeriodAggregatedAssets.Count; i++)
            {
                if (i > 0 && _fullPeriodAggregatedAssets[i - 1].EstimatedAsset != 0)
                    _fullPeriodAggregatedAssets[i].ProfitRate = (_fullPeriodAggregatedAssets[i].EstimatedAsset / _fullPeriodAggregatedAssets[i - 1].EstimatedAsset - 1) * 100;
                else
                    _fullPeriodAggregatedAssets[i].ProfitRate = 0;
            }

            // 2. Asset Trend 차트 데이터 업데이트
            FilterAndDisplayChart(30);
            if (Btn1m != null) Btn1m.IsChecked = true;

            // 3. Holdings를 대체할 원형 그래프 데이터 가공
            AccountStatuses.Clear();
            foreach (var account in accounts.Where(a => a.TokenStatus == "Success"))
            {
                var status = new AccountStatusViewModel { AccountNumber = account.AccountNumber };

                if (account.EstimatedDepositAsset > 0)
                {
                    status.AssetUsageRatio = (account.TotalPurchaseAmount / account.EstimatedDepositAsset) * 100;
                }

                if (account.DailyAssetList != null && account.DailyAssetList.Count >= 2)
                {
                    var todayAssetInfo = account.DailyAssetList.OrderByDescending(d => d.Date).FirstOrDefault();
                    var yesterdayAssetInfo = account.DailyAssetList.OrderByDescending(d => d.Date).Skip(1).FirstOrDefault();

                    if (todayAssetInfo != null && yesterdayAssetInfo != null && yesterdayAssetInfo.EstimatedAsset > 0)
                    {
                        status.DailyProfitRate = (todayAssetInfo.EstimatedAsset / yesterdayAssetInfo.EstimatedAsset - 1);
                        status.DailyProfitLoss = todayAssetInfo.EstimatedAsset - yesterdayAssetInfo.EstimatedAsset;
                    }
                }

                // [CHANGED] PieChart 데이터 생성
                status.PieChartSeries = new SeriesCollection
        {
            new PieSeries
            {
                Title = "사용 비중",
                Values = new ChartValues<double> { status.AssetUsageRatio },
                Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D81B60")),
                DataLabels = false,
                StrokeThickness = 0
            },
            new PieSeries
            {
                Title = "남은 비중",
                Values = new ChartValues<double> { 100 - status.AssetUsageRatio },
                Fill = new SolidColorBrush(Color.FromArgb(51, 74, 92, 197)), // #4a5cc5 with opacity
                DataLabels = false,
                StrokeThickness = 0
            }
        };

                AccountStatuses.Add(status);
            }
            OnPropertyChanged(nameof(AccountStatuses));
        }

        private void FilterAndDisplayChart(int days)
        {
            if (_fullPeriodAggregatedAssets == null || !_fullPeriodAggregatedAssets.Any()) return;

            var filteredData = _fullPeriodAggregatedAssets.Skip(Math.Max(0, _fullPeriodAggregatedAssets.Count - days)).ToList();
            var profitRateMapper = new CartesianMapper<DailyAssetInfo>()
                .Y(point => point.ProfitRate)
                .Fill(point => point.ProfitRate >= 0 ? (Brush)FindResource("PositiveGreen") : (Brush)FindResource("NegativeRed"));

            SeriesCollection.Clear();
            SeriesCollection.Add(new LineSeries
            {
                Title = "총 자산",
                Values = new ChartValues<DailyAssetInfo>(filteredData),
                Configuration = new CartesianMapper<DailyAssetInfo>().Y(point => point.EstimatedAsset),
                PointGeometry = null,
                StrokeThickness = 2,
                Stroke = (Brush)FindResource("PrimaryText"),
                Fill = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1), GradientStops = new GradientStopCollection { new GradientStop((Color)ColorConverter.ConvertFromString("#805e72e4"), 0.1), new GradientStop((Color)ColorConverter.ConvertFromString("#005e72e4"), 1) } },
                ScalesYAt = 0,
            });
            SeriesCollection.Add(new ColumnSeries
            {
                Title = "일별 수익률",
                Values = new ChartValues<DailyAssetInfo>(filteredData),
                Configuration = profitRateMapper,
                MaxColumnWidth = 5,
                ScalesYAt = 1,
                DataLabels = false,
            });

            Labels = filteredData.Select(d => DateTime.ParseExact(d.Date, "yyyyMMdd", null).ToString("MM-dd")).ToArray();
            OnPropertyChanged(nameof(Labels));
        }

        private void PeriodButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var clickedButton = sender as System.Windows.Controls.Primitives.ToggleButton;
            if (clickedButton == null || clickedButton.IsChecked == false) return;

            var buttons = new[] { Btn1m, Btn3m, Btn6m };
            foreach (var button in buttons)
            {
                if (button != clickedButton) button.IsChecked = false;
            }
            clickedButton.IsChecked = true;

            int days = 30;
            if (clickedButton.Name == "Btn3m") days = 90;
            else if (clickedButton.Name == "Btn6m") days = 180;
            FilterAndDisplayChart(days);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void AccountChart_Click(object sender, RoutedEventArgs e)
        {
            // 클릭된 버튼에서 AccountStatusViewModel 데이터를 가져옴
            if ((sender as FrameworkElement)?.DataContext is AccountStatusViewModel selectedStatus)
            {
                // MainWindow에 있는 전체 계좌 목록에서 일치하는 계좌 정보를 찾음
                var mainWindow = (MainWindow)Application.Current.MainWindow;
                var selectedAccount = mainWindow.AccountManageList
                                        .FirstOrDefault(acc => acc.AccountNumber == selectedStatus.AccountNumber);

                if (selectedAccount != null)
                {
                    // 찾은 계좌 정보를 전달하여 새 창을 열고 보여줌
                    var holdingsWindow = new HoldingsWindow(selectedAccount);
                    holdingsWindow.Show();
                }
            }
        }
    }
}