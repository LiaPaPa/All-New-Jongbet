using Newtonsoft.Json;

namespace All_New_Jongbet
{
    public class OrderHistoryItem
    {
        // JSON 응답 필드와 매핑
        [JsonProperty("ord_dt")]
        public string OrderDate { get; set; }

        [JsonProperty("ord_gno")]
        public string OrderNumber { get; set; }

        [JsonProperty("stk_cd")]
        public string StockCode { get; set; }

        [JsonProperty("stk_nm")]
        public string StockName { get; set; }

        [JsonProperty("sell_buy_tp")]
        public string OrderTypeCode { get; set; } // "1": 매도, "2": 매수, "3": 매수정정 ...

        [JsonProperty("ord_qty")]
        public int OrderQuantity { get; set; }

        [JsonProperty("ord_pric")]
        public double OrderPrice { get; set; }

        [JsonProperty("exec_qty")]
        public int ExecutedQuantity { get; set; }

        [JsonProperty("unpd_qty")]
        public int UnfilledQuantity { get; set; }

        [JsonProperty("ord_stat")]
        public string OrderStatus { get; set; }

        // UI 표시를 위한 계산 속성
        public string OrderTypeDisplay => GetOrderTypeDisplay(OrderTypeCode);
        public string CardBrushKey => GetCardBrushKey(OrderTypeCode);

        private string GetOrderTypeDisplay(string typeCode)
        {
            switch (typeCode)
            {
                case "1": return "매도";
                case "2": return "매수";
                case "3": return "매수정정";
                case "4": return "매도정정";
                case "5": return "매수취소";
                case "6": return "매도취소";
                default: return "기타";
            }
        }

        private string GetCardBrushKey(string typeCode)
        {
            switch (typeCode)
            {
                case "2": case "3": return "BuyOrderCardBrush";  // 매수, 매수정정
                case "1": case "4": return "SellOrderCardBrush"; // 매도, 매도정정
                default: return "CancelOrderCardBrush"; // 취소 등
            }
        }
    }
}
