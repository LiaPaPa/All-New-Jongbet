// OrderHistoryItem.cs 파일 전체를 아래 코드로 교체하세요.

using Newtonsoft.Json;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace All_New_Jongbet
{
    public class OrderHistoryItem : INotifyPropertyChanged
    {
        [JsonIgnore]
        public string AccountNumber { get; set; }

        [JsonProperty("ord_dt")]
        public string OrderDate { get; set; }

        [JsonProperty("ord_no")]
        public string OrderNumber { get; set; }

        [JsonProperty("stk_cd")]
        public string StockCode { get; set; }

        [JsonProperty("stk_nm")]
        public string StockName { get; set; }

        private string _orderTime;
        [JsonProperty("ord_tm")]
        public string OrderTime
        {
            get => _orderTime;
            set { _orderTime = value; OnPropertyChanged(); }
        }

        private string _rawOrderTypeCode;
        [JsonProperty("io_tp_nm")]
        public string OrderTypeCode
        {
            get => _rawOrderTypeCode;
            set
            {
                _rawOrderTypeCode = value;
                OnPropertyChanged(nameof(OrderTypeDisplay));
                OnPropertyChanged(nameof(CardBrushKey));
            }
        }

        // [NEW] 정정/취소 구분을 위한 속성 추가
        [JsonProperty("mdfy_cncl")]
        public string ModificationCancellationType { get; set; }

        [JsonProperty("ord_qty")]
        public int OrderQuantity { get; set; }

        [JsonProperty("ord_uv")]
        public double OrderPrice { get; set; }

        private int _executedQuantity;
        [JsonProperty("cntr_qty")]
        public int ExecutedQuantity
        {
            get => _executedQuantity;
            set { _executedQuantity = value; OnPropertyChanged(); }
        }

        private int _unfilledQuantity;
        public int UnfilledQuantity
        {
            get => _unfilledQuantity;
            set { _unfilledQuantity = value; OnPropertyChanged(); }
        }

        public string OrderStatusFromApi { get; set; }

        private string _orderStatusDisplay;
        public string OrderStatusDisplay
        {
            get => _orderStatusDisplay;
            set { _orderStatusDisplay = value; OnPropertyChanged(); }
        }

        public string OrderTypeDisplay
        {
            get
            {
                if (string.IsNullOrEmpty(_rawOrderTypeCode)) return "기타";

                string tradeAction = "";
                if (_rawOrderTypeCode.Contains("매수") || _rawOrderTypeCode.Contains("+"))
                    tradeAction = "매수";
                else if (_rawOrderTypeCode.Contains("매도") || _rawOrderTypeCode.Contains("-"))
                    tradeAction = "매도";

                // [CHANGED] 정정/취소 여부를 조합하여 표시
                if (!string.IsNullOrEmpty(ModificationCancellationType) && !string.IsNullOrEmpty(tradeAction))
                {
                    return tradeAction + ModificationCancellationType;
                }

                if (!string.IsNullOrEmpty(tradeAction))
                    return tradeAction;

                return _rawOrderTypeCode.Replace("+", "").Replace("-", "").Replace("현금", "").Replace("신용", "");
            }
        }

        public string CardBrushKey
        {
            get
            {
                // [CHANGED] 정정/취소 주문도 Cancel 색상으로 표시
                if (!string.IsNullOrEmpty(ModificationCancellationType) && (ModificationCancellationType.Contains("취소") || ModificationCancellationType.Contains("정정")))
                {
                    return "CancelOrderCardBrush";
                }
                if (string.IsNullOrEmpty(_rawOrderTypeCode)) return "DefaultOrderCardBrush";
                if (_rawOrderTypeCode.Contains("매수") || _rawOrderTypeCode.Contains("+")) return "BuyOrderCardBrush";
                if (_rawOrderTypeCode.Contains("매도") || _rawOrderTypeCode.Contains("-")) return "SellOrderCardBrush";
                return "DefaultOrderCardBrush";
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
