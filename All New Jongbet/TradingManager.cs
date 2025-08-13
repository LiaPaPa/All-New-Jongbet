// TradingManager.cs 파일 전체를 아래 코드로 교체하세요.

using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace All_New_Jongbet
{
    public class TradingManager : IDisposable
    {
        private readonly MainWindow _mainWindow; // MainWindow 참조
        private readonly KiwoomApiService _apiService;
        private readonly ObservableCollection<StrategyInfo> _strategies;
        private readonly ObservableCollection<AccountInfo> _accountList;
        private readonly ApiRequestScheduler _apiRequestScheduler;
        private readonly Func<ClientWebSocket, string, object, Task<JObject>> _sendWsRequestAsync;

        private Dictionary<int, bool> _liquidationExecuted;
        private ConcurrentDictionary<string, DateTime> _sellOrderTimestamps = new ConcurrentDictionary<string, DateTime>(); // 매도 주문 시간 기록

        public bool IsTradingEnabled { get; private set; } = true;

        // [MODIFIED] 생성자에 MainWindow 추가
        public TradingManager(MainWindow mainWindow, KiwoomApiService apiService, ObservableCollection<StrategyInfo> strategies, ObservableCollection<AccountInfo> accountList, ApiRequestScheduler scheduler, Func<ClientWebSocket, string, object, Task<JObject>> sendWsRequestFunc)
        {
            _mainWindow = mainWindow; // 참조 저장
            _apiService = apiService;
            _strategies = strategies;
            _accountList = accountList;
            _apiRequestScheduler = scheduler;
            _sendWsRequestAsync = sendWsRequestFunc;
            _liquidationExecuted = new Dictionary<int, bool>();
        }

        public void StartTrading()
        {
            IsTradingEnabled = true;
            Logger.Instance.Add("자동매매가 [시작]되었습니다.");
        }

        public void StopTrading()
        {
            IsTradingEnabled = false;
            Logger.Instance.Add("자동매매가 [중지]되었습니다. (오늘 하루 실행되지 않습니다)");
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            Logger.Instance.Add("자동매매 관리자를 시작합니다.");
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await ExecuteTradingCycleAsync();
                    await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                }
                catch (TaskCanceledException) { break; }
                catch (Exception ex)
                {
                    Logger.Instance.Add($"[오류] 자동매매 루프에서 예외 발생: {ex.Message}");
                    await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
                }
            }
            Logger.Instance.Add("자동매매 관리자가 종료되었습니다.");
        }

        private async Task ExecuteTradingCycleAsync()
        {
            if (!IsTradingEnabled) return;
            DateTime now = DateTime.Now;
            if (now.Hour == 0 && now.Minute < 2)
            {
                IsTradingEnabled = true;
                _liquidationExecuted.Clear();
                _sellOrderTimestamps.Clear();
            }

            if (!App.IsDebugMode && now.Hour >= 18)
            {
                if (now.Hour == 18 && now.Minute < 1) _liquidationExecuted.Clear();
                return;
            }

            foreach (var strategy in _strategies.Where(s => s.Status == "Active"))
            {
                var account = _accountList.FirstOrDefault(a => a.AccountNumber == strategy.AccountNumber);
                if (account == null || strategy.TradeSettings == null) continue;

                var buyStartTime = DateTime.Today.AddHours(strategy.TradeSettings.Buy.BuyStartHour).AddMinutes(strategy.TradeSettings.Buy.BuyStartMinute);
                var liquidationTime = buyStartTime.AddMinutes(-1);

                if (now >= liquidationTime && !_liquidationExecuted.ContainsKey(strategy.StrategyNumber))
                {
                    _liquidationExecuted[strategy.StrategyNumber] = true;
                    await LiquidatePositionsIfNeededAsync(strategy, account);
                }
                if (now >= buyStartTime && strategy.LastExecutionDate < DateTime.Today)
                {
                    await ExecuteBuyLogicAsync(strategy, account);
                }
            }
        }

        private async Task LiquidatePositionsIfNeededAsync(StrategyInfo strategy, AccountInfo account)
        {
            Logger.Instance.Add($"[{strategy.ConditionName}] 매수 시작 1분 전, 잔여 포지션 청산을 시작합니다.");
            var unfilledOrders = await _apiService.GetUnfilledOrdersAsync(account);
            await Task.Delay(300);
            var buyOrdersToCancel = unfilledOrders.Where(o => o.OrderTypeCode.Contains("매수")).ToList();
            if (buyOrdersToCancel.Any())
            {
                Logger.Instance.Add($" -> {buyOrdersToCancel.Count}건의 미체결 매수 주문을 취소합니다.");
                foreach (var order in buyOrdersToCancel)
                {
                    await _apiService.SendCancelOrderAsync(account, order.StockCode, order.OrderNumber, order.UnfilledQuantity);
                    await Task.Delay(300);
                }
            }
            await _apiService.GetAccountBalanceAsync(account);
            await Task.Delay(300);
            var holdings = account.HoldingStockList.ToList();
            var sellOrdersToCancel = unfilledOrders.Where(o => o.OrderTypeCode.Contains("매도")).ToList();
            foreach (var order in sellOrdersToCancel)
            {
                Logger.Instance.Add($" -> 미체결 매도 주문({order.StockName})을 취소하고 시장가로 재주문합니다.");
                await _apiService.SendCancelOrderAsync(account, order.StockCode, order.OrderNumber, order.UnfilledQuantity);
                await Task.Delay(300);
            }
            if (!holdings.Any())
            {
                Logger.Instance.Add(" -> 청산할 잔여 종목이 없습니다.");
                return;
            }
            Logger.Instance.Add($" -> {holdings.Count}개의 보유 종목을 시장가로 매도합니다.");
            foreach (var stock in holdings)
            {
                if (stock.TradableQuantity > 0)
                {
                    await _apiService.SendSellOrderAsync(account, stock.StockCode, stock.TradableQuantity, 0, "3");
                    await Task.Delay(300);
                }
            }
            Logger.Instance.Add(" -> 모든 잔여 포지션에 대한 청산 주문이 완료되었습니다.");
        }

        private async Task ExecuteBuyLogicAsync(StrategyInfo strategy, AccountInfo account)
        {
            Logger.Instance.Add($"[{strategy.ConditionName}] 매수 로직을 실행합니다.");
            var searchedStocks = await GetConditionSearchResultAsync(strategy, account);
            if (searchedStocks == null || !searchedStocks.Any())
            {
                Logger.Instance.Add($" -> [{strategy.ConditionName}] 결과: 포착된 종목 없음");
                strategy.LastExecutionDate = DateTime.Today;
                StrategyRepository.Save(_strategies);
                Logger.Instance.Add($"[{strategy.ConditionName}] 포착된 종목이 없어 오늘 전략 실행을 완료 처리합니다.");
                return;
            }
            Logger.Instance.Add($" -> [{strategy.ConditionName}] 결과: {searchedStocks.Count}개 종목 포착");
            var detailedStocks = await FetchAllStockDataAsync(searchedStocks);
            var prioritizedStocks = CalculatePriorityAndSort(detailedStocks, strategy.TradeSettings.Buy.Priority);
            var stocksToOrder = CalculateOrderQuantity(prioritizedStocks, account, strategy.TradeSettings.Buy);
            foreach (var stock in stocksToOrder)
            {
                Logger.Instance.Add($" -> [매수 주문 시도] 종목: {stock.StockName}, 가격: {stock.OrderPrice:N0}, 수량: {stock.OrderQuantity}");
                await _apiService.SendBuyOrderAsync(account, stock.StockCode, stock.OrderQuantity, stock.OrderPrice, "0");
                await Task.Delay(300);
            }
            strategy.LastExecutionDate = DateTime.Today;
            StrategyRepository.Save(_strategies);
            await _mainWindow.UpdateStockSubscriptionAsync(account);
            Logger.Instance.Add($"[{strategy.ConditionName}] 매수 주문 시도가 완료되어, 오늘 전략 실행을 완료 처리합니다.");
        }

        private async Task<List<SearchedStock>> FetchAllStockDataAsync(List<SearchedStock> initialList)
        {
            var primaryAccount = _accountList.FirstOrDefault(a => a.TokenStatus == "Success");
            if (primaryAccount == null) return new List<SearchedStock>();
            Logger.Instance.Add("포착된 종목들의 상세 정보 조회를 시작합니다.");
            var detailedList = new List<SearchedStock>();
            foreach (var stock in initialList)
            {
                var dailyChartData = await _apiService.GetDailyChartAsync(primaryAccount, stock.StockCode, DateTime.Today.AddDays(-30).ToString("yyyyMMdd"));
                if (dailyChartData != null && dailyChartData.Any())
                {
                    var latestData = dailyChartData.First();
                    stock.CurrentPrice = latestData.ClosePrice;
                    stock.Volume = latestData.Volume;
                    stock.TradingAmount = latestData.TradingAmount;
                    stock.DailyChart = dailyChartData;
                    detailedList.Add(stock);
                }
                await Task.Delay(300);
            }
            return detailedList;
        }

        private List<SearchedStock> CalculatePriorityAndSort(List<SearchedStock> stocks, string priority)
        {
            foreach (var stock in stocks)
            {
                if (stock.DailyChart == null || !stock.DailyChart.Any())
                {
                    stock.PriorityScore = double.MinValue;
                    continue;
                }
                var today = stock.DailyChart.First();
                if (stock.PreviousClosePrice == 0) stock.PreviousClosePrice = stock.DailyChart.Count > 1 ? stock.DailyChart[1].ClosePrice : today.OpenPrice;
                double high = today.HighPrice;
                double low = today.LowPrice;
                double open = today.OpenPrice;
                double close = today.ClosePrice;
                long volume = today.Volume > 0 ? today.Volume : 1;
                long amount = today.TradingAmount;
                long marketCap = stock.MarketCap;
                double prevClose = stock.PreviousClosePrice;
                switch (priority)
                {
                    case "거래대금": stock.PriorityScore = amount; break;
                    case "거래량": stock.PriorityScore = volume; break;
                    case "거래대금/시가총액": stock.PriorityScore = marketCap > 0 ? amount / (double)marketCap : 0; break;
                    case "종가/고가": stock.PriorityScore = high > 0 ? close / high : 0; break;
                    case "(고-피봇)/전종": stock.PriorityScore = (high - (high + close + low) / 3) / prevClose; break;
                    case "(고-피봇2)/전종": stock.PriorityScore = (high - (high + close * 2 + low) / 4) / prevClose; break;
                    case "(고-종)/전종": stock.PriorityScore = (high - close) / prevClose; break;
                    case "(고+종-피봇2)/전종": stock.PriorityScore = (high + close - ((high + close * 2 + low) / 4) * 2) / prevClose; break;
                    case "(고+종-시-저)/전종": stock.PriorityScore = (high + close - open - low) / prevClose; break;
                    case "(고+종-시-저)/전종+Ln(거래량)": stock.PriorityScore = ((high + close - open - low) / prevClose) * 100 + Math.Log(volume); break;
                    default: stock.PriorityScore = 0; break;
                }
            }
            return stocks.OrderByDescending(s => s.PriorityScore).ToList();
        }

        private List<SearchedStock> CalculateOrderQuantity(List<SearchedStock> stocks, AccountInfo account, BuySettings settings)
        {
            int maxStocksToBuy = settings.BuyWeight > 0 ? (int)(100 / settings.BuyWeight) : 0;
            if (maxStocksToBuy == 0) return new List<SearchedStock>();
            var selectedStocks = stocks.Take(maxStocksToBuy).ToList();
            Logger.Instance.Add($" -> 매수 비중({settings.BuyWeight}%)에 따라 최대 {maxStocksToBuy}개 종목을 매수합니다.");
            double budgetPerStock = account.EstimatedDepositAsset * (settings.BuyWeight / 100.0);
            foreach (var stock in selectedStocks)
            {
                stock.OrderPrice = stock.CurrentPrice;
                if (stock.OrderPrice > 0)
                {
                    stock.OrderQuantity = (int)(budgetPerStock / stock.OrderPrice);
                }
            }
            return selectedStocks.Where(s => s.OrderQuantity > 0).ToList();
        }

        public async Task CheckSellConditionsAsync(AccountInfo account, HoldingStock stock)
        {
            if (!IsTradingEnabled || stock.TradableQuantity <= 0) return;

            var strategy = _strategies.FirstOrDefault(s => s.AccountNumber == account.AccountNumber);
            if (strategy == null || strategy.TradeSettings == null) return;

            string sellOrderKey = $"{account.AccountNumber}_{stock.StockCode}";
            if (_sellOrderTimestamps.TryGetValue(sellOrderKey, out DateTime lastOrderTime) && (DateTime.Now - lastOrderTime).TotalSeconds < 60)
            {
                return; // 60초 이내에 이미 매도 주문을 보냈으면 중복 실행 방지
            }

            var sellSettings = strategy.TradeSettings.Sell;
            double currentPrice = stock.CurrentPrice;
            bool shouldSell = false;
            string reason = "";

            switch (sellSettings.TargetPriceType)
            {
                case "단순":
                    if (currentPrice >= sellSettings.SimpleTargetPrice)
                    {
                        shouldSell = true;
                        reason = $"단순 목표가({sellSettings.SimpleTargetPrice:N0}) 도달";
                    }
                    break;
                case "트레일링":
                    if (stock.HighPrice >= sellSettings.TrailingTriggerPrice)
                    {
                        double trailingStopPrice = stock.HighPrice * (1 - sellSettings.TrailingStopRate / 100.0);
                        if (currentPrice <= trailingStopPrice)
                        {
                            shouldSell = true;
                            reason = $"트레일링 스탑 발동 (고점: {stock.HighPrice:N0}, 하락율: {sellSettings.TrailingStopRate}%)";
                        }
                    }
                    break;
                case "스탑로스":
                    if (currentPrice <= sellSettings.StopLossPreservePrice)
                    {
                        shouldSell = true;
                        reason = $"스탑로스 보존가({sellSettings.StopLossPreservePrice:N0}) 하회";
                    }
                    else if (currentPrice >= sellSettings.StopLossTargetPrice)
                    {
                        shouldSell = true;
                        reason = $"스탑로스 목표가({sellSettings.StopLossTargetPrice:N0}) 도달";
                    }
                    break;
            }

            var now = DateTime.Now;
            var sellEndTime = DateTime.Today.AddHours(sellSettings.SellEndHour).AddMinutes(sellSettings.SellEndMinute);
            if (now >= sellEndTime)
            {
                shouldSell = true;
                reason = $"매도 종료시간({sellEndTime:HH:mm}) 도달";
            }

            if (shouldSell)
            {
                Logger.Instance.Add($"[매도 조건 충족] {stock.StockName} ({reason}) -> 시장가 매도 주문 실행");
                _sellOrderTimestamps[sellOrderKey] = DateTime.Now; // 매도 주문 시간 기록
                await _apiService.SendSellOrderAsync(account, stock.StockCode, stock.TradableQuantity, 0, "3");
            }
        }

        private async Task<List<SearchedStock>> GetConditionSearchResultAsync(StrategyInfo strategy, AccountInfo account)
        {
            try
            {
                var ws = _mainWindow.GetWebSocketByAppKey(account.AppKey);
                if (ws == null)
                {
                    Logger.Instance.Add($"[오류] 조건검색({strategy.ConditionName})을 위한 웹소켓을 찾을 수 없습니다.");
                    return null;
                }
                var requestPacket = new { trnm = "CNSRREQ", seq = strategy.ConditionIndex, search_type = "0", stex_tp = "K", cont_yn = "N", next_key = "" };
                var response = await _sendWsRequestAsync(ws, "CNSRREQ", requestPacket);
                if (response?["return_code"]?.ToString() == "0")
                {
                    var stocks = new List<SearchedStock>();
                    if (response["data"] is JArray dataArray)
                    {
                        foreach (var item in dataArray.OfType<JObject>())
                        {
                            stocks.Add(new SearchedStock(item["9001"]?.ToString().TrimStart('A'), item["302"]?.ToString()));
                        }
                    }
                    return stocks;
                }
                else
                {
                    Logger.Instance.Add($"[오류] 조건검색({strategy.ConditionName}) API 응답 오류: {response?["return_msg"]}");
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Add($"[오류] 조건검색({strategy.ConditionName}) 요청 중 예외: {ex.Message}");
            }
            return null;
        }

        public void Dispose() { }
    }
}
