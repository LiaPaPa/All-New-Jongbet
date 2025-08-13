// MainWindow.xaml.cs file

using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using All_New_Jongbet.Properties;
using System.Text;
using System.Drawing;
namespace All_New_Jongbet
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        // CHANGED: API 호출 간격을 210ms로 조정하여 속도 향상 (초당 5회 제한 준수)
        public static int ApiRequestDelay = 210;
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
        private readonly ConcurrentDictionary<string, TaskCompletionSource<JObject>> _wsResponseTasks = new ConcurrentDictionary<string, TaskCompletionSource<JObject>>();

        private ApiRequestScheduler _apiRequestScheduler;
        private TradingManager _tradingManager;
        private CancellationTokenSource _appCts = new CancellationTokenSource();

        private readonly ConcurrentQueue<Notification> _orderNotificationQueue;
        private readonly Notification _statusNotification;

        public event PropertyChangedEventHandler PropertyChanged;
        public ObservableCollection<OrderHistoryItem> AllOrderHistoryList { get; set; }

        private bool _isSetupMenuActive;
        public bool IsSetupMenuActive
        {
            get => _isSetupMenuActive;
            set
            {
                _isSetupMenuActive = value;
                OnPropertyChanged(nameof(IsSetupMenuActive));
            }
        }

        private Timer _notificationTimer;
        private bool _isNotificationSentToday = false;
        private readonly TelegramApiService _telegramService; // Telegram 서비스 객체 추가

        private readonly ChartGenerator _chartGenerator;

        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = this;

            _telegramService = new TelegramApiService(); // 생성자에서 초기화
            _telegramService.OnMessageReceived += HandleTelegramMessage;
            _chartGenerator = new ChartGenerator();

            _apiService = new KiwoomApiService();
            _orderNotificationQueue = new ConcurrentQueue<Notification>();

            AccountManageList = new ObservableCollection<AccountInfo>();
            DisplayedNotifications = new ObservableCollection<Notification>();
            OrderLogList = new ObservableCollection<OrderLogItem>();
            OrderQueList = new ObservableCollection<OrderHistoryItem>();
            AllOrderHistoryList = new ObservableCollection<OrderHistoryItem>();

            _statusNotification = new Notification { Message = "Initializing...", StyleKey = "RequestingStatusLabel" };
            DisplayedNotifications.Add(_statusNotification);

            LoadStrategies();

            _dashboardPage = new DashboardPage(AccountManageList, OrderQueList);
            _tradeSetupPage = new TradeSetupPage(this, StrategyList);
            _systemSettingsPage = new SystemSettingsPage(this, AccountManageList, StrategyList);
            _logsPage = new LogsPage(OrderLogList);
            _settingsPage = new SettingsPage();


            this.Loaded += async (s, e) =>
            {
                Logger.Instance.Add("메인 윈도우 로딩 완료.");
                await LoadAndLinkTradeSettingsAsync();

                await LoadApiKeysAndRequestTokensAsync();
                await ConnectAllWebsocketsAsync();

                var primaryAppKey = _realtimeClients.Keys.FirstOrDefault();
                var activeAccounts = AccountManageList.Where(acc => acc.TokenStatus == "Success").ToList();
                _apiRequestScheduler = new ApiRequestScheduler(activeAccounts);

                if (primaryAppKey != null)
                {
                    var primaryWs = GetWebSocketByAppKey(primaryAppKey);
                    if (primaryWs != null)
                    {
                        _tradingManager = new TradingManager(_apiService, StrategyList, AccountManageList, _apiRequestScheduler, primaryWs, _wsResponseTasks);
                    }
                }

                await FetchAllConditionListsAsync();
                await FetchAllAccountBalancesAsync();
                await FetchAllOrderHistoriesAsync();
                await FetchAllDailyAssetHistoriesAsync();

                await SubscribeToRealtimeDataAsync();

                _dashboardPage.UpdateFullPeriodData(AccountManageList);

                _ = _apiRequestScheduler.RunAsync(_appCts.Token);

                if (_tradingManager != null)
                {
                    _ = _tradingManager.StartAsync(_appCts.Token);
                }

                UpdateStatus("Auto Trading Ready", "StatusLabel");

                SetSidebarButtonsEnabled(true);
                MainContentControl.Content = _dashboardPage;
                DashboardButton.IsChecked = true;

                SetupNotificationTimer();

                await InitializeTelegramBot();
            };

            _ = ProcessNotificationQueueAsync();
        }

        private async Task ConnectAllWebsocketsAsync()
        {
            Logger.Instance.Add("AppKey별 웹소켓 연결을 시작합니다.");
            int accountIndex = 0;

            foreach (var account in AccountManageList.Where(acc => acc.TokenStatus == "Success"))
            {
                if (_realtimeClients.ContainsKey(account.AppKey))
                {
                    Logger.Instance.Add($"{account.AccountNumber} 계좌의 AppKey에 대한 웹소켓은 이미 연결되어 있습니다.");
                    continue;
                }

        private async Task ConnectAllWebsocketsAsync()
        {
            Logger.Instance.Add("AppKey별 웹소켓 연결을 시작합니다.");
            int accountIndex = 0;

            foreach (var account in AccountManageList.Where(acc => acc.TokenStatus == "Success"))
            {
                if (_realtimeClients.ContainsKey(account.AppKey))
                {
                    Logger.Instance.Add($"{account.AccountNumber} 계좌의 AppKey에 대한 웹소켓은 이미 연결되어 있습니다.");
                    continue;
                }

                UpdateStatus($"Connecting WebSocket for AppKey {account.AppKey.Substring(0, 8)}...", "RequestingStatusLabel");

                var wsClient = new KiwoomRealtimeClient(account.Token);

                wsClient.OnReceiveData += (data) =>
                {
                    string trnm = data["trnm"]?.ToString();
                    if (trnm == "REAL")
                    {
                        HandleRealtimeData(account, data);
                    }
                    else if (trnm != null && _wsResponseTasks.TryRemove(trnm, out var tcs))
                    {
                        tcs.TrySetResult(data);
                    }
                };

                bool isConnected = await wsClient.ConnectAndLoginAsync();
                if (isConnected)
                {
                    _realtimeClients[account.AppKey] = wsClient;
                    Logger.Instance.Add($"AppKey {account.AppKey.Substring(0, 8)}... 에 대한 웹소켓 연결 성공.");
                }
                accountIndex++;
            }
        }

        private async Task SubscribeToRealtimeDataAsync()
        {
            Logger.Instance.Add("모든 계좌에 대한 실시간 데이터 구독을 시작합니다.");

            var accountsWithHoldings = AccountManageList
                .Where(acc => acc.TokenStatus == "Success")
                .ToList();

            for (int i = 0; i < accountsWithHoldings.Count; i++)
            {
                var account = accountsWithHoldings[i];
                if (_realtimeClients.TryGetValue(account.AppKey, out var wsClient))
                {
                    await wsClient.RegisterRealtimeAsync($"{i:D2}01", new[] { "" }, new[] { "00" }); // 주문체결
                    await Task.Delay(250);
                    await wsClient.RegisterRealtimeAsync($"{i:D2}02", new[] { "" }, new[] { "04" }); // 잔고
                    await Task.Delay(250);

                    if (account.HoldingStockList != null && account.HoldingStockList.Any())
                    {
                        var stockCodes = account.HoldingStockList.Select(s => s.StockCode.TrimStart('A')).ToArray();
                        await wsClient.RegisterRealtimeAsync($"{i:D2}03", stockCodes, new[] { "0B" }); // 주식체결
                        await Task.Delay(250);
                        await wsClient.RegisterRealtimeAsync($"{i:D2}04", stockCodes, new[] { "0C" }); // 주식우선호가
                        await Task.Delay(250);
                    }
                }
                else
                {
                    Logger.Instance.Add($"[오류] {account.AccountNumber} 계좌의 AppKey에 해당하는 웹소켓 클라이언트를 찾을 수 없습니다.");
                }
            }
        }

        private async Task UpdateStockSubscriptionAsync(AccountInfo account)
        {
            var accountIndex = AccountManageList.IndexOf(account);
            if (accountIndex == -1) return;

            if (_realtimeClients.TryGetValue(account.AppKey, out var wsClient))
            {
                var stockCodes = account.HoldingStockList?.Select(s => s.StockCode.TrimStart('A')).ToArray() ?? new string[0];

                if (stockCodes.Any())
                {
                    Logger.Instance.Add($"[{account.AccountNumber}] 보유 종목 변경으로 실시간 시세를 재구독합니다. (대상: {stockCodes.Length}개)");
                    await wsClient.RegisterRealtimeAsync($"{accountIndex:D2}03", stockCodes, new[] { "0B" }, "0");
                    await Task.Delay(250);
                    await wsClient.RegisterRealtimeAsync($"{accountIndex:D2}04", stockCodes, new[] { "0C" }, "0");
                }
                else
                {
                    Logger.Instance.Add($"[{account.AccountNumber}] 보유 종목이 없어 실시간 시세 구독을 해지합니다.");
                    await wsClient.UnregisterRealtimeAsync($"{accountIndex:D2}03");
                    await Task.Delay(250);
                    await wsClient.UnregisterRealtimeAsync($"{accountIndex:D2}04");
                }
            }
        }


        private ClientWebSocket GetWebSocketByAppKey(string appKey)
        {
            if (_realtimeClients.TryGetValue(appKey, out var client))
            {
                return client.WebSocket;
            }
            return null;
        }

        private void LoadStrategies()
        {
            StrategyList = StrategyRepository.Load();
        }

        private async Task LoadAndLinkTradeSettingsAsync()
        {
            Logger.Instance.Add("저장된 거래설정을 불러와 전략에 연결합니다.");
            foreach (var strategy in StrategyList)
            {
                strategy.TradeSettings = await _tradeSetupPage.LoadTradeSettingsForStrategyAsync(strategy.StrategyNumber);
            }
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

            IsSetupMenuActive = TradeSetupButton.IsChecked == true || StrategySetupButton.IsChecked == true;

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

            IsSetupMenuActive = false;

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
                case "DashboardButton": MainContentControl.Content = _dashboardPage; break;
                case "TradeSetupButton": MainContentControl.Content = _tradeSetupPage; break;
                case "StrategySetupButton": MainContentControl.Content = _systemSettingsPage; break;
                case "LogsButton": MainContentControl.Content = _logsPage; break;
                case "SettingsButton": MainContentControl.Content = _settingsPage; break;
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
            Logger.Instance.Add("계좌별 조건검색식 목록 조회를 시작합니다.");
            foreach (var account in AccountManageList.Where(acc => acc.TokenStatus == "Success"))
            {
                if (_realtimeClients.TryGetValue(account.AppKey, out var wsClient))
                {
                    try
                    {
                        var requestPacket = new { trnm = "CNSRLST" };
                        var response = await SendWsRequestAsync(wsClient.WebSocket, "CNSRLST", requestPacket);

                        if (response?["return_code"]?.ToString() == "0")
                        {
                            var conditions = new List<ConditionInfo>();
                            if (response["data"] is JArray dataArray)
                            {
                                foreach (var item in dataArray.OfType<JArray>())
                                {
                                    if (item.Count >= 2)
                                    {
                                        conditions.Add(new ConditionInfo
                                        {
                                            Index = item[0]?.ToString(),
                                            Name = item[1]?.ToString()
                                        });
                                    }
                                }
                            }
                            account.Conditions = conditions;
                            Logger.Instance.Add($"[{account.AccountNumber}] 조건검색식 목록 조회 성공: {conditions.Count}개");
                        }
                        else
                        {
                            Logger.Instance.Add($"[{account.AccountNumber}] 조건검색식 목록 조회 실패: {response?["return_msg"]}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.Add($"[{account.AccountNumber}] 조건검색식 목록 조회 중 예외 발생: {ex.Message}");
                    }
                    await Task.Delay(300);
                }
                else
                {
                    Logger.Instance.Add($"[오류] {account.AccountNumber} 계좌에 해당하는 웹소켓 클라이언트를 찾을 수 없습니다.");
                }
            }
        }

        private async Task<JObject> SendWsRequestAsync(ClientWebSocket ws, string trnm, object requestPacket)
        {
            var tcs = new TaskCompletionSource<JObject>();
            _wsResponseTasks.TryAdd(trnm, tcs);

            try
            {
                await _apiService.SendWsMessageAsync(ws, requestPacket);
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
                {
                    var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(-1, cts.Token));
                    if (completedTask == tcs.Task)
                    {
                        return await tcs.Task;
                    }
                    else
                    {
                        throw new TimeoutException($"{trnm} 요청 응답 시간 초과.");
                    }
                }
            }
            finally
            {
                _wsResponseTasks.TryRemove(trnm, out _);
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
            var strategyAccountNumbers = StrategyList
                .Select(s => s.AccountNumber)
                .Distinct()
                .ToList();

            if (!strategyAccountNumbers.Any())
            {
                Logger.Instance.Add("조회할 전략이 등록된 계좌가 없어 주문/체결 내역 조회를 건너뜁니다.");
                return;
            }

            Logger.Instance.Add($"전략에 등록된 계좌({string.Join(", ", strategyAccountNumbers)})의 주문/체결/미체결 내역 조회를 시작합니다.");

            AllOrderHistoryList.Clear();
            OrderQueList.Clear();

            var accountsToQuery = AccountManageList
                .Where(acc => acc.TokenStatus == "Success" && strategyAccountNumbers.Contains(acc.AccountNumber));

            foreach (var account in accountsToQuery)
            {
                UpdateStatus($"Fetching Executed Orders for {account.AccountNumber}...", "RequestingStatusLabel");
                var executedHistory = await _apiService.GetOrderHistoryAsync(account);
                foreach (var order in executedHistory)
                {
                    order.AccountNumber = account.AccountNumber;
                    AllOrderHistoryList.Add(order);
                }

                await Task.Delay(ApiRequestDelay);

                UpdateStatus($"Fetching Unfilled Orders for {account.AccountNumber}...", "RequestingStatusLabel");
                var unfilledHistory = await _apiService.GetUnfilledOrdersAsync(account);
                foreach (var order in unfilledHistory)
                {
                    if (!AllOrderHistoryList.Any(o => o.OrderNumber == order.OrderNumber))
                    {
                        order.AccountNumber = account.AccountNumber;
                        AllOrderHistoryList.Add(order);
                    }
                }

                await Task.Delay(ApiRequestDelay);
            }

            foreach (var order in AllOrderHistoryList.OrderBy(o => o.OrderTime))
            {
                order.OrderStatusDisplay = ConvertApiStatusToDisplayStatus(order);
                OrderQueList.Insert(0, order);
            }

            Logger.Instance.Add($"총 {OrderQueList.Count}건의 주문 내역을 로드했습니다.");
        }

        private string ConvertApiStatusToDisplayStatus(OrderHistoryItem order)
        {
            if (!string.IsNullOrEmpty(order.OrderStatusFromApi))
            {
                if (order.OrderStatusFromApi.Contains("취소")) return "취소완료";
                if (order.OrderStatusFromApi.Contains("거부")) return "주문거부";
                if (order.OrderStatusFromApi.Contains("확인"))
                {
                    return order.UnfilledQuantity > 0 ? "체결중" : "체결완료";
                }
                if (order.OrderStatusFromApi.Contains("접수")) return "체결대기";
            }

            if (order.ExecutedQuantity == 0 && order.UnfilledQuantity > 0)
            {
                return "체결대기";
            }
            else if (order.UnfilledQuantity > 0)
            {
                return "체결중";
            }
            else if (order.UnfilledQuantity == 0 && order.ExecutedQuantity > 0 && order.ExecutedQuantity == order.OrderQuantity)
            {
                return "체결완료";
            }

            if (order.UnfilledQuantity > 0 && order.ExecutedQuantity == 0)
            {
                return "체결대기";
            }

            return "확인필요";
        }

        private void HandleRealtimeData(AccountInfo account, JObject data)
        {
            JArray dataArray = data["data"] as JArray;
            if (dataArray == null) return;

            foreach (JObject item in dataArray)
            {
                string dataType = item["type"]?.ToString();
                JObject values = item["values"] as JObject;
                if (values == null) continue;

                string stockCode = string.Empty;
                if (dataType == "0B" || dataType == "0C")
                {
                    HandleStockExecution(values);
                    continue;
                }
                else
                {
                    stockCode = values["9001"]?.ToString()?.TrimStart('A');
                }

                if (string.IsNullOrEmpty(stockCode)) continue;

                switch (dataType)
                {
                    case "00": HandleOrderExecution(account, values); break;
                    case "04": HandleBalanceUpdate(account, values); break;
                    case "0B": HandleStockExecution(stockCode, values); break;
                    case "0C": HandlePriorityQuote(stockCode, values); break;
                }
            }
        }

        private void HandlePriorityQuote(string stockCode, JObject values)
        {
            if (string.IsNullOrEmpty(stockCode)) return;

            double.TryParse(values["27"]?.ToString(), out double rawAskPrice);
            double.TryParse(values["28"]?.ToString(), out double rawBidPrice);
            double bestAskPrice = Math.Abs(rawAskPrice);
            double bestBidPrice = Math.Abs(rawBidPrice);

            foreach (var account in AccountManageList)
            {
                var stockToUpdate = account.HoldingStockList?.FirstOrDefault(s => s.StockCode.TrimStart('A') == stockCode);
                if (stockToUpdate != null)
                {
                    Dispatcher.Invoke(() =>
                    {
                        stockToUpdate.BestAskPrice = bestAskPrice;
                        stockToUpdate.BestBidPrice = bestBidPrice;
                    });
                }
            }
        }


        private void HandleStockExecution(string stockCode, JObject values)
        {
            try
            {
                if (string.IsNullOrEmpty(stockCode)) return;

                double.TryParse(values["10"]?.ToString(), out double rawCurrentPrice);
                double currentPrice = Math.Abs(rawCurrentPrice);

                double.TryParse(values["12"]?.ToString(), out double fluctuationRate);
                long.TryParse(values["13"]?.ToString(), out long cumulativeVolume);

                double.TryParse(values["17"]?.ToString(), out double rawHighPrice);
                double highPrice = Math.Abs(rawHighPrice);

                double.TryParse(values["18"]?.ToString(), out double rawLowPrice);
                double lowPrice = Math.Abs(rawLowPrice);

                foreach (var account in AccountManageList)
                {
                    var stockToUpdate = account.HoldingStockList?.FirstOrDefault(s => s.StockCode.TrimStart('A') == stockCode);

                    if (stockToUpdate != null)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            stockToUpdate.CurrentPrice = currentPrice;
                            stockToUpdate.FluctuationRate = fluctuationRate;
                            stockToUpdate.CumulativeVolume = cumulativeVolume;
                            stockToUpdate.HighPrice = highPrice;
                            stockToUpdate.LowPrice = lowPrice;

                            if (stockToUpdate.HoldingQuantity > 0)
                            {
                                stockToUpdate.EvaluationAmount = currentPrice * stockToUpdate.HoldingQuantity;
                                stockToUpdate.EvaluationProfitLoss = stockToUpdate.EvaluationAmount - stockToUpdate.PurchaseAmount;
                                if (stockToUpdate.PurchaseAmount > 0)
                                {
                                    stockToUpdate.ProfitRate = (stockToUpdate.EvaluationProfitLoss / stockToUpdate.PurchaseAmount) * 100;
                                }
                            }

                            account.RecalculateAndUpdateTotals();
                            _dashboardPage.UpdateRealtimeUIData(AccountManageList);

                            _ = _tradingManager.CheckSellConditionsAsync(account, stockToUpdate);
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Add($"[오류] 실시간 주식체결 데이터 처리 중 예외 발생: {ex.Message}");
            }
        }

        private void HandleOrderExecution(AccountInfo account, JObject values)
        {
            try
            {
                string orderNumber = values["9203"]?.ToString();
                if (string.IsNullOrEmpty(orderNumber)) return;

                string orderStatusFromApi = values["913"]?.ToString();
                string stockCode = values["9001"]?.ToString()?.TrimStart('A');
                string stockName = values["302"]?.ToString();
                int.TryParse(values["900"]?.ToString(), out int orderQuantity);
                int.TryParse(values["902"]?.ToString(), out int unfilledQuantity);
                double.TryParse(values["901"]?.ToString(), out double orderPrice);
                string orderTypeCode = values["905"]?.ToString();
                string timeHHMMSS = values["908"]?.ToString();
                string formattedTime = timeHHMMSS;
                if (!string.IsNullOrEmpty(timeHHMMSS) && timeHHMMSS.Length == 6)
                {
                    formattedTime = $"{timeHHMMSS.Substring(0, 2)}:{timeHHMMSS.Substring(2, 2)}:{timeHHMMSS.Substring(4, 2)}";
                }

                Logger.Instance.Add($"[실시간 주문처리] 계좌:{account.AccountNumber}, 주문번호:{orderNumber}, 상태:{orderStatusFromApi}, 미체결:{unfilledQuantity}");

                Dispatcher.Invoke(async () => // 비동기 처리를 위해 async 추가
                {
                    var existingOrder = OrderQueList.FirstOrDefault(o => o.OrderNumber == orderNumber);

                    if (existingOrder != null)
                    {
                        existingOrder.UnfilledQuantity = unfilledQuantity;
                        existingOrder.ExecutedQuantity = orderQuantity - unfilledQuantity;
                        existingOrder.OrderStatusFromApi = orderStatusFromApi;
                        existingOrder.OrderTypeCode = orderTypeCode ?? existingOrder.OrderTypeCode;
                        existingOrder.OrderTime = formattedTime ?? existingOrder.OrderTime;
                        existingOrder.OrderStatusDisplay = ConvertApiStatusToDisplayStatus(existingOrder);
                    }
                    else
                    {
                        var newOrderItem = new OrderHistoryItem
                        {
                            AccountNumber = account.AccountNumber,
                            OrderNumber = orderNumber,
                            StockCode = stockCode,
                            StockName = stockName,
                            OrderQuantity = orderQuantity,
                            OrderPrice = orderPrice,
                            ExecutedQuantity = orderQuantity - unfilledQuantity,
                            UnfilledQuantity = unfilledQuantity,
                            OrderStatusFromApi = orderStatusFromApi,
                            OrderTypeCode = orderTypeCode,
                            OrderTime = formattedTime
                        };
                        newOrderItem.OrderStatusDisplay = ConvertApiStatusToDisplayStatus(newOrderItem);
                        OrderQueList.Insert(0, newOrderItem);
                    }

                    if (executedQuantity > 0)
                    {
                        bool isStrategyAccount = StrategyList.Any(s => s.AccountNumber == account.AccountNumber);
                        if (isStrategyAccount)
                        {
                            TransactionRepository.AddOrUpdateTransaction(
                                account.AccountNumber,
                                stockCode,
                                stockName,
                                orderTypeCode,
                                executedQuantity,
                                executedPrice,
                                formattedTime
                            );
                        }

                        var holdingStock = account.HoldingStockList.FirstOrDefault(s => s.StockCode.TrimStart('A') == stockCode);

                        if (orderTypeCode.Contains("매수"))
                        {
                            double buyAmount = executedPrice * executedQuantity;
                            string message = $"[매수 체결] 📈\n\n" +
                                             $"- 계좌: {account.AccountNumber}\n" +
                                             $"- 종목: {stockName}\n" +
                                             $"- 체결가: {executedPrice:N0}원\n" +
                                             $"- 체결금액: {buyAmount:N0}원";
                            await _telegramService.SendTradeNotificationAsync(message);

                            if (holdingStock == null)
                            {
                                holdingStock = new HoldingStock
                                {
                                    StockCode = stockCode,
                                    StockName = stockName,
                                    HoldingQuantity = executedQuantity,
                                    TradableQuantity = executedQuantity,
                                    PurchasePrice = executedPrice,
                                    PurchaseAmount = executedPrice * executedQuantity
                                };
                                account.HoldingStockList.Add(holdingStock);
                                await UpdateStockSubscriptionAsync(account); // 신규 편입 종목 실시간 구독
                            }
                            else
                            {
                                double totalPurchaseAmount = holdingStock.PurchaseAmount + (executedPrice * executedQuantity);
                                int totalQuantity = holdingStock.HoldingQuantity + executedQuantity;
                                holdingStock.PurchasePrice = totalPurchaseAmount / totalQuantity;
                                holdingStock.HoldingQuantity = totalQuantity;
                                holdingStock.TradableQuantity += executedQuantity;
                                holdingStock.PurchaseAmount = totalPurchaseAmount;
                            }
                        }
                        else if (orderTypeCode.Contains("매도"))
                        {
                            if (holdingStock != null)
                            {
                                double profitRate = (holdingStock.PurchasePrice > 0) ? (executedPrice / holdingStock.PurchasePrice - 1) * 100 : 0;
                                string message = $"[매도 체결] 📉\n\n" +
                                                 $"- 계좌: {account.AccountNumber}\n" +
                                                 $"- 종목: {stockName}\n" +
                                                 $"- 수익률: {profitRate:F2}%";
                                await _telegramService.SendTradeNotificationAsync(message);

                                holdingStock.HoldingQuantity -= executedQuantity;
                                if (holdingStock.HoldingQuantity <= 0)
                                {
                                    account.HoldingStockList.Remove(holdingStock);
                                    await UpdateStockSubscriptionAsync(account); // 전량 매도 종목 실시간 구독 해지
                                }
                            }
                        }
                        account.RecalculateAndUpdateTotals();
                        _dashboardPage.UpdateFullPeriodData(AccountManageList);
                    }

                    if (unfilledQuantity == 0)
                    {
                        var orderToRemove = OrderQueList.FirstOrDefault(o => o.OrderNumber == orderNumber);
                        if (orderToRemove != null)
                        {
                            // OrderQueList.Remove(orderToRemove); 
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.Instance.Add($"[오류] 실시간 주문체결 데이터 처리 중 예외 발생: {ex.Message}");
            }
        }

        private void HandleBalanceUpdate(AccountInfo account, JObject values)
        {
            try
            {
                string stockCode = values["9001"]?.ToString();
                if (string.IsNullOrEmpty(stockCode)) return;

                Logger.Instance.Add($"[실시간 잔고변경] 계좌:{account.AccountNumber}, 종목:{stockCode}");

                Dispatcher.Invoke(() =>
                {
                    _apiRequestScheduler.EnqueueRequest(async (acc) =>
                    {
                        await _apiService.GetAccountBalanceAsync(account);
                        _dashboardPage.UpdateFullPeriodData(AccountManageList);
                    });
                });
            }
            catch (Exception ex)
            {
                Logger.Instance.Add($"[오류] 실시간 잔고 데이터 처리 중 예외 발생: {ex.Message}");
            }
        }

        public async Task RunOrderTestAsync()
        {
            Logger.Instance.Add("===== 매수 주문 테스트 시작 =====");
            UpdateStatus("Running Order Test...", "RequestingStatusLabel");

            foreach (var account in AccountManageList.Where(acc => acc.TokenStatus == "Success" && acc.HoldingStockList.Any()))
            {
                foreach (var stock in account.HoldingStockList)
                {
                    Logger.Instance.Add($" -> 계좌({account.AccountNumber})의 보유종목({stock.StockName})에 대해 10회 매수 주문 시작");
                    for (int i = 1; i <= 10; i++)
                    {
                        await _apiService.SendBuyOrderAsync(account, stock.StockCode, 1, 1000);
                        await Task.Delay(ApiRequestDelay);
                    }
                }
            }

            Logger.Instance.Add("===== 모든 테스트 주문 요청이 완료되었습니다. =====");
            UpdateStatus("Order Test Finished", "StatusLabel");
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
            }
            else
            {
                this.WindowState = WindowState.Maximized;
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        public void SetupNotificationTimer()
        {
            _notificationTimer?.Dispose();
            _notificationTimer = new Timer(NotificationTimer_Callback, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
            Logger.Instance.Add("텔레그램 알림 스케줄러를 시작합니다.");
        }

        private void NotificationTimer_Callback(object state)
        {
            var now = DateTime.Now;

            if (now.Hour == 0 && now.Minute == 0)
            {
                _isNotificationSentToday = false;
            }

            if (!Settings.Default.IsTelegramNotificationEnabled || _isNotificationSentToday)
            {
                return;
            }

            if (now.ToString("HH:mm") == Settings.Default.TelegramNotificationTime)
            {
                _isNotificationSentToday = true;
                SendTelegramSummary();
            }
        }

        private async void SendTelegramSummary()
        {
            string botToken = Settings.Default.TelegramBotToken;
            string chatId = Settings.Default.TelegramChatId;

            if (string.IsNullOrEmpty(botToken) || string.IsNullOrEmpty(chatId))
            {
                Logger.Instance.Add("[텔레그램 알림] 봇 토큰 또는 채팅 ID가 없어 메시지를 보낼 수 없습니다.");
                return;
            }

            Logger.Instance.Add("[텔레그램 알림] 계좌 현황 요약 메시지를 준비합니다.");

            double totalAssets = AccountManageList.Sum(acc => acc.EstimatedDepositAsset);
            double totalProfitLoss = AccountManageList.Sum(acc => acc.TotalEvaluationProfitLoss);

            string message = $"🔔 Jongbet 데일리 리포트 ({DateTime.Now:yyyy-MM-dd HH:mm})\n\n" +
                             $"- 총 자산: {totalAssets:N0}원\n" +
                             $"- 총 평가손익: {totalProfitLoss:N0}원\n\n" +
                             $"오늘도 좋은 하루 되세요!";

            await _telegramService.SendMessageAsync(botToken, chatId, message);
        }

        private async Task InitializeTelegramBot()
        {
            string botToken = Settings.Default.TelegramBotToken;
            string chatId = Settings.Default.TelegramChatId;

            if (string.IsNullOrEmpty(botToken) || string.IsNullOrEmpty(chatId))
            {
                Logger.Instance.Add("[텔레그램] 봇 토큰 또는 채팅 ID가 설정되지 않아 봇을 시작할 수 없습니다.");
                return;
            }

            await _telegramService.SetCommandsAsync(botToken);
            await _telegramService.ClearPendingMessagesAsync(botToken);
            _ = _telegramService.StartReceivingMessagesAsync(botToken, _appCts.Token);
            string welcomeMessage = $"✅ Jongbet 프로그램이 연결되었습니다.";
            await _telegramService.SendMessageAsync(botToken, chatId, welcomeMessage, true);
        }

        private async void HandleTelegramMessage(string chatId, string command)
        {
            if (chatId != Settings.Default.TelegramChatId) return;

            string botToken = Settings.Default.TelegramBotToken;
            string responseMessage = "알 수 없는 명령입니다.";

            switch (command)
            {
                case "/daily_report":
                    double totalAssets = AccountManageList.Sum(acc => acc.EstimatedDepositAsset);
                    double dailyChange = AccountManageList.Sum(acc =>
                    {
                        if (acc.DailyAssetList != null && acc.DailyAssetList.Count >= 2)
                        {
                            var today = acc.DailyAssetList.Last();
                            var yesterday = acc.DailyAssetList.ElementAt(acc.DailyAssetList.Count - 2);
                            return today.EstimatedAsset - yesterday.EstimatedAsset;
                        }
                        return 0;
                    });
                    responseMessage = $"📊 데일리 리포트 ({DateTime.Now:MM-dd HH:mm})\n\n" +
                                      $"- 총 자산: {totalAssets:N0}원\n" +
                                      $"- 당일 손익: {dailyChange:N0}원";
                    break;

                case "/account_status":
                    var sb = new StringBuilder();
                    sb.AppendLine($"📋 전체 계좌 현황 ({DateTime.Now:MM-dd HH:mm})\n");
                    foreach (var acc in AccountManageList.Where(a => a.TokenStatus == "Success"))
                    {
                        sb.AppendLine($"--- [{acc.AccountNumber}] ---");
                        sb.AppendLine($"- 총자산: {acc.EstimatedDepositAsset:N0}원");
                        sb.AppendLine($"- 평가손익: {acc.TotalEvaluationProfitLoss:N0}원");

                        double accDailyChange = 0;
                        if (acc.DailyAssetList != null && acc.DailyAssetList.Count >= 2)
                        {
                            var today = acc.DailyAssetList.Last();
                            var yesterday = acc.DailyAssetList.ElementAt(acc.DailyAssetList.Count - 2);
                            accDailyChange = today.EstimatedAsset - yesterday.EstimatedAsset;
                        }
                        sb.AppendLine($"- 당일손익: {accDailyChange:N0}원");

                        if (acc.HoldingStockList != null && acc.HoldingStockList.Any())
                        {
                            sb.AppendLine("- 보유종목:");
                            foreach (var stock in acc.HoldingStockList)
                            {
                                sb.AppendLine($"  • {stock.StockName} ({stock.ProfitRate:F2}%)");
                            }
                        }
                        else
                        {
                            sb.AppendLine("- 보유종목: 없음");
                        }
                        sb.AppendLine();
                    }
                    responseMessage = sb.ToString();
                    break;

                case "/asset_trend":
                    Logger.Instance.Add("[텔레그램] Asset Trend 차트 생성 요청 수신 (1개월, 3개월, 6개월).");

                    var aggregatedAssets = AccountManageList
                        .Where(acc => acc.DailyAssetList != null)
                        .SelectMany(acc => acc.DailyAssetList)
                        .GroupBy(d => d.Date)
                        .Select(g => new DailyAssetInfo { Date = g.Key, EstimatedAsset = g.Sum(d => d.EstimatedAsset) })
                        .OrderBy(d => d.Date)
                        .ToList();

                    var chartGenerator = new ChartGenerator();
                    var tempFiles = new List<string>();

                    var last1MonthData = aggregatedAssets.Where(d => d.DateObject >= DateTime.Now.AddMonths(-1)).OrderBy(d => d.DateObject).ToList();
                    string path1Month = chartGenerator.CreateAssetTrendChartImage(last1MonthData, "최근 1개월 자산 추이");
                    if (!string.IsNullOrEmpty(path1Month)) tempFiles.Add(path1Month);

                    var last3MonthsData = aggregatedAssets.Where(d => d.DateObject >= DateTime.Now.AddMonths(-3)).OrderBy(d => d.DateObject).ToList();
                    string path3Months = chartGenerator.CreateAssetTrendChartImage(last3MonthsData, "최근 3개월 자산 추이");
                    if (!string.IsNullOrEmpty(path3Months)) tempFiles.Add(path3Months);

                    var last6MonthsData = aggregatedAssets.Where(d => d.DateObject >= DateTime.Now.AddMonths(-6)).OrderBy(d => d.DateObject).ToList();
                    string path6Months = chartGenerator.CreateAssetTrendChartImage(last6MonthsData, "최근 6개월 자산 추이");
                    if (!string.IsNullOrEmpty(path6Months)) tempFiles.Add(path6Months);

                    if (tempFiles.Any())
                    {
                        string mergedImagePath = MergeImagesVertically(tempFiles);
                        if (!string.IsNullOrEmpty(mergedImagePath))
                        {
                            await _telegramService.SendPhotoAsync(botToken, chatId, mergedImagePath, "최근 1개월, 3개월, 6개월 자산 추이입니다.");
                            File.Delete(mergedImagePath);
                        }
                        foreach (var file in tempFiles)
                        {
                            File.Delete(file);
                        }
                    }
                    else
                    {
                        await _telegramService.SendMessageAsync(botToken, chatId, "차트를 생성할 데이터가 부족합니다.");
                    }
                    return;

                case "/start_trading":
                    _tradingManager?.StartTrading();
                    responseMessage = "✅ 자동매매를 시작합니다.";
                    break;

                case "/stop_trading":
                    _tradingManager?.StopTrading();
                    responseMessage = "🛑 오늘 하루 자동매매를 중지합니다.";
                    break;
            }

            if (responseMessage != "알 수 없는 명령입니다.")
            {
                await _telegramService.SendMessageAsync(botToken, chatId, responseMessage);
            }

        }
        private string MergeImagesVertically(List<string> imagePaths)
        {
            if (!imagePaths.Any()) return null;

            try
            {
                List<System.Drawing.Bitmap> images = imagePaths.Select(System.Drawing.Image.FromFile).Cast<System.Drawing.Bitmap>().ToList();

                int totalHeight = images.Sum(img => img.Height);
                int maxWidth = images.Max(img => img.Width);

                System.Drawing.Bitmap resultImage = new System.Drawing.Bitmap(maxWidth, totalHeight);

                using (Graphics g = Graphics.FromImage(resultImage))
                {
                    g.Clear(System.Drawing.Color.White);

                    int currentY = 0;
                    foreach (System.Drawing.Bitmap img in images)
                    {
                        g.DrawImage(img, new System.Drawing.Point(0, currentY));
                        currentY += img.Height;
                        img.Dispose();
                    }
                }

                string mergedPath = Path.Combine(Path.GetTempPath(), $"merged_asset_trend_{DateTime.Now.Ticks}.png");
                resultImage.Save(mergedPath, System.Drawing.Imaging.ImageFormat.Png);
                resultImage.Dispose();

                return mergedPath;
            }
            catch (Exception ex)
            {
                Logger.Instance.Add($"이미지 병합 중 오류 발생: {ex.Message}");
                return null;
            }
        }
    }
}