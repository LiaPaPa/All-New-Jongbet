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

        // NEW: 마지막 API 요청 시간을 기록 (고정밀도 타이머 Ticks)
        public long LastRequestTimestamp { get; set; }

        // NEW: 계좌별 조건식 목록을 저장할 리스트
        public List<ConditionInfo> Conditions { get; set; }


        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

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

                    // 동기화 로직: DailyAssetList의 마지막(오늘) 데이터도 함께 업데이트
                    if (DailyAssetList != null && DailyAssetList.Any())
                    {
                        var todayData = DailyAssetList.Last();
                        // 날짜가 오늘 날짜와 같으면 자산 값 업데이트
                        if (todayData.Date == DateTime.Today.ToString("yyyyMMdd"))
                        {
                            todayData.EstimatedAsset = value;
                        }
                    }
                }
            }
        }

        public List<HoldingStock> HoldingStockList { get; set; }

        // NEW: 일별 자산 현황 리스트 (차트 데이터용)
        public ObservableCollection<DailyAssetInfo> DailyAssetList { get; set; }

        public AccountInfo()
        {
            DailyAssetList = new ObservableCollection<DailyAssetInfo>();
        }

        // [NEW] 보유 종목 변경 시 계좌 전체 요약 정보를 다시 계산하고 UI에 알리는 메서드
        public void RecalculateAndUpdateTotals()
        {
            if (HoldingStockList == null || !HoldingStockList.Any())
            {
                // 보유 종목이 없으면 평가 관련 금액은 0으로 처리
                TotalPurchaseAmount = 0;
                TotalEvaluationAmount = 0;
                TotalEvaluationProfitLoss = 0;
                TotalProfitRate = 0;
                // 추정예탁자산은 현금 잔고만 남게 됨 (여기서는 단순화하여 0으로 처리,
                // 실제로는 현금 잔액을 별도 관리해야 함)
            }
            else
            {
                TotalPurchaseAmount = HoldingStockList.Sum(s => s.PurchaseAmount);
                TotalEvaluationAmount = HoldingStockList.Sum(s => s.EvaluationAmount);
                TotalEvaluationProfitLoss = TotalEvaluationAmount - TotalPurchaseAmount;
                TotalProfitRate = (TotalPurchaseAmount > 0) ? (TotalEvaluationProfitLoss / TotalPurchaseAmount) * 100 : 0;
            }

            // 변경된 속성들을 UI에 알림
            OnPropertyChanged(nameof(TotalPurchaseAmount));
            OnPropertyChanged(nameof(TotalEvaluationAmount));
            OnPropertyChanged(nameof(TotalEvaluationProfitLoss));
            OnPropertyChanged(nameof(TotalProfitRate));
            OnPropertyChanged(nameof(EstimatedDepositAsset)); // 추정예탁자산도 함께 업데이트
        }
    }
}
