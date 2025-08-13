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

        private string _orderTypeCode;
        [JsonProperty("io_tp_nm")]
        public string OrderTypeCode
        {
            get => _orderTypeCode;
            set
            {
                string cleanedValue = value;
                if (!string.IsNullOrEmpty(cleanedValue))
                {
                    cleanedValue = cleanedValue.Replace("+", "")
                                               .Replace("-", "")
                                               .Replace("현금", "")
                                               .Replace("신용", "");
                }
                _orderTypeCode = cleanedValue;

                OnPropertyChanged(nameof(OrderTypeDisplay));
                OnPropertyChanged(nameof(CardBrushKey));
            }
        }

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
        // [수정] JsonProperty를 제거하여 각기 다른 필드명(ord_remnq, oso_qty)을 수동으로 매핑할 수 있도록 함
        public int UnfilledQuantity
        {
            get => _unfilledQuantity;
            set { _unfilledQuantity = value; OnPropertyChanged(); }
        }

        // [수정] JsonProperty를 제거하여 초기 조회 시 오류 방지. 실시간 데이터 수신 시에는 코드에서 직접 값을 할당.
        public string OrderStatusFromApi { get; set; }

        private string _orderStatusDisplay;
        public string OrderStatusDisplay
        {
            get => _orderStatusDisplay;
            set { _orderStatusDisplay = value; OnPropertyChanged(); }
        }

        public string OrderTypeDisplay => _orderTypeCode;
        public string CardBrushKey => GetCardBrushKey(_orderTypeCode);

        private string GetCardBrushKey(string typeCode)
        {
            if (string.IsNullOrEmpty(typeCode)) return "DefaultOrderCardBrush";

            if (typeCode.Contains("취소")) return "CancelOrderCardBrush";
            if (typeCode.Contains("매수")) return "BuyOrderCardBrush";
            if (typeCode.Contains("매도")) return "SellOrderCardBrush";

            return "DefaultOrderCardBrush";
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}