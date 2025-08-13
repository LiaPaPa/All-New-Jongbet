// StockData.cs 파일 전체를 아래 코드로 교체하세요.

using Newtonsoft.Json;
using System.Collections.Generic;

namespace All_New_Jongbet
{
    public class DailyChartData
    {
        // CHANGED: 키움증권 공식 API 문서의 예시 응답에 맞게 JsonProperty 수정
        [JsonProperty("dt")]
        public string Date { get; set; }

        [JsonProperty("open_pric")]
        public double OpenPrice { get; set; }

        [JsonProperty("high_pric")]
        public double HighPrice { get; set; }

        [JsonProperty("low_pric")]
        public double LowPrice { get; set; }

        [JsonProperty("cur_prc")]
        public double ClosePrice { get; set; }

        [JsonProperty("trde_qty")]
        public long Volume { get; set; }

        [JsonProperty("trde_prica")]
        public long TradingAmount { get; set; }
    }

    public class SearchedStock
    {
        // 기본 정보
        public string StockCode { get; set; }
        public string StockName { get; set; }

        // ka10095 (관심종목정보요청)으로 채워질 정보
        public double CurrentPrice { get; set; }
        public double PreviousClosePrice { get; set; }
        public long Volume { get; set; }
        public long TradingAmount { get; set; }
        public long MarketCap { get; set; } // 시가총액

        // ka10081 (주식일봉차트조회)로 채워질 정보
        public List<DailyChartData> DailyChart { get; set; }

        // 우선순위 계산 및 주문에 필요한 정보
        public double PriorityScore { get; set; }
        public int OrderQuantity { get; set; }
        public double OrderPrice { get; set; }


        public SearchedStock(string code, string name)
        {
            StockCode = code;
            StockName = name;
            DailyChart = new List<DailyChartData>();
        }
    }
}