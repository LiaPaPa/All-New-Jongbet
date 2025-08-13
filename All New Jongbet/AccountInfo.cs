using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Collections.ObjectModel; // ObservableCollection 사용

namespace All_New_Jongbet
{
    public class AccountInfo : INotifyPropertyChanged
    {
        public string AccountNumber { get; set; }
        public string AppKey { get; set; }
        public string SecretKey { get; set; }

        private string _token;
        public string Token
        {
            get { return _token; }
            set
            {
                _token = value;
                OnPropertyChanged("Token");
            }
        }

        private string _tokenStatus;
        public string TokenStatus
        {
            get { return _tokenStatus; }
            set
            {
                _tokenStatus = value;
                OnPropertyChanged("TokenStatus");
            }
        }

        public long LastRequestTimestamp { get; set; }

        public List<ConditionInfo> Conditions { get; set; }


        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // [NEW] 실시간 자산 계산을 위해 현금 잔고 속성 추가
        public double CashBalance { get; set; }
        public double TotalPurchaseAmount { get; set; }
        public double TotalEvaluationAmount { get; set; }
        public double TotalEvaluationProfitLoss { get; set; }
        public double TotalProfitRate { get; set; }


        private double _estimatedDepositAsset;
        public double EstimatedDepositAsset
        {
            get => _estimatedDepositAsset;
            set
            {
                if (_estimatedDepositAsset != value)
                {
                    _estimatedDepositAsset = value;
                    OnPropertyChanged(nameof(EstimatedDepositAsset));

                    if (DailyAssetList != null && DailyAssetList.Any())
                    {
                        var todayData = DailyAssetList.Last();
                        if (todayData.Date == DateTime.Today.ToString("yyyyMMdd"))
                        {
                            todayData.EstimatedAsset = value;
                        }
                    }
                }
            }
        }

        // [CHANGED] List -> ObservableCollection으로 변경하여 UI 자동 업데이트 지원
        public ObservableCollection<HoldingStock> HoldingStockList { get; set; }

        public ObservableCollection<DailyAssetInfo> DailyAssetList { get; set; }

        public AccountInfo()
        {
            DailyAssetList = new ObservableCollection<DailyAssetInfo>();
            // [NEW] 생성자에서 초기화
            HoldingStockList = new ObservableCollection<HoldingStock>();
        }

        // [CHANGED] 실시간 시세 변동에 따른 계좌 정보 전체를 재계산하는 메서드
        public void RecalculateAndUpdateTotals()
        {
            if (HoldingStockList == null || !HoldingStockList.Any())
            {
                TotalPurchaseAmount = 0;
                TotalEvaluationAmount = 0;
            }
            else
            {
                TotalPurchaseAmount = HoldingStockList.Sum(s => s.PurchaseAmount);
                TotalEvaluationAmount = HoldingStockList.Sum(s => s.EvaluationAmount);
            }

            TotalEvaluationProfitLoss = TotalEvaluationAmount - TotalPurchaseAmount;
            TotalProfitRate = (TotalPurchaseAmount > 0) ? (TotalEvaluationProfitLoss / TotalPurchaseAmount) * 100 : 0;

            // [NEW] 현금 + 주식평가액으로 실시간 추정예탁자산 재계산
            EstimatedDepositAsset = CashBalance + TotalEvaluationAmount;

            // [DEBUG LOG]
            //Logger.Instance.Add($"[DEBUG Recalculate] Account: {AccountNumber}, Cash: {CashBalance:N0}, StockValue: {TotalEvaluationAmount:N0}, TotalAsset: {EstimatedDepositAsset:N0}");

            OnPropertyChanged(nameof(TotalPurchaseAmount));
            OnPropertyChanged(nameof(TotalEvaluationAmount));
            OnPropertyChanged(nameof(TotalEvaluationProfitLoss));
            OnPropertyChanged(nameof(TotalProfitRate));
            // OnPropertyChanged(nameof(EstimatedDepositAsset)); // EstimatedDepositAsset의 set 접근자에서 이미 호출됨
        }
    }
}
