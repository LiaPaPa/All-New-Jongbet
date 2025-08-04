using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Linq; // Linq 사용
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

    }
}
