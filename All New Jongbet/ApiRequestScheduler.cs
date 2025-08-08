using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace All_New_Jongbet
{
    public class ApiRequestScheduler
    {
        private readonly List<AccountInfo> _accounts;
        private readonly ConcurrentQueue<Func<AccountInfo, Task>> _requestQueue;
        private readonly long _minIntervalTicks;

        public ApiRequestScheduler(List<AccountInfo> activeAccounts)
        {
            _accounts = activeAccounts;
            _requestQueue = new ConcurrentQueue<Func<AccountInfo, Task>>();
            // CHANGED: 고정 값 대신 MainWindow의 ApiRequestDelay 값을 사용하도록 변경
            _minIntervalTicks = (long)(MainWindow.ApiRequestDelay / 1000.0 * Stopwatch.Frequency);
        }

        public void EnqueueRequest(Func<AccountInfo, Task> apiCall)
        {
            _requestQueue.Enqueue(apiCall);
        }

        public AccountInfo GetNextAvailableAccount()
        {
            if (!_accounts.Any()) return null;
            return _accounts.OrderBy(acc => acc.LastRequestTimestamp).First();
        }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            Logger.Instance.Add("API 요청 스케줄러를 시작합니다.");
            while (!cancellationToken.IsCancellationRequested)
            {
                var nextAvailableAccount = GetNextAvailableAccount();

                if (nextAvailableAccount != null && _requestQueue.TryDequeue(out var apiCall))
                {
                    long currentTimestamp = Stopwatch.GetTimestamp();
                    long elapsedTicks = currentTimestamp - nextAvailableAccount.LastRequestTimestamp;

                    if (elapsedTicks < _minIntervalTicks)
                    {
                        long delayTicks = _minIntervalTicks - elapsedTicks;
                        int delayMs = (int)(delayTicks * 1000.0 / Stopwatch.Frequency);
                        if (delayMs > 0)
                        {
                            await Task.Delay(delayMs, cancellationToken);
                        }
                    }

                    nextAvailableAccount.LastRequestTimestamp = Stopwatch.GetTimestamp();
                    _ = apiCall(nextAvailableAccount);
                }
                else
                {
                    await Task.Delay(50, cancellationToken);
                }
            }
            Logger.Instance.Add("API 요청 스케줄러가 종료되었습니다.");
        }
    }
}