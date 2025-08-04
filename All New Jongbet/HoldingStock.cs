using Newtonsoft.Json;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace All_New_Jongbet
{
    // ViewModelBase를 상속받아 UI 자동 업데이트 지원
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
        public double CurrentPrice { get => _currentPrice; set { _currentPrice = value; OnPropertyChanged(); } }

        private double _purchaseAmount;
        [JsonProperty("pur_amt")]
        public double PurchaseAmount { get => _purchaseAmount; set { _purchaseAmount = value; OnPropertyChanged(); } }

        private double _purchaseCommission;
        [JsonProperty("pur_cmsn")]
        public double PurchaseCommission { get => _purchaseCommission; set { _purchaseCommission = value; OnPropertyChanged(); } }

        private double _evaluationAmount;
        [JsonProperty("evlt_amt")]
        public double EvaluationAmount { get => _evaluationAmount; set { _evaluationAmount = value; OnPropertyChanged(); } }

        private double _evaluationCommission;
        [JsonProperty("sell_cmsn")]
        public double EvaluationCommission { get => _evaluationCommission; set { _evaluationCommission = value; OnPropertyChanged(); } }

        private double _tax;
        [JsonProperty("tax")]
        public double Tax { get => _tax; set { _tax = value; OnPropertyChanged(); } }

        private double _totalCommission;
        [JsonProperty("sum_cmsn")]
        public double TotalCommission { get => _totalCommission; set { _totalCommission = value; OnPropertyChanged(); } }

        // NEW: 실시간 데이터 반영을 위한 속성 추가
        private double _changeFromPreviousDay;
        public double ChangeFromPreviousDay { get => _changeFromPreviousDay; set { _changeFromPreviousDay = value; OnPropertyChanged(); } }

        private double _fluctuationRate;
        public double FluctuationRate { get => _fluctuationRate; set { _fluctuationRate = value; OnPropertyChanged(); } }

        private double _bestAskPrice;
        public double BestAskPrice { get => _bestAskPrice; set { _bestAskPrice = value; OnPropertyChanged(); } }

        private double _bestBidPrice;
        public double BestBidPrice { get => _bestBidPrice; set { _bestBidPrice = value; OnPropertyChanged(); } }

        private long _cumulativeVolume;
        public long CumulativeVolume { get => _cumulativeVolume; set { _cumulativeVolume = value; OnPropertyChanged(); } }

        private long _cumulativeAmount;
        public long CumulativeAmount { get => _cumulativeAmount; set { _cumulativeAmount = value; OnPropertyChanged(); } }

        private double _openPrice;
        public double OpenPrice { get => _openPrice; set { _openPrice = value; OnPropertyChanged(); } }

        private double _highPrice;
        public double HighPrice { get => _highPrice; set { _highPrice = value; OnPropertyChanged(); } }

        private double _lowPrice;
        public double LowPrice { get => _lowPrice; set { _lowPrice = value; OnPropertyChanged(); } }
    }
}
