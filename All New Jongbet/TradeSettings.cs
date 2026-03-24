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

        protected string CleanComboBoxString(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            if (value.StartsWith("System.Windows.Controls.ComboBoxItem: "))
            {
                return value.Replace("System.Windows.Controls.ComboBoxItem: ", "").Trim();
            }
            return value;
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

        // 매수 시작시간 모드 설정
        private string _buyStartTimeMode = "절대시간";
        public string BuyStartTimeMode
        {
            get => _buyStartTimeMode;
            set { _buyStartTimeMode = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsBuyStartAbsoluteTime)); OnPropertyChanged(nameof(IsBuyStartRelativeTime)); }
        }
        public bool IsBuyStartAbsoluteTime
        {
            get => _buyStartTimeMode == "절대시간";
            set { if (value) BuyStartTimeMode = "절대시간"; }
        }
        public bool IsBuyStartRelativeTime
        {
            get => _buyStartTimeMode == "상대시간";
            set { if (value) BuyStartTimeMode = "상대시간"; }
        }

        // 상대 시간 설정
        private string _buyStartRelativeBase = "장시작";
        public string BuyStartRelativeBase { get => _buyStartRelativeBase; set { _buyStartRelativeBase = CleanComboBoxString(value); OnPropertyChanged(); } }

        private int _buyStartRelativeOffsetMinutes = 0;
        public int BuyStartRelativeOffsetMinutes { get => _buyStartRelativeOffsetMinutes; set { _buyStartRelativeOffsetMinutes = value; OnPropertyChanged(); } }

        // CHANGED: string에서 int로 변경 (텔레그램 연동 갱신을 위해 INPC 추가)
        private int _buyStartHour = 9;
        public int BuyStartHour { get => _buyStartHour; set { _buyStartHour = value; OnPropertyChanged(); } }

        private int _buyStartMinute = 0;
        public int BuyStartMinute { get => _buyStartMinute; set { _buyStartMinute = value; OnPropertyChanged(); } }

        private int _buyStartSecond = 0;
        public int BuyStartSecond { get => _buyStartSecond; set { _buyStartSecond = value; OnPropertyChanged(); } }

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

        // 매도 시작시간 모드 설정
        private string _sellStartTimeMode = "절대시간";
        public string SellStartTimeMode
        {
            get => _sellStartTimeMode;
            set { _sellStartTimeMode = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsSellStartAbsoluteTime)); OnPropertyChanged(nameof(IsSellStartRelativeTime)); }
        }
        public bool IsSellStartAbsoluteTime
        {
            get => _sellStartTimeMode == "절대시간";
            set { if (value) SellStartTimeMode = "절대시간"; }
        }
        public bool IsSellStartRelativeTime
        {
            get => _sellStartTimeMode == "상대시간";
            set { if (value) SellStartTimeMode = "상대시간"; }
        }

        private string _sellStartRelativeBase = "장시작";
        public string SellStartRelativeBase { get => _sellStartRelativeBase; set { _sellStartRelativeBase = CleanComboBoxString(value); OnPropertyChanged(); } }

        private int _sellStartRelativeOffsetMinutes = 0;
        public int SellStartRelativeOffsetMinutes { get => _sellStartRelativeOffsetMinutes; set { _sellStartRelativeOffsetMinutes = value; OnPropertyChanged(); } }

        // CHANGED: string에서 int로 변경 (텔레그램 연동 갱신을 위해 INPC 추가)
        private int _sellStartHour = 9;
        public int SellStartHour { get => _sellStartHour; set { _sellStartHour = value; OnPropertyChanged(); } }

        private int _sellStartMinute = 0;
        public int SellStartMinute { get => _sellStartMinute; set { _sellStartMinute = value; OnPropertyChanged(); } }

        private int _sellStartSecond = 0;
        public int SellStartSecond { get => _sellStartSecond; set { _sellStartSecond = value; OnPropertyChanged(); } }

        // 매도 종료시간 모드 설정
        private string _sellEndTimeMode = "절대시간";
        public string SellEndTimeMode
        {
            get => _sellEndTimeMode;
            set { _sellEndTimeMode = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsSellEndAbsoluteTime)); OnPropertyChanged(nameof(IsSellEndRelativeTime)); }
        }
        public bool IsSellEndAbsoluteTime
        {
            get => _sellEndTimeMode == "절대시간";
            set { if (value) SellEndTimeMode = "절대시간"; }
        }
        public bool IsSellEndRelativeTime
        {
            get => _sellEndTimeMode == "상대시간";
            set { if (value) SellEndTimeMode = "상대시간"; }
        }

        private string _sellEndRelativeBase = "장종료";
        public string SellEndRelativeBase { get => _sellEndRelativeBase; set { _sellEndRelativeBase = CleanComboBoxString(value); OnPropertyChanged(); } }

        private int _sellEndRelativeOffsetMinutes = -10;
        public int SellEndRelativeOffsetMinutes { get => _sellEndRelativeOffsetMinutes; set { _sellEndRelativeOffsetMinutes = value; OnPropertyChanged(); } }

        private int _sellEndHour = 15;
        public int SellEndHour { get => _sellEndHour; set { _sellEndHour = value; OnPropertyChanged(); } }

        private int _sellEndMinute = 20;
        public int SellEndMinute { get => _sellEndMinute; set { _sellEndMinute = value; OnPropertyChanged(); } }

        private int _sellEndSecond = 0;
        public int SellEndSecond { get => _sellEndSecond; set { _sellEndSecond = value; OnPropertyChanged(); } }

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

        // 청산시간 모드 설정
        private string _liquidationTimeMode = "절대시간";
        public string LiquidationTimeMode
        {
            get => _liquidationTimeMode;
            set { _liquidationTimeMode = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsLiquidationAbsoluteTime)); OnPropertyChanged(nameof(IsLiquidationRelativeTime)); }
        }
        public bool IsLiquidationAbsoluteTime
        {
            get => _liquidationTimeMode == "절대시간";
            set { if (value) LiquidationTimeMode = "절대시간"; }
        }
        public bool IsLiquidationRelativeTime
        {
            get => _liquidationTimeMode == "상대시간";
            set { if (value) LiquidationTimeMode = "상대시간"; }
        }

        private string _liquidationRelativeBase = "장종료";
        public string LiquidationRelativeBase { get => _liquidationRelativeBase; set { _liquidationRelativeBase = CleanComboBoxString(value); OnPropertyChanged(); } }

        private int _liquidationRelativeOffsetMinutes = -10;
        public int LiquidationRelativeOffsetMinutes { get => _liquidationRelativeOffsetMinutes; set { _liquidationRelativeOffsetMinutes = value; OnPropertyChanged(); } }

        // CHANGED: string에서 int로 변경 (텔레그램 연동 갱신을 위해 INPC 추가)
        private int _liquidationHour = 15;
        public int LiquidationHour { get => _liquidationHour; set { _liquidationHour = value; OnPropertyChanged(); } }

        private int _liquidationMinute = 20;
        public int LiquidationMinute { get => _liquidationMinute; set { _liquidationMinute = value; OnPropertyChanged(); } }

        private int _liquidationSecond = 0;
        public int LiquidationSecond { get => _liquidationSecond; set { _liquidationSecond = value; OnPropertyChanged(); } }

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
