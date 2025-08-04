using Newtonsoft.Json;
using System.Collections.Generic;

namespace All_New_Jongbet
{
    // ka10001 주식기본정보요청 응답
    public class StockBasicInfo
    {
        [JsonProperty("stk_cd")]
        public string StockCode { get; set; }

        [JsonProperty("stk_nm")]
        public string StockName { get; set; }

        // ... (필요에 따라 가이드의 다른 필드들도 추가)
    }

    // ka10081 주식일봉차트조회요청 응답
    public class DailyChartData
    {
        [JsonProperty("stk_dt")]
        public string Date { get; set; }

        [JsonProperty("stk_oprc")]
        public double OpenPrice { get; set; }

        [JsonProperty("stk_hgprc")]
        public double HighPrice { get; set; }

        [JsonProperty("stk_lwprc")]
        public double LowPrice { get; set; }

        [JsonProperty("stk_clprc")]
        public double ClosePrice { get; set; }

        [JsonProperty("acml_vol")]
        public long Volume { get; set; }
    }

    // 조건검색된 종목의 모든 정보를 통합 관리하는 클래스
    public class SearchedStock
    {
        public string StockCode { get; set; }
        public string StockName { get; set; }
        public StockBasicInfo BasicInfo { get; set; }
        public List<DailyChartData> DailyChart { get; set; }

        public SearchedStock(string code, string name)
        {
            StockCode = code;
            StockName = name;
            DailyChart = new List<DailyChartData>();
        }
    }
}
