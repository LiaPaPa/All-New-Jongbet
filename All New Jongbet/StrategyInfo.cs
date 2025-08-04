// StrategyInfo.cs
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
        public string Token { get; set; }
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

        public List<StockItem> ConditionSearchResultList { get; set; }

        public List<SearchedStock> SearchedStockList { get; set; }


        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}