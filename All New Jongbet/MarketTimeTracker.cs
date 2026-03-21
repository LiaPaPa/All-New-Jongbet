using System;

namespace All_New_Jongbet
{
    /// <summary>
    /// 장시작시간(type 0s) 실시간 데이터를 통해 당일 개장/폐장 시각을 추적
    /// </summary>
    public class MarketTimeTracker
    {
        public DateTime? TodayMarketOpen { get; private set; }
        public DateTime? TodayMarketClose { get; private set; }
        public string CurrentMarketStatus { get; private set; } // "0", "2", "3", "4"

        public event EventHandler<MarketTimeDetectedEventArgs> MarketTimeDetected;

        /// <summary>
        /// type "0s" 실시간 데이터 values 객체 처리
        /// </summary>
        public void ProcessMarketTimeData(Newtonsoft.Json.Linq.JObject values)
        {
            if (values == null) return;

            string status = values["215"]?.ToString();      // 장운영구분
            string currentTime = values["20"]?.ToString();  // 현재시각 (HHmmss)
            string remainTime = values["214"]?.ToString();  // 남은시간 (HHmmss)

            if (string.IsNullOrEmpty(status) || string.IsNullOrEmpty(currentTime) || string.IsNullOrEmpty(remainTime))
                return;

            // 장시작 시각 감지 (장운영구분 = 0 = 장시작전)
            if (status == "0" && !TodayMarketOpen.HasValue)
            {
                try
                {
                    var now = ParseTime(currentTime);
                    var remain = ParseTime(remainTime);
                    TodayMarketOpen = DateTime.Today.Add(now).Add(remain);
                    
                    Logger.Instance.Add($"[장시간 감지] 오늘 개장시각: {TodayMarketOpen:HH:mm:ss}");
                    OnMarketTimeDetected(new MarketTimeDetectedEventArgs
                    {
                        MarketOpen = TodayMarketOpen,
                        MarketClose = TodayMarketClose
                    });
                }
                catch (Exception ex)
                {
                    Logger.Instance.Add($"[오류] 개장시각 파싱 실패: {ex.Message}");
                }
            }
            // 장종료 시각 감지 (장운영구분 = 2 = 동시호가 = 종료 10분전)
            else if (status == "2" && !TodayMarketClose.HasValue)
            {
                try
                {
                    var now = ParseTime(currentTime);
                    var remain = ParseTime(remainTime);
                    TodayMarketClose = DateTime.Today.Add(now).Add(remain);
                    
                    Logger.Instance.Add($"[장시간 감지] 오늘 폐장시각: {TodayMarketClose:HH:mm:ss}");
                    OnMarketTimeDetected(new MarketTimeDetectedEventArgs
                    {
                        MarketOpen = TodayMarketOpen,
                        MarketClose = TodayMarketClose
                    });
                }
                catch (Exception ex)
                {
                    Logger.Instance.Add($"[오류] 폐장시각 파싱 실패: {ex.Message}");
                }
            }

            CurrentMarketStatus = status;
        }

        /// <summary>
        /// HHmmss 형식 문자열을 TimeSpan으로 변환
        /// </summary>
        private TimeSpan ParseTime(string hhmmss)
        {
            if (hhmmss.Length != 6)
                throw new ArgumentException($"Invalid time format: {hhmmss}");

            int hh = int.Parse(hhmmss.Substring(0, 2));
            int mm = int.Parse(hhmmss.Substring(2, 2));
            int ss = int.Parse(hhmmss.Substring(4, 2));
            return new TimeSpan(hh, mm, ss);
        }

        /// <summary>
        /// 자정에 당일 데이터 초기화
        /// </summary>
        public void ResetDailyData()
        {
            TodayMarketOpen = null;
            TodayMarketClose = null;
            CurrentMarketStatus = null;
            Logger.Instance.Add("[장시간] 일별 데이터 초기화");
        }

        protected virtual void OnMarketTimeDetected(MarketTimeDetectedEventArgs e)
        {
            MarketTimeDetected?.Invoke(this, e);
        }
    }

    public class MarketTimeDetectedEventArgs : EventArgs
    {
        public DateTime? MarketOpen { get; set; }
        public DateTime? MarketClose { get; set; }
    }
}
