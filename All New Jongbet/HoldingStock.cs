// HoldingStock.cs 파일 전체를 아래 코드로 교체하세요.

using Newtonsoft.Json;

namespace All_New_Jongbet
{
    public class HoldingStock : ViewModelBase
    {
        [JsonProperty("stk_cd")]
        public string StockCode { get; set; }

        [JsonProperty("stk_nm")]
        public string StockName { get; set; }

        private double _evaluationProfitLoss;
        [JsonProperty("evltv_prft")]
        public double EvaluationProfitLoss { get => _evaluationProfitLoss; set { _evaluationProfitLoss = value; OnPropertyChanged(); } }

        private double _profitRate;
        [JsonProperty("prft_rt")]
        public double ProfitRate { get => _profitRate; set { _profitRate = value; OnPropertyChanged(); } }

        private double _purchasePrice;
        [JsonProperty("pur_pric")]
        public double PurchasePrice { get => _purchasePrice; set { _purchasePrice = value; OnPropertyChanged(); } }

        private double _previousClosePrice;
        [JsonProperty("pred_close_pric")]
        public double PreviousClosePrice { get => _previousClosePrice; set { _previousClosePrice = value; OnPropertyChanged(); } }

        private int _holdingQuantity;
        [JsonProperty("rmnd_qty")]
        public int HoldingQuantity { get => _holdingQuantity; set { _holdingQuantity = value; OnPropertyChanged(); } }

        private int _tradableQuantity;
        [JsonProperty("trde_able_qty")]
        public int TradableQuantity { get => _tradableQuantity; set { _tradableQuantity = value; OnPropertyChanged(); } }

        private double _currentPrice;
        [JsonProperty("cur_prc")]
        // [수정] setter에서 자동 계산 로직을 모두 제거하여 순수한 속성으로 변경
        public double CurrentPrice { get => _currentPrice; set { _currentPrice = value; OnPropertyChanged(); } }

        private double _purchaseAmount;
        [JsonProperty("pur_amt")]
        public double PurchaseAmount { get => _purchaseAmount; set { _purchaseAmount = value; OnPropertyChanged(); } }

        private double _evaluationAmount;
        [JsonProperty("evlt_amt")]
        public double EvaluationAmount { get => _evaluationAmount; set { _evaluationAmount = value; OnPropertyChanged(); } }

        private double _fluctuationRate;
        public double FluctuationRate { get => _fluctuationRate; set { _fluctuationRate = value; OnPropertyChanged(); } }

        private long _cumulativeVolume;
        public long CumulativeVolume { get => _cumulativeVolume; set { _cumulativeVolume = value; OnPropertyChanged(); } }

        private double _highPrice;
        public double HighPrice { get => _highPrice; set { _highPrice = value; OnPropertyChanged(); } }

        private double _lowPrice;
        public double LowPrice { get => _lowPrice; set { _lowPrice = value; OnPropertyChanged(); } }
    }
}