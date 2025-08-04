using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace All_New_Jongbet
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public static int ApiRequestDelay = 300;
        public ObservableCollection<AccountInfo> AccountManageList { get; set; }
        public ObservableCollection<StrategyInfo> StrategyList { get; set; }
        public ObservableCollection<Notification> DisplayedNotifications { get; set; }
        public ObservableCollection<OrderLogItem> OrderLogList { get; set; }
        public ObservableCollection<OrderHistoryItem> OrderQueList { get; set; }

        private readonly KiwoomApiService _apiService;
        private readonly DashboardPage _dashboardPage;
        private readonly TradeSetupPage _tradeSetupPage;
        private readonly SystemSettingsPage _systemSettingsPage;
        private readonly LogsPage _logsPage;
        private readonly SettingsPage _settingsPage;
        private readonly Dictionary<string, KiwoomRealtimeClient> _realtimeClients = new Dictionary<string, KiwoomRealtimeClient>();
        private ApiRequestScheduler _apiRequestScheduler;
        private TradingManager _tradingManager;
        private CancellationTokenSource _appCts = new CancellationTokenSource();

        private readonly ConcurrentQueue<Notification> _orderNotificationQueue;
        private readonly Notification _statusNotification;

        public event PropertyChangedEventHandler PropertyChanged;

        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = this;

            AccountManageList = new ObservableCollection<AccountInfo>();
            StrategyList = new ObservableCollection<StrategyInfo>();
            DisplayedNotifications = new ObservableCollection<Notification>();
            OrderLogList = new ObservableCollection<OrderLogItem>();
            OrderQueList = new ObservableCollection<OrderHistoryItem>();
            _orderNotificationQueue = new ConcurrentQueue<Notification>();
            _apiService = new KiwoomApiService();

            _statusNotification = new Notification { Message = "Initializing...", StyleKey = "RequestingStatusLabel" };
            DisplayedNotifications.Add(_statusNotification);

            _dashboardPage = new DashboardPage(AccountManageList, OrderQueList);
            _tradeSetupPage = new TradeSetupPage(StrategyList);
            _systemSettingsPage = new SystemSettingsPage(this, AccountManageList, StrategyList);
            _logsPage = new LogsPage(OrderLogList);
            _settingsPage = new SettingsPage();

            this.Loaded += async (s, e) =>
            {
                Logger.Instance.Add("메인 윈도우 로딩 완료.");
                LoadStrategies();
                await LoadApiKeysAndRequestTokensAsync();
                await FetchAllConditionListsAsync();
                await FetchAllAccountBalancesAsync();
                await FetchAllDailyAssetHistoriesAsync();
                await FetchAllOrderHistoriesAsync();

                _dashboardPage.UpdateFullPeriodData(AccountManageList);

                var activeAccounts = AccountManageList.Where(acc => acc.TokenStatus == "Success").ToList();
                _apiRequestScheduler = new ApiRequestScheduler(activeAccounts);
                _tradingManager = new TradingManager(_apiService, _apiRequestScheduler, StrategyList.Where(st => st.Status == "Active"));

                _ = _apiRequestScheduler.RunAsync(_appCts.Token);
                // ✅ CHANGED: Start()를 StartAsync()로 수정했습니다.
                _ = _tradingManager.StartAsync(_appCts.Token);

                await ConnectAllRealtimeSocketsAsync();
                SetSidebarButtonsEnabled(true);
                MainFrame.Navigate(_dashboardPage);
                DashboardButton.IsChecked = true;
            };

            _ = ProcessNotificationQueueAsync();
        }

        private void SetSidebarButtonsEnabled(bool isEnabled)
        {
            DashboardButton.IsEnabled = isEnabled;
            TradeSetupButton.IsEnabled = isEnabled;
            StrategySetupButton.IsEnabled = isEnabled;
            LogsButton.IsEnabled = isEnabled;
            SettingsButton.IsEnabled = isEnabled;
        }

        private void NavigateButton_Click(object sender, RoutedEventArgs e)
        {
            var clickedButton = sender as ToggleButton;
            if (clickedButton == null) return;
            var allToggleButtons = new[] { DashboardButton, TradeSetupButton, StrategySetupButton, LogsButton };
            foreach (var button in allToggleButtons)
            {
                if (button != clickedButton) button.IsChecked = false;
            }
            clickedButton.IsChecked = true;
            NavigateToPage(clickedButton.Name);
        }

        private void FooterButton_Click(object sender, RoutedEventArgs e)
        {
            var clickedButton = sender as Button;
            if (clickedButton == null) return;
            var allToggleButtons = new[] { DashboardButton, TradeSetupButton, StrategySetupButton, LogsButton };
            foreach (var button in allToggleButtons)
            {
                button.IsChecked = false;
            }
            NavigateToPage(clickedButton.Name);
        }

        private void NavigateToPage(string buttonName)
        {
            if (buttonName != "TradeSetupButton" && buttonName != "StrategySetupButton")
            {
                if (SetupExpander != null) SetupExpander.IsExpanded = false;
            }
            switch (buttonName)
            {
                case "DashboardButton": MainFrame.Navigate(_dashboardPage); break;
                case "TradeSetupButton": MainFrame.Navigate(_tradeSetupPage); break;
                case "StrategySetupButton": MainFrame.Navigate(_systemSettingsPage); break;
                case "LogsButton": MainFrame.Navigate(_logsPage); break;
                case "SettingsButton": MainFrame.Navigate(_settingsPage); break;
            }
        }

        private void UpdateStatus(string message, string styleKey)
        {
            Dispatcher.Invoke(() =>
            {
                _statusNotification.Message = message;
                _statusNotification.StyleKey = styleKey;
            });
        }

        public void AddOrderNotification(string type, string stockName, int quantity)
        {
            var styleKey = type.ToUpper() == "BUY" ? "BuyLabel" : "SellLabel";
            var message = $"{type.ToUpper()}: {stockName} {quantity} shares";
            _orderNotificationQueue.Enqueue(new Notification { Message = message, StyleKey = styleKey });
        }

        private async Task ProcessNotificationQueueAsync()
        {
            while (true)
            {
                if (DisplayedNotifications.Count - 1 < 3 && _orderNotificationQueue.TryDequeue(out Notification notificationToShow))
                {
                    _ = ShowAndRemoveNotificationAsync(notificationToShow);
                }
                await Task.Delay(100);
            }
        }

        private async Task ShowAndRemoveNotificationAsync(Notification notification)
        {
            await Dispatcher.InvokeAsync(() => DisplayedNotifications.Add(notification));
            await Task.Delay(5000);
            await Dispatcher.InvokeAsync(() => DisplayedNotifications.Remove(notification));
        }

        private void LoadStrategies()
        {
            string strategyFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Strategy", "strategies.json");
            if (File.Exists(strategyFilePath))
            {
                Logger.Instance.Add("저장된 전략 파일을 불러옵니다.");
                string json = File.ReadAllText(strategyFilePath);
                var loadedStrategies = JsonConvert.DeserializeObject<List<StrategyInfo>>(json);
                if (loadedStrategies != null)
                {
                    foreach (var strategy in loadedStrategies)
                    {
                        StrategyList.Add(strategy);
                    }
                }
            }
        }

        public async Task LoadApiKeysAndRequestTokensAsync()
        {
            UpdateStatus("Requesting Tokens...", "RequestingStatusLabel");
            Logger.Instance.Add("API 키 로드 및 토큰 발급을 시작합니다.");
            AccountManageList.Clear();
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string keysDirectory = Path.Combine(baseDirectory, "API Keys");
            Directory.CreateDirectory(keysDirectory);
            var keyFiles = Directory.GetFiles(keysDirectory, "*.txt");
            var accounts = new Dictionary<string, AccountInfo>();
            foreach (var file in keyFiles)
            {
                string fileName = Path.GetFileNameWithoutExtension(file);
                string[] parts = fileName.Split('_');
                if (parts.Length != 2) continue;
                string accountNumber = parts[0];
                string keyType = parts[1].ToLower();
                string keyContent = File.ReadAllText(file).Trim();
                if (!accounts.ContainsKey(accountNumber))
                {
                    accounts[accountNumber] = new AccountInfo { AccountNumber = accountNumber };
                }
                if (keyType == "appkey") accounts[accountNumber].AppKey = keyContent;
                else if (keyType == "secretkey") accounts[accountNumber].SecretKey = keyContent;
            }
            var validAccounts = accounts.Values.Where(acc => !string.IsNullOrEmpty(acc.AppKey) && !string.IsNullOrEmpty(acc.SecretKey)).ToList();
            foreach (var account in validAccounts)
            {
                account.TokenStatus = "Requesting...";
                AccountManageList.Add(account);
                await Task.Delay(ApiRequestDelay);
                var (token, isSuccess) = await _apiService.GetAccessTokenAsync(account.AppKey, account.SecretKey);
                if (isSuccess)
                {
                    account.Token = token;
                    account.TokenStatus = "Success";
                    Logger.Instance.Add($"{account.AccountNumber} 계좌의 토큰 발급 성공.");
                }
                else
                {
                    account.TokenStatus = "Fail";
                    Logger.Instance.Add($"{account.AccountNumber} 계좌의 토큰 발급에 실패했습니다.");
                }
            }
        }

        public async Task FetchAllConditionListsAsync()
        {
            Logger.Instance.Add("모든 계좌의 조건식 목록 조회를 시작합니다.");
            foreach (var account in AccountManageList.Where(acc => acc.TokenStatus == "Success"))
            {
                UpdateStatus($"Fetching Conditions for {account.AccountNumber}...", "RequestingStatusLabel");
                account.Conditions = await _apiService.GetConditionListAsync(account);
            }
        }

        public async Task FetchAllAccountBalancesAsync()
        {
            Logger.Instance.Add("모든 계좌의 잔고 조회를 시작합니다.");
            foreach (var account in AccountManageList.Where(acc => acc.TokenStatus == "Success"))
            {
                UpdateStatus($"Fetching Balance for {account.AccountNumber}...", "RequestingStatusLabel");
                await _apiService.GetAccountBalanceAsync(account);
            }
        }

        public async Task FetchAllDailyAssetHistoriesAsync()
        {
            Logger.Instance.Add("모든 계좌의 일별 자산 현황 조회를 시작합니다.");
            string today = DateTime.Today.ToString("yyyyMMdd");
            string startDate = DateTime.Today.AddMonths(-6).ToString("yyyyMMdd");
            foreach (var account in AccountManageList.Where(acc => acc.TokenStatus == "Success"))
            {
                UpdateStatus($"Fetching Daily Assets for {account.AccountNumber}...", "RequestingStatusLabel");
                var history = await _apiService.GetDailyAssetHistoryAsync(account, startDate, today);
                for (int i = 0; i < history.Count; i++)
                {
                    if (i > 0 && history[i - 1].EstimatedAsset != 0)
                        history[i].ProfitRate = (history[i].EstimatedAsset / history[i - 1].EstimatedAsset - 1) * 100;
                    else
                        history[i].ProfitRate = 0;
                }
                account.DailyAssetList.Clear();
                foreach (var item in history)
                {
                    account.DailyAssetList.Add(item);
                }
                var todayDataInHistory = account.DailyAssetList.FirstOrDefault(h => h.Date == today);
                if (todayDataInHistory != null)
                {
                    todayDataInHistory.EstimatedAsset = account.EstimatedDepositAsset;
                }
                else
                {
                    account.DailyAssetList.Add(new DailyAssetInfo { Date = today, EstimatedAsset = account.EstimatedDepositAsset });
                }
            }
        }

        public async Task FetchAllOrderHistoriesAsync()
        {
            Logger.Instance.Add("모든 계좌의 주문/체결 내역 조회를 시작합니다.");
            OrderQueList.Clear();
            foreach (var account in AccountManageList.Where(acc => acc.TokenStatus == "Success"))
            {
                UpdateStatus($"Fetching Order History for {account.AccountNumber}...", "RequestingStatusLabel");
                var history = await _apiService.GetOrderHistoryAsync(account);
                var unfilledOrders = history.Where(o => o.UnfilledQuantity > 0);
                foreach (var order in unfilledOrders)
                {
                    OrderQueList.Add(order);
                }
            }
        }

        public async Task ConnectAllRealtimeSocketsAsync()
        {
            Logger.Instance.Add("모든 계좌의 실시간 소켓 연결을 시작합니다.");
            int accountIndex = 0;
            foreach (var account in AccountManageList.Where(acc => acc.TokenStatus == "Success"))
            {
                UpdateStatus($"Connecting Realtime Socket for {account.AccountNumber}...", "RequestingStatusLabel");
                var wsClient = new KiwoomRealtimeClient(account.Token);
                _realtimeClients[account.AccountNumber] = wsClient;
                wsClient.OnReceiveData += (data) => HandleRealtimeData(account, data);
                bool isConnected = await wsClient.ConnectAndLoginAsync();
                if (isConnected)
                {
                    string groupNumber = $"02{accountIndex:D2}";
                    await wsClient.RegisterRealtimeAsync(groupNumber, new[] { "" }, new[] { "02" });
                }
                accountIndex++;
            }
            UpdateStatus("Auto Trading Ready", "StatusLabel");
        }

        private void HandleRealtimeData(AccountInfo account, JObject data)
        {
            string trnm = data["trnm"]?.ToString();
            if (trnm != "ACCT_EVL") return;
            string stockCode = data["values"]?["9001"]?.ToString();
            if (string.IsNullOrEmpty(stockCode)) return;
            Dispatcher.Invoke(() =>
            {
                var stockToUpdate = account.HoldingStockList.FirstOrDefault(s => s.StockCode == stockCode);
                if (stockToUpdate != null)
                {
                    if (double.TryParse(data["values"]?["10"]?.ToString(), out var currentPrice)) stockToUpdate.CurrentPrice = currentPrice;
                    if (double.TryParse(data["values"]?["11"]?.ToString(), out var change)) stockToUpdate.ChangeFromPreviousDay = change;
                    if (double.TryParse(data["values"]?["12"]?.ToString(), out var rate)) stockToUpdate.FluctuationRate = rate;
                    if (double.TryParse(data["values"]?["27"]?.ToString(), out var askPrice)) stockToUpdate.BestAskPrice = askPrice;
                    if (double.TryParse(data["values"]?["28"]?.ToString(), out var bidPrice)) stockToUpdate.BestBidPrice = bidPrice;
                    if (long.TryParse(data["values"]?["13"]?.ToString(), out var volume)) stockToUpdate.CumulativeVolume = volume;
                    if (long.TryParse(data["values"]?["14"]?.ToString(), out var amount)) stockToUpdate.CumulativeAmount = amount;
                    if (double.TryParse(data["values"]?["16"]?.ToString(), out var openPrice)) stockToUpdate.OpenPrice = openPrice;
                    if (double.TryParse(data["values"]?["17"]?.ToString(), out var highPrice)) stockToUpdate.HighPrice = highPrice;
                    if (double.TryParse(data["values"]?["18"]?.ToString(), out var lowPrice)) stockToUpdate.LowPrice = lowPrice;
                }
            });
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
