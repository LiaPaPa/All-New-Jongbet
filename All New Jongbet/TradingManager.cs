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
        private readonly KiwoomApiService _apiService;
        private readonly ObservableCollection<StrategyInfo> _strategies;
        private readonly ObservableCollection<AccountInfo> _accountList;
        private readonly ApiRequestScheduler _apiRequestScheduler;
        private readonly ClientWebSocket _ws;
        private readonly ConcurrentDictionary<string, TaskCompletionSource<JObject>> _wsResponseTasks;
        private Dictionary<int, bool> _liquidationExecuted;

        public TradingManager(KiwoomApiService apiService, ObservableCollection<StrategyInfo> strategies, ObservableCollection<AccountInfo> accountList, ApiRequestScheduler scheduler, ClientWebSocket ws, ConcurrentDictionary<string, TaskCompletionSource<JObject>> wsResponseTasks)
        {
            _apiService = apiService;
            _strategies = strategies;
            _accountList = accountList;
            _apiRequestScheduler = scheduler;
            _ws = ws;
            _wsResponseTasks = wsResponseTasks;
            _liquidationExecuted = new Dictionary<int, bool>();
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
            DateTime now = DateTime.Now;
            if (!App.IsDebugMode && now.Hour >= 18)
            {
                if (now.Hour == 18 && now.Minute < 1)
                {
                    _liquidationExecuted.Clear();
                }
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
            // CHANGED: 로그 형식 통일
            Logger.Instance.Add($"[{strategy.ConditionName}] [청산] 로직을 시작합니다.");

            var unfilledOrders = await _apiService.GetUnfilledOrdersAsync(account);
            await Task.Delay(MainWindow.ApiRequestDelay);

            var buyOrdersToCancel = unfilledOrders.Where(o => o.OrderTypeCode.Contains("매수")).ToList();
            if (buyOrdersToCancel.Any())
            {
                // CHANGED: 로그 형식 통일
                Logger.Instance.Add($"[{strategy.ConditionName}] [청산] 미체결 매수 주문 {buyOrdersToCancel.Count}건을 취소합니다.");
                foreach (var order in buyOrdersToCancel)
                {
                    await _apiService.SendCancelOrderAsync(account, order.StockCode, order.OrderNumber, order.UnfilledQuantity);
                    await Task.Delay(MainWindow.ApiRequestDelay);
                }
            }

            await _apiService.GetAccountBalanceAsync(account);
            await Task.Delay(MainWindow.ApiRequestDelay);
            var holdings = account.HoldingStockList;

            var sellOrdersToCancel = unfilledOrders.Where(o => o.OrderTypeCode.Contains("매도")).ToList();
            foreach (var order in sellOrdersToCancel)
            {
                // CHANGED: 로그 형식 통일
                Logger.Instance.Add($"[{strategy.ConditionName}] [청산] 미체결 매도 주문({order.StockName}) 취소 후 시장가로 재주문합니다.");
                await _apiService.SendCancelOrderAsync(account, order.StockCode, order.OrderNumber, order.UnfilledQuantity);
                await Task.Delay(MainWindow.ApiRequestDelay);
            }

            if (holdings == null || !holdings.Any())
            {
                // CHANGED: 로그 형식 통일
                Logger.Instance.Add($"[{strategy.ConditionName}] [청산] 청산할 보유 종목이 없습니다.");
                return;
            }

            // CHANGED: 로그 형식 통일
            Logger.Instance.Add($"[{strategy.ConditionName}] [청산] 보유 종목 {holdings.Count}개를 시장가로 매도합니다.");
            foreach (var stock in holdings)
            {
                if (stock.TradableQuantity > 0)
                {
                    await _apiService.SendSellOrderAsync(account, stock.StockCode, stock.TradableQuantity, 0, "3");
                    await Task.Delay(MainWindow.ApiRequestDelay);
                }
            }
            // CHANGED: 로그 형식 통일
            Logger.Instance.Add($"[{strategy.ConditionName}] [청산] 로직을 종료합니다.");
        }

        private async Task ExecuteBuyLogicAsync(StrategyInfo strategy, AccountInfo account)
        {
            // CHANGED: 로그 형식 통일
            Logger.Instance.Add($"[{strategy.ConditionName}] [매수] 로직을 시작합니다.");

            var searchedStocks = await GetConditionSearchResultAsync(strategy);
            if (searchedStocks == null || !searchedStocks.Any())
            {
                // CHANGED: 로그 형식 통일
                Logger.Instance.Add($"[{strategy.ConditionName}] [매수] 조건검색 결과: 포착된 종목이 없습니다.");
                strategy.LastExecutionDate = DateTime.Today;
                StrategyRepository.Save(_strategies);
                // CHANGED: 로그 형식 통일
                Logger.Instance.Add($"[{strategy.ConditionName}] [매수] 로직을 종료합니다.");
                return;
            }
            // CHANGED: 로그 형식 통일
            Logger.Instance.Add($"[{strategy.ConditionName}] [매수] 조건검색 결과: {searchedStocks.Count}개 종목 포착.");

            var detailedStocks = await FetchAllStockDataAsync(searchedStocks);
            var prioritizedStocks = CalculatePriorityAndSort(detailedStocks, strategy.TradeSettings.Buy.Priority);
            var stocksToOrder = CalculateOrderQuantity(prioritizedStocks, account, strategy.TradeSettings.Buy);

            foreach (var stock in stocksToOrder)
            {
                // CHANGED: 로그 형식 통일
                Logger.Instance.Add($"[{strategy.ConditionName}] [매수] 주문 실행: {stock.StockName} ({stock.OrderPrice:N0}원 x {stock.OrderQuantity}주)");
                await _apiService.SendBuyOrderAsync(account, stock.StockCode, stock.OrderQuantity, stock.OrderPrice, "5");
                await Task.Delay(MainWindow.ApiRequestDelay);
            }

            strategy.LastExecutionDate = DateTime.Today;
            StrategyRepository.Save(_strategies);
            // CHANGED: 로그 형식 통일
            Logger.Instance.Add($"[{strategy.ConditionName}] [매수] 모든 주문 전송이 완료되어 로직을 종료합니다.");
        }

        private async Task<List<SearchedStock>> FetchAllStockDataAsync(List<SearchedStock> initialList)
        {
            var primaryAccount = _accountList.FirstOrDefault(a => a.TokenStatus == "Success");
            if (primaryAccount == null) return new List<SearchedStock>();

            var stockCodes = initialList.Select(s => s.StockCode).ToArray();
            var detailedStocks = await _apiService.GetWatchlistDetailsAsync(primaryAccount, stockCodes);
            await Task.Delay(MainWindow.ApiRequestDelay);

            var chartResults = new ConcurrentDictionary<string, List<DailyChartData>>();
            var tasks = new List<Task>();
            string today = DateTime.Today.ToString("yyyyMMdd");

            foreach (var stock in detailedStocks)
            {
                var tcs = new TaskCompletionSource<bool>();
                tasks.Add(tcs.Task);

                _apiRequestScheduler.EnqueueRequest(async (acc) => {
                    try
                    {
                        var chartData = await _apiService.GetDailyChartAsync(acc, stock.StockCode, today);
                        chartResults[stock.StockCode] = chartData;
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.Add($"[오류] {stock.StockName} 일봉 조회 중 예외: {ex.Message}");
                        chartResults[stock.StockCode] = new List<DailyChartData>();
                    }
                    finally
                    {
                        tcs.SetResult(true);
                    }
                });
            }
            await Task.WhenAll(tasks);

            foreach (var stock in detailedStocks)
            {
                stock.DailyChart.Clear();
                if (chartResults.TryGetValue(stock.StockCode, out var chart))
                {
                    stock.DailyChart.AddRange(chart);
                }
            }
            return detailedStocks;
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
                if (stock.PreviousClosePrice == 0) continue;

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
            // CHANGED: 로그 형식 통일
            Logger.Instance.Add($"[{_strategies.FirstOrDefault(s => s.AccountNumber == account.AccountNumber)?.ConditionName}] [매수] 매수 비중({settings.BuyWeight}%)에 따라 최대 {maxStocksToBuy}개 종목을 주문합니다.");

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

        public Task CheckSellConditionsAsync(AccountInfo account, HoldingStock stock)
        {
            Logger.Instance.Add($"[{account.AccountNumber}] {stock.StockName}({stock.StockCode}) 시세 변경 감지 -> 매도 조건 확인 실행... (현재가: {stock.CurrentPrice:N0})");
            return Task.CompletedTask;
        }

        public async Task<List<ConditionInfo>> GetConditionListAsync()
        {
            try
            {
                var requestPacket = new { trnm = "CNSRLST" };
                var response = await SendWsRequestAsync("CNSRLST", requestPacket);

                if (response?["return_code"]?.ToString() == "0")
                {
                    var conditions = new List<ConditionInfo>();
                    if (response["data"] is JObject data && data["list"] is JArray list)
                    {
                        foreach (var item in list)
                        {
                            conditions.Add(new ConditionInfo
                            {
                                Index = item["seq"]?.ToString(),
                                Name = item["cnsr_nm"]?.ToString()
                            });
                        }
                    }
                    Logger.Instance.Add($"조건검색식 목록 조회 성공: {conditions.Count}개");
                    return conditions;
                }
                Logger.Instance.Add($"[오류] 조건검색식 목록 조회 실패: {response?["return_msg"]}");
            }
            catch (Exception ex)
            {
                Logger.Instance.Add($"[오류] 조건검색식 목록 조회 중 예외: {ex.Message}");
            }
            return new List<ConditionInfo>();
        }

        private async Task<List<SearchedStock>> GetConditionSearchResultAsync(StrategyInfo strategy)
        {
            try
            {
                var requestPacket = new { trnm = "CNSRREQ", seq = strategy.ConditionIndex, search_type = "0", stex_tp = "K", cont_yn = "N", next_key = "" };
                var response = await SendWsRequestAsync("CNSRREQ", requestPacket);
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

        private async Task<JObject> SendWsRequestAsync(string trnm, object requestPacket)
        {
            var tcs = new TaskCompletionSource<JObject>();
            if (!_wsResponseTasks.TryAdd(trnm, tcs))
            {
                if (_wsResponseTasks.TryRemove(trnm, out var oldTcs))
                {
                    oldTcs?.TrySetCanceled();
                }
                _wsResponseTasks.TryAdd(trnm, tcs);
            }
            await _apiService.SendWsMessageAsync(_ws, requestPacket);
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
            {
                var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(-1, cts.Token));
                if (completedTask == tcs.Task)
                {
                    _wsResponseTasks.TryRemove(trnm, out _);
                    return await tcs.Task;
                }
                else
                {
                    _wsResponseTasks.TryRemove(trnm, out _);
                    throw new TimeoutException($"{trnm} 요청 응답 시간 초과.");
                }
            }
        }

        public void Dispose() { }
    }
}
