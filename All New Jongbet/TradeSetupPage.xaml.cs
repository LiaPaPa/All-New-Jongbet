// TradeSetupPage.xaml.cs 파일 전체를 아래 코드로 교체하세요.

using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace All_New_Jongbet
{
    public partial class TradeSetupPage : Page, INotifyPropertyChanged
    {
        private MainWindow _mainWindow;
        private readonly ObservableCollection<StrategyInfo> _masterStrategyList;
        public ICollectionView ActiveStrategyListView { get; }

        private TradeSettings _selectedTradeSettings;
        public TradeSettings SelectedTradeSettings
        {
            get => _selectedTradeSettings;
            set { _selectedTradeSettings = value; OnPropertyChanged(); }
        }

        public ObservableCollection<string> TradeTypes { get; } = new ObservableCollection<string> { "일반", "수량분할", "시분할" };
        public ObservableCollection<string> BuyOrderTypes { get; } = new ObservableCollection<string> { "현재가", "시장가", "현재가+1틱", "매도1호가" };
        public ObservableCollection<string> SellOrderTypes { get; } = new ObservableCollection<string> { "현재가", "시장가", "현재가-1틱", "매수1호가" };
        public ObservableCollection<string> TargetPriceTypes { get; } = new ObservableCollection<string> { "단순", "트레일링", "스탑로스" };
        public ObservableCollection<string> LiquidationMethods { get; } = new ObservableCollection<string> { "현재가", "시장가" };

        // [NEW] 매수 우선순위 옵션 컬렉션
        public ObservableCollection<string> PriorityOptions { get; }

        public TradeSetupPage(MainWindow mainWindow, ObservableCollection<StrategyInfo> masterStrategyList)
        {
            InitializeComponent();
            this.DataContext = this;
            _mainWindow = mainWindow;
            _masterStrategyList = masterStrategyList;

            // [NEW] 우선순위 옵션 목록 초기화
            PriorityOptions = new ObservableCollection<string>
            {
                "거래대금",
                "거래량",
                "거래대금/시가총액",
                "종가/고가",
                "(고-피봇)/전종",
                "(고-피봇2)/전종",
                "(고-종)/전종",
                "(고+종-피봇2)/전종",
                "(고+종-시-저)/전종",
                "(고+종-시-저)/전종+Ln(거래량)"
            };

            ActiveStrategyListView = CollectionViewSource.GetDefaultView(_masterStrategyList);
            ActiveStrategyListView.Filter = item => (item as StrategyInfo)?.Status == "Active";

            if (_masterStrategyList.Any(s => s.Status == "Active"))
            {
                StrategyListDataGrid.SelectedIndex = 0;
            }
        }

        private async void StrategyListDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (StrategyListDataGrid.SelectedItem is StrategyInfo selectedStrategy)
            {
                if (selectedStrategy.TradeSettings == null)
                {
                    selectedStrategy.TradeSettings = await LoadTradeSettingsForStrategyAsync(selectedStrategy.StrategyNumber);
                }
                SelectedTradeSettings = selectedStrategy.TradeSettings;
                Logger.Instance.Add($"{selectedStrategy.StrategyNumber}번 전략의 매매설정을 표시합니다.");
            }
        }

        public async Task<TradeSettings> LoadTradeSettingsForStrategyAsync(int strategyNumber)
        {
            try
            {
                string settingsFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TradeSettings");
                string filePath = Path.Combine(settingsFolderPath, $"settings_{strategyNumber}.json");

                if (File.Exists(filePath))
                {
                    string json = await Task.Run(() => File.ReadAllText(filePath));
                    return await Task.Run(() => JsonConvert.DeserializeObject<TradeSettings>(json));
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Add($"{strategyNumber}번 전략 설정 로드 중 오류 발생: {ex.Message}");
            }

            return new TradeSettings { StrategyNumber = strategyNumber };
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
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

        private void RunOrderTest_Click(object sender, RoutedEventArgs e)
        {
            _mainWindow?.RunOrderTestAsync();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}