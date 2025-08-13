// StrategyInfo.cs 파일 전체를 아래 코드로 교체하세요.

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace All_New_Jongbet
{
    public class StrategyInfo : INotifyPropertyChanged
    {
        private int _strategyNumber;
        public int StrategyNumber
        {
            get => _strategyNumber;
            set { _strategyNumber = value; OnPropertyChanged(nameof(StrategyNumber)); }
        }

        public string AccountNumber { get; set; }
        // [수정] 만료된 토큰을 저장하지 않도록 Token 속성 제거
        // public string Token { get; set; } 
        public string ConditionIndex { get; set; }
        public string ConditionName { get; set; }

        private string _creationDate;
        public string CreationDate
        {
            get => _creationDate;
            set { _creationDate = value; OnPropertyChanged(nameof(CreationDate)); }
        }

        private string _status;
        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(nameof(Status)); }
        }

        public DateTime LastExecutionDate { get; set; } = DateTime.MinValue;

        [JsonIgnore]
        public List<SearchedStock> SearchedStockList { get; set; }

        [JsonIgnore]
        public TradeSettings TradeSettings { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}