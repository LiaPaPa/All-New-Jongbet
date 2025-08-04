using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace All_New_Jongbet
{
    public class TradingManager
    {
        private readonly KiwoomApiService _apiService;
        private readonly ApiRequestScheduler _scheduler;
        private readonly List<StrategyInfo> _strategies;
        private readonly Dictionary<int, bool> _executedStrategies = new Dictionary<int, bool>();

        public TradingManager(KiwoomApiService apiService, ApiRequestScheduler scheduler, IEnumerable<StrategyInfo> strategies)
        {
            _apiService = apiService;
            _scheduler = scheduler;
            _strategies = strategies.ToList();
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            Logger.Instance.Add("트레이딩 매니저를 시작합니다. 전략 실행 시간을 감시합니다.");
            while (!cancellationToken.IsCancellationRequested)
            {
                DateTime now = DateTime.Now;

                foreach (var strategy in _strategies)
                {
                    // 오늘 이미 실행된 전략인지 확인
                    if (_executedStrategies.ContainsKey(strategy.StrategyNumber)) continue;

                    // TradeSettings에서 매수 시작 시간 가져오기 (파일 로드 필요)
                    // 이 부분은 TradeSettings 로드 로직과 연동되어야 합니다.
                    // 지금은 하드코딩된 시간으로 테스트합니다.
                    TimeSpan buyStartTime = TimeSpan.Parse("09:05:00");

                    if (now.TimeOfDay >= buyStartTime)
                    {
                        _executedStrategies[strategy.StrategyNumber] = true; // 실행했다고 표시
                        _ = ExecuteStrategyAsync(strategy); // 비동기로 전략 실행
                    }
                }
                await Task.Delay(1000, cancellationToken); // 1초마다 시간 확인
            }
        }

        private async Task ExecuteStrategyAsync(StrategyInfo strategy)
        {
            Logger.Instance.Add($"전략 #{strategy.StrategyNumber} 실행 시작...");

            var accountForConditionSearch = _scheduler.GetNextAvailableAccount();
            if (accountForConditionSearch == null)
            {
                Logger.Instance.Add("사용 가능한 계좌가 없어 전략 실행을 중단합니다.");
                return;
            }

            strategy.SearchedStockList = await _apiService.GetConditionSearchResultAsync(accountForConditionSearch, strategy.ConditionName, strategy.ConditionIndex);
            Logger.Instance.Add($"{strategy.SearchedStockList.Count}개 종목 발견.");

            if (!strategy.SearchedStockList.Any()) return;

            var dataGatheringTasks = new List<Task>();
            foreach (var stock in strategy.SearchedStockList)
            {
                var tcsBasicInfo = new TaskCompletionSource<bool>();
                var tcsChartData = new TaskCompletionSource<bool>();
                dataGatheringTasks.Add(tcsBasicInfo.Task);
                dataGatheringTasks.Add(tcsChartData.Task);

                _scheduler.EnqueueRequest(async (account) => {
                    stock.BasicInfo = await _apiService.GetStockBasicInfoAsync(account, stock.StockCode);
                    tcsBasicInfo.SetResult(true);
                });

                _scheduler.EnqueueRequest(async (account) => {
                    stock.DailyChart = await _apiService.GetDailyChartAsync(account, stock.StockCode);
                    tcsChartData.SetResult(true);
                });
            }

            await Task.WhenAll(dataGatheringTasks);
            Logger.Instance.Add("모든 종목의 데이터 조회가 완료되었습니다.");

            // TODO: 실시간 체결 구독 및 우선순위 계산, 매수 주문 로직 추가
            Logger.Instance.Add("우선순위 계산 및 매수 주문 실행 단계.");
        }
    }
}
