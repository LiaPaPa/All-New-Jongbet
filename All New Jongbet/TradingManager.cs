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
        private HashSet<string> _todayBoughtStocks = new HashSet<string>(); // 당일 매수 종목 추적

        public bool IsTradingEnabled { get; private set; } = true;
        public bool IsTestMode { get; set; } = false; // Test Mode Property

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
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
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
                _todayBoughtStocks.Clear(); // 자정 초기화
            }

            // 정규 거래시간 체크 (09:00 ~ 15:30)
            // 디버그 모드에서도 장 시간 외에는 청산/매수 시도하지 않음
            bool isMarketHours = IsMarketOpen(now);

            if (!isMarketHours)
            {
                // 장 종료 후 초기화 (18시에 한 번만)
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

                var buyStartTime = CalculateActualTime(
                    strategy.TradeSettings.Buy.BuyStartTimeMode,
                    strategy.TradeSettings.Buy.BuyStartHour,
                    strategy.TradeSettings.Buy.BuyStartMinute,
                    strategy.TradeSettings.Buy.BuyStartSecond,
                    strategy.TradeSettings.Buy.BuyStartRelativeBase,
                    strategy.TradeSettings.Buy.BuyStartRelativeOffsetMinutes
                );

                var liquidationTime = CalculateActualTime(
                    strategy.TradeSettings.Sell.LiquidationTimeMode,
                    strategy.TradeSettings.Sell.LiquidationHour,
                    strategy.TradeSettings.Sell.LiquidationMinute,
                    strategy.TradeSettings.Sell.LiquidationSecond,
                    strategy.TradeSettings.Sell.LiquidationRelativeBase,
                    strategy.TradeSettings.Sell.LiquidationRelativeOffsetMinutes
                );

                if (now >= liquidationTime && !_liquidationExecuted.ContainsKey(strategy.StrategyNumber))
                {
                    _liquidationExecuted[strategy.StrategyNumber] = true;
                    await LiquidatePositionsIfNeededAsync(strategy, account);
                }
                if (now >= buyStartTime && strategy.LastExecutionDate < DateTime.Today)
                {
                    Logger.Instance.Add($"[{strategy.ConditionName}] 매수 시간 도달 (설정시간: {buyStartTime:HH:mm:ss}), 매수 로직 실행");
                    await ExecuteBuyLogicAsync(strategy, account);
                }
            }
        }

        /// <summary>
        /// 현재 시간이 정규 거래시간(09:00 ~ 15:30)인지 확인
        /// 주말과 공휴일은 별도로 체크하지 않음 (API 호출 실패로 자연스럽게 처리됨)
        /// </summary>
        private bool IsMarketOpen(DateTime now)
        {
            // 주말 체크
            if (now.DayOfWeek == DayOfWeek.Saturday || now.DayOfWeek == DayOfWeek.Sunday)
            {
                return false;
            }

            // 정규 거래시간: 09:00 ~ 15:30
            var marketOpen = new TimeSpan(9, 0, 0);
            var marketClose = new TimeSpan(15, 30, 0);
            var currentTime = now.TimeOfDay;

            return currentTime >= marketOpen && currentTime <= marketClose;
        }

        private DateTime CalculateActualTime(string timeMode, int hour, int minute, int second, string relativeBase, int relativeOffsetMinutes)
        {
            DateTime today = DateTime.Today;
            if (timeMode == "절대시간")
            {
                return today.AddHours(hour).AddMinutes(minute).AddSeconds(second);
            }
            else // 상대시간
            {
                DateTime baseTime;
                if (relativeBase == "장시작")
                {
                    baseTime = today.AddHours(9).AddMinutes(0); // 09:00
                }
                else // 장종료
                {
                    baseTime = today.AddHours(15).AddMinutes(30); // 15:30
                }

                return baseTime.AddMinutes(relativeOffsetMinutes);
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
            // 사용자의 청산 주문 방법("시장가" 또는 "현재가" 등) 가져오기
            string liquidationMethod = strategy.TradeSettings.Sell.LiquidationMethod == "시장가" ? "3" : "0";
            string orderTypeName = strategy.TradeSettings.Sell.LiquidationMethod == "시장가" ? "시장가" : "현재가";
            
            Logger.Instance.Add($" -> {holdings.Count}개의 보유 종목을 {orderTypeName}로 청산 매도합니다.");
            foreach (var stock in holdings)
            {
                if (stock.TradableQuantity > 0)
                {
                    int quantity = IsTestMode ? 0 : stock.TradableQuantity; // Test Mode: 0 quantity
                    if (IsTestMode) Logger.Instance.Add($"[TEST MODE] 청산 매도 주문 - 종목: {stock.StockName}, 수량: 0 (Original: {stock.TradableQuantity})");

                    await _apiService.SendSellOrderAsync(account, stock.StockCode, quantity, 0, liquidationMethod);
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
                await _apiService.SendBuyOrderAsync(account, stock.StockCode, stock.OrderQuantity, stock.OrderPrice, "5"); // 5 = 조건부지정가
                _todayBoughtStocks.Add(stock.StockCode); // 당일 매수 종목 추적
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
            if (primaryAccount == null || !initialList.Any()) return new List<SearchedStock>();
            Logger.Instance.Add($"포착된 {initialList.Count}개 종목들의 상세 정보 조회를 시작합니다. (ka10095 활용)");

            var stockCodes = initialList.Select(s => s.StockCode).ToList();
            var detailedList = await _apiService.GetMultiStockInfoAsync(primaryAccount, stockCodes);

            // Fetch된 detailedList에는 종목코드 기준의 SearchedStock 객체들이 담겨있음
            // Priority 계산을 위해 필요한 속성 연결 및 누락된 정보 병합
            foreach (var original in initialList)
            {
                var detailed = detailedList.FirstOrDefault(d => d.StockCode == original.StockCode);
                if (detailed != null)
                {
                    // 조건검색 시점에 CurrentPrice가 있으면 우선하고, 없으면 상세정보 사용
                    if (original.CurrentPrice > 0)
                        detailed.CurrentPrice = original.CurrentPrice;

                    // DailyChart가 GetMultiStockInfoAsync에서 단일 데이터로 생성되었음 (Priority 계산 목적)
                }
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

                if (IsTestMode)
                {
                    Logger.Instance.Add($"[TEST MODE] 매수 수량 계산 - 종목: {stock.StockName}, 계산된 수량: {stock.OrderQuantity} -> 0으로 변경");
                    stock.OrderQuantity = 0;
                }
            }
            // TestMode에서는 OrderQuantity가 0이어도 주문을 보내야 함
            if (IsTestMode)
            {
                return selectedStocks;
            }
            return selectedStocks.Where(s => s.OrderQuantity > 0).ToList();
        }

        public async Task CheckSellConditionsAsync(AccountInfo account, HoldingStock stock)
        {
            if (!IsTradingEnabled || stock.TradableQuantity <= 0) return;

            // 당일 매수한 종목은 매도 체크 제외
            if (_todayBoughtStocks.Contains(stock.StockCode))
            {
                return;
            }

            var strategy = _strategies.FirstOrDefault(s => s.AccountNumber == account.AccountNumber);
            if (strategy == null || strategy.TradeSettings == null) return;

            string sellOrderKey = $"{account.AccountNumber}_{stock.StockCode}";
            if (_sellOrderTimestamps.TryGetValue(sellOrderKey, out DateTime lastOrderTime) && (DateTime.Now - lastOrderTime).TotalSeconds < 60)
            {
                return; // 60초 이내에 이미 매도 주문을 보냈으면 중복 실행 방지
            }

            var sellSettings = strategy.TradeSettings.Sell;

            // 매도 시간 체크
            var sellStartTime = CalculateActualTime(
                sellSettings.SellStartTimeMode,
                sellSettings.SellStartHour,
                sellSettings.SellStartMinute,
                sellSettings.SellStartSecond,
                sellSettings.SellStartRelativeBase,
                sellSettings.SellStartRelativeOffsetMinutes
            );

            var sellEndTime = CalculateActualTime(
                sellSettings.SellEndTimeMode,
                sellSettings.SellEndHour,
                sellSettings.SellEndMinute,
                sellSettings.SellEndSecond,
                sellSettings.SellEndRelativeBase,
                sellSettings.SellEndRelativeOffsetMinutes
            );

            if (DateTime.Now < sellStartTime)
            {
                return; // 매도 시작 시간 전임
            }
            double currentPrice = stock.CurrentPrice;
            double purchasePrice = stock.PurchasePrice;

            if (purchasePrice <= 0)
            {
                return; // 매수가 오류 (0 이하) 시 매도 판단 중지
            }

            bool shouldSell = false;
            string reason = "";

            switch (sellSettings.TargetPriceType)
            {
                case "단순":
                    // 단순 목표가: 퍼센트를 절대 가격으로 변환
                    double simpleTargetPrice = purchasePrice * (1 + sellSettings.SimpleTargetPrice / 100.0);
                    if (currentPrice >= simpleTargetPrice)
                    {
                        shouldSell = true;
                        reason = $"단순 목표가({simpleTargetPrice:N0}, +{sellSettings.SimpleTargetPrice}%) 도달";
                    }
                    break;
                case "트레일링":
                    // 트레일링 발동 가격: 퍼센트를 절대 가격으로 변환
                    double trailingTriggerPrice = purchasePrice * (1 + sellSettings.TrailingTriggerPrice / 100.0);
                    if (stock.HighPrice >= trailingTriggerPrice)
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
                    // 스탑로스 목표가/보존가: 퍼센트를 절대 가격으로 변환
                    double stopLossPreservePrice = purchasePrice * (1 + sellSettings.StopLossPreservePrice / 100.0);
                    double stopLossTargetPrice = purchasePrice * (1 + sellSettings.StopLossTargetPrice / 100.0);

                    if (sellSettings.StopLossPreservePrice > 0)
                    {
                        // 보존가 로직: 최고가가 감시 시작가(목표가) 상회 후 보존가 하회 시
                        // 1분 단위 정각 감시 (틱 기반이므로 0~10초 사이에 들어온 틱을 통해 정각 확인)
                        if (stock.MaxPriceSincePurchase >= stopLossTargetPrice && currentPrice <= stopLossPreservePrice)
                        {
                            if (DateTime.Now.Second < 10)
                            {
                                shouldSell = true;
                                reason = $"스탑로스 보존가({stopLossPreservePrice:N0}, {sellSettings.StopLossPreservePrice}%) 하회 (최고가 {stock.MaxPriceSincePurchase:N0} 달성 후)";
                            }
                        }
                    }
                    else
                    {
                        // 일반 손절가 로직 (보존가 설정이 양수가 아닐 때 작동)
                        if (currentPrice <= stopLossPreservePrice)
                        {
                            shouldSell = true;
                            reason = $"스탑로스 손절가({stopLossPreservePrice:N0}, {sellSettings.StopLossPreservePrice}%) 하회";
                        }
                        else if (currentPrice >= stopLossTargetPrice && sellSettings.StopLossTargetPrice > 0)
                        {
                            shouldSell = true;
                            reason = $"스탑로스 목표가({stopLossTargetPrice:N0}, +{sellSettings.StopLossTargetPrice}%) 도달";
                        }
                    }
                    break;
            }

            // 반등컷 로직 (Bounce Cut)
            if (!shouldSell && sellSettings.UseReboundCut)
            {
                double lowPrice = stock.LowPrice;

                // 최저 수익률 = (저가 - 보유가) / 보유가 * 100
                double minProfitRate = ((lowPrice - purchasePrice) / purchasePrice) * 100.0;

                // 반등컷 활성화 조건: 최저 수익률이 -10% 이하
                if (minProfitRate <= -10.0)
                {
                    // 조건 1: 저가 대비 현재가가 5% 상승
                    double reboundFromLowRate = ((currentPrice - lowPrice) / lowPrice) * 100.0;

                    // 조건 2: (현재가 - 저가) / (보유가 - 저가) >= 50%
                    double recoveryRatio = 0;
                    if (purchasePrice > lowPrice)
                    {
                        recoveryRatio = ((currentPrice - lowPrice) / (purchasePrice - lowPrice)) * 100.0;
                    }

                    if (reboundFromLowRate >= 5.0)
                    {
                        shouldSell = true;
                        reason = $"반등컷 (저가 대비 +{reboundFromLowRate:F2}% 반등, 최저수익률: {minProfitRate:F2}%)";
                    }
                    else if (recoveryRatio >= 50.0)
                    {
                        shouldSell = true;
                        reason = $"반등컷 (회복률 {recoveryRatio:F2}%, 최저수익률: {minProfitRate:F2}%)";
                    }
                }
            }

            var now = DateTime.Now;
            // 매도 종료시간 도달 체크 (sellEndTime은 상단에서 계산됨)
            if (now >= sellEndTime)
            {
                shouldSell = true;
                reason = $"매도 종료시간({sellEndTime:HH:mm}) 도달";
            }

            if (shouldSell)
            {
                // 주문 타입 매핑: "현재가" -> "0", "시장가" -> "3", 기본값 "0"
                string orderTypeCode = sellSettings.OrderType == "시장가" ? "3" : "0";
                string orderTypeName = sellSettings.OrderType == "시장가" ? "시장가" : "현재가";

                Logger.Instance.Add($"[매도 조건 충족] {stock.StockName} ({reason}) -> {orderTypeName} 매도 주문 실행");
                _sellOrderTimestamps[sellOrderKey] = DateTime.Now; // 매도 주문 시간 기록

                int quantity = IsTestMode ? 0 : stock.TradableQuantity;
                if (IsTestMode) Logger.Instance.Add($"[TEST MODE] 매도 주문 - 종목: {stock.StockName}, 수량: 0 (Original: {stock.TradableQuantity})");

                await _apiService.SendSellOrderAsync(account, stock.StockCode, quantity, 0, orderTypeCode);
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
                            var stock = new SearchedStock(item["9001"]?.ToString().TrimStart('A'), item["302"]?.ToString());
                            // 조건검색 결과에서 현재가 파싱 ("10" = 현재가)
                            if (double.TryParse(item["10"]?.ToString(), out double currentPrice))
                            {
                                stock.CurrentPrice = Math.Abs(currentPrice); // 현재가는 절대값 사용 (하락 시 음수로 올 수 있음)
                            }
                            stocks.Add(stock);
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
