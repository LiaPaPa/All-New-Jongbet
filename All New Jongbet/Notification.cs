// Notification.cs
using System.ComponentModel;

namespace All_New_Jongbet
{
    public class Notification : INotifyPropertyChanged
    {
        private string _message;
        public string Message
        {
            get => _message;
            set { _message = value; OnPropertyChanged("Message"); }
        }

        private string _styleKey;
        public string StyleKey
        {
            get => _styleKey;
            set { _styleKey = value; OnPropertyChanged("StyleKey"); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}