// DailyAssetInfo.cs
using Newtonsoft.Json;
using System.ComponentModel;

namespace All_New_Jongbet
{
    public class DailyAssetInfo : INotifyPropertyChanged
    {
        [JsonProperty("dt")]
        public string Date { get; set; }

        private double _estimatedAsset;
        [JsonProperty("prsm_dpst_aset_amt")]
        public double EstimatedAsset
        {
            get => _estimatedAsset;
            set
            {
                _estimatedAsset = value;
                OnPropertyChanged(nameof(EstimatedAsset));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        public double ProfitRate { get; set; }
    }
}