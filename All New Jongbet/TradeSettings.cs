using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace All_New_Jongbet
{
    // INotifyPropertyChanged 구현을 위한 기본 클래스
    public class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class BuySettings : ViewModelBase
    {
        private string _buyType = "일반";
        public string BuyType
        {
            get => _buyType;
            set { _buyType = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsTimeDivisionEnabled)); }
        }
        public bool IsTimeDivisionEnabled => _buyType == "시분할";

        private int _timeDivisionInterval;
        public int TimeDivisionInterval { get => _timeDivisionInterval; set { _timeDivisionInterval = value; OnPropertyChanged(); } }

        private string _orderType = "현재가";
        public string OrderType { get => _orderType; set { _orderType = value; OnPropertyChanged(); } }

        // CHANGED: string에서 int로 변경
        public int BuyStartHour { get; set; } = 9;
        public int BuyStartMinute { get; set; } = 0;
        public int BuyStartSecond { get; set; } = 0;

        private double _buyWeight = 10.0;
        public double BuyWeight { get => _buyWeight; set { _buyWeight = value; OnPropertyChanged(); } }

        private string _priority;
        public string Priority { get => _priority; set { _priority = value; OnPropertyChanged(); } }
    }

    public class SellSettings : ViewModelBase
    {
        private string _sellType = "일반";
        public string SellType
        {
            get => _sellType;
            set { _sellType = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsTimeDivisionEnabled)); }
        }
        public bool IsTimeDivisionEnabled => _sellType == "시분할";

        private int _timeDivisionInterval;
        public int TimeDivisionInterval { get => _timeDivisionInterval; set { _timeDivisionInterval = value; OnPropertyChanged(); } }

        private string _orderType = "현재가";
        public string OrderType { get => _orderType; set { _orderType = value; OnPropertyChanged(); } }

        // CHANGED: string에서 int로 변경
        public int SellStartHour { get; set; } = 9;
        public int SellStartMinute { get; set; } = 0;
        public int SellStartSecond { get; set; } = 0;

        public int SellEndHour { get; set; } = 15;
        public int SellEndMinute { get; set; } = 20;
        public int SellEndSecond { get; set; } = 0;

        private string _targetPriceType = "단순";
        public string TargetPriceType
        {
            get => _targetPriceType;
            set
            {
                _targetPriceType = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsSimpleTargetEnabled));
                OnPropertyChanged(nameof(IsTrailingStopEnabled));
                OnPropertyChanged(nameof(IsStopLossEnabled));
            }
        }
        public bool IsSimpleTargetEnabled => _targetPriceType == "단순";
        public bool IsTrailingStopEnabled => _targetPriceType == "트레일링";
        public bool IsStopLossEnabled => _targetPriceType == "스탑로스";

        private double _simpleTargetPrice;
        public double SimpleTargetPrice { get => _simpleTargetPrice; set { _simpleTargetPrice = value; OnPropertyChanged(); } }

        private double _trailingTriggerPrice;
        public double TrailingTriggerPrice { get => _trailingTriggerPrice; set { _trailingTriggerPrice = value; OnPropertyChanged(); } }

        private double _trailingStopRate;
        public double TrailingStopRate { get => _trailingStopRate; set { _trailingStopRate = value; OnPropertyChanged(); } }

        private double _stopLossTargetPrice;
        public double StopLossTargetPrice { get => _stopLossTargetPrice; set { _stopLossTargetPrice = value; OnPropertyChanged(); } }

        private double _stopLossPreservePrice;
        public double StopLossPreservePrice { get => _stopLossPreservePrice; set { _stopLossPreservePrice = value; OnPropertyChanged(); } }

        private string _liquidationMethod = "현재가";
        public string LiquidationMethod { get => _liquidationMethod; set { _liquidationMethod = value; OnPropertyChanged(); } }

        // CHANGED: string에서 int로 변경
        public int LiquidationHour { get; set; } = 15;
        public int LiquidationMinute { get; set; } = 20;
        public int LiquidationSecond { get; set; } = 0;

        private bool _useReboundCut = false;
        public bool UseReboundCut { get => _useReboundCut; set { _useReboundCut = value; OnPropertyChanged(); } }

        private double _reboundCutMinProfitRate;
        public double ReboundCutMinProfitRate { get => _reboundCutMinProfitRate; set { _reboundCutMinProfitRate = value; OnPropertyChanged(); } }

        private double _reboundCutRate;
        public double ReboundCutRate { get => _reboundCutRate; set { _reboundCutRate = value; OnPropertyChanged(); } }

        private double _reboundCutAmount;
        public double ReboundCutAmount { get => _reboundCutAmount; set { _reboundCutAmount = value; OnPropertyChanged(); } }
    }

    public class TradeSettings : ViewModelBase
    {
        private int _strategyNumber;
        public int StrategyNumber { get => _strategyNumber; set { _strategyNumber = value; OnPropertyChanged(); } }

        private BuySettings _buy = new BuySettings();
        public BuySettings Buy { get => _buy; set { _buy = value; OnPropertyChanged(); } }

        private SellSettings _sell = new SellSettings();
        public SellSettings Sell { get => _sell; set { _sell = value; OnPropertyChanged(); } }
    }
}
