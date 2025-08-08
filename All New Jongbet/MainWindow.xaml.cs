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

        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = this;

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
                        await FetchAllConditionListsAsync();
                    }
                }

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
                MainFrame.Navigate(_dashboardPage);
                DashboardButton.IsChecked = true;
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
            int accountIndex = 0;

            foreach (var account in AccountManageList.Where(acc => acc.TokenStatus == "Success"))
            {
                if (_realtimeClients.TryGetValue(account.AppKey, out var wsClient))
                {
                    await wsClient.RegisterRealtimeAsync($"{accountIndex:D2}01", new[] { "" }, new[] { "00" });
                    await wsClient.RegisterRealtimeAsync($"{accountIndex:D2}02", new[] { "" }, new[] { "04" });

                    if (account.HoldingStockList != null && account.HoldingStockList.Any())
                    {
                        var stockCodes = account.HoldingStockList.Select(s => s.StockCode).ToArray();
                        await wsClient.RegisterRealtimeAsync($"{accountIndex:D2}03", stockCodes, new[] { "0B" });
                    }
                }
                else
                {
                    Logger.Instance.Add($"[오류] {account.AccountNumber} 계좌의 AppKey에 해당하는 웹소켓 클라이언트를 찾을 수 없습니다.");
                }
                accountIndex++;
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
            var allConditions = await _tradingManager.GetConditionListAsync();

            if (allConditions != null)
            {
                foreach (var account in AccountManageList.Where(acc => acc.TokenStatus == "Success"))
                {
                    account.Conditions = allConditions;
                }
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

                if (dataType == "0B")
                {
                    HandleStockExecution(values);
                    continue;
                }

                string realtimeAccountNumber = values["9201"]?.ToString();
                var targetAccount = AccountManageList.FirstOrDefault(acc => acc.AccountNumber.Contains(realtimeAccountNumber));
                if (targetAccount == null) targetAccount = account;

                switch (dataType)
                {
                    case "00": HandleOrderExecution(targetAccount, values); break;
                    case "04": HandleBalanceUpdate(targetAccount, values); break;
                }
            }
        }

        private void HandleStockExecution(JObject values)
        {
            try
            {
                string stockCode = values["9001"]?.ToString()?.TrimStart('A');
                if (string.IsNullOrEmpty(stockCode)) return;

                double.TryParse(values["10"]?.ToString(), out double currentPrice);
                double.TryParse(values["12"]?.ToString(), out double fluctuationRate);
                long.TryParse(values["13"]?.ToString(), out long cumulativeVolume);
                double.TryParse(values["17"]?.ToString(), out double highPrice);
                double.TryParse(values["18"]?.ToString(), out double lowPrice);

                foreach (var account in AccountManageList)
                {
                    var stockToUpdate = account.HoldingStockList?.FirstOrDefault(s => s.StockCode == stockCode);
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
                string orderTypeCode = values["906"]?.ToString();
                string timeHHMMSS = values["908"]?.ToString();
                string formattedTime = timeHHMMSS;
                if (!string.IsNullOrEmpty(timeHHMMSS) && timeHHMMSS.Length == 6)
                {
                    formattedTime = $"{timeHHMMSS.Substring(0, 2)}:{timeHHMMSS.Substring(2, 2)}:{timeHHMMSS.Substring(4, 2)}";
                }

                Logger.Instance.Add($"[실시간 주문처리] 계좌:{account.AccountNumber}, 주문번호:{orderNumber}, 상태:{orderStatusFromApi}, 미체결:{unfilledQuantity}");

                Dispatcher.Invoke(() =>
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
    }
}
