using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace All_New_Jongbet
{
    public partial class TradeSetupPage : Page, INotifyPropertyChanged
    {
        private readonly ObservableCollection<StrategyInfo> _masterStrategyList;
        public ICollectionView ActiveStrategyListView { get; }

        private TradeSettings _selectedTradeSettings;
        public TradeSettings SelectedTradeSettings
        {
            get => _selectedTradeSettings;
            set { _selectedTradeSettings = value; OnPropertyChanged(); }
        }

        // ComboBox ItemsSources
        public ObservableCollection<string> TradeTypes { get; } = new ObservableCollection<string> { "일반", "수량분할", "시분할" };
        public ObservableCollection<string> BuyOrderTypes { get; } = new ObservableCollection<string> { "현재가", "시장가", "현재가+1틱", "매도1호가" };
        public ObservableCollection<string> SellOrderTypes { get; } = new ObservableCollection<string> { "현재가", "시장가", "현재가-1틱", "매수1호가" };
        public ObservableCollection<string> TargetPriceTypes { get; } = new ObservableCollection<string> { "단순", "트레일링", "스탑로스" };
        public ObservableCollection<string> PriorityOptions { get; } = new ObservableCollection<string>
        {
            "거래대금", "거래량", "거래대금/시가총액", "종가/고가",
            "(종*종)/(고*전일종가)", "(고가-피봇)/전일종가", "(고가-피봇2)/전일종가",
            "(고가-종가)/전일종가", "(고+종-피봇2)/전일종가", "(고+종-시-저)/전일종가",
            "(고종시저)/전종+Ln(거래량)"
        };

        public event PropertyChangedEventHandler PropertyChanged;

        public TradeSetupPage(ObservableCollection<StrategyInfo> strategies)
        {
            InitializeComponent();
            this.DataContext = this;
            _masterStrategyList = strategies;

            // 'Active' 상태인 전략만 보여주는 뷰 생성
            ActiveStrategyListView = CollectionViewSource.GetDefaultView(_masterStrategyList);
            ActiveStrategyListView.Filter = item => (item as StrategyInfo)?.Status == "Active";

            // 전략의 상태가 변경될 때마다 뷰를 새로고침하도록 이벤트 구독
            foreach (var strategy in _masterStrategyList)
            {
                strategy.PropertyChanged += OnStrategyStatusChanged;
            }
            _masterStrategyList.CollectionChanged += (s, e) =>
            {
                if (e.NewItems != null)
                {
                    foreach (StrategyInfo newItem in e.NewItems)
                    {
                        newItem.PropertyChanged += OnStrategyStatusChanged;
                    }
                }
                // UI 스레드에서 뷰 새로고침
                Dispatcher.Invoke(() => ActiveStrategyListView.Refresh());
            };
        }

        private void OnStrategyStatusChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(StrategyInfo.Status))
            {
                Dispatcher.Invoke(() => ActiveStrategyListView.Refresh());
            }
        }

        private void StrategyListDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (StrategyListDataGrid.SelectedItem is StrategyInfo selectedStrategy)
            {
                LoadTradeSettings(selectedStrategy.StrategyNumber);
            }
            else
            {
                SelectedTradeSettings = null;
            }
        }

        private void LoadTradeSettings(int strategyNumber)
        {
            string settingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TradeSettings", $"settings_{strategyNumber}.json");
            if (File.Exists(settingsFilePath))
            {
                try
                {
                    string json = File.ReadAllText(settingsFilePath);
                    SelectedTradeSettings = JsonConvert.DeserializeObject<TradeSettings>(json);
                    Logger.Instance.Add($"{strategyNumber}번 전략의 매매설정을 불러왔습니다.");
                }
                catch (Exception ex)
                {
                    Logger.Instance.Add($"매매설정 파일 로드 중 오류 발생: {ex.Message}");
                    SelectedTradeSettings = new TradeSettings { StrategyNumber = strategyNumber };
                }
            }
            else
            {
                SelectedTradeSettings = new TradeSettings { StrategyNumber = strategyNumber };
            }
        }

        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedTradeSettings == null || SelectedTradeSettings.StrategyNumber == 0)
            {
                MessageBox.Show("저장할 전략을 먼저 선택해주세요.", "알림");
                return;
            }

            try
            {
                string settingsFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TradeSettings");
                Directory.CreateDirectory(settingsFolderPath);
                string filePath = Path.Combine(settingsFolderPath, $"settings_{SelectedTradeSettings.StrategyNumber}.json");

                string json = JsonConvert.SerializeObject(SelectedTradeSettings, Formatting.Indented);
                File.WriteAllText(filePath, json);
                Logger.Instance.Add($"{SelectedTradeSettings.StrategyNumber}번 전략의 매매설정을 저장했습니다.");
                MessageBox.Show("설정이 저장되었습니다.", "알림");
            }
            catch (Exception ex)
            {
                Logger.Instance.Add($"매매설정 저장 중 오류 발생: {ex.Message}");
                MessageBox.Show($"설정 저장 중 오류가 발생했습니다: {ex.Message}", "오류");
            }
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
