using System;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace All_New_Jongbet
{
    public partial class LogsPage : Page
    {
        public ObservableCollection<OrderLogItem> OrderLogList { get; set; }

        public LogsPage(ObservableCollection<OrderLogItem> orderLogs)
        {
            InitializeComponent();
            this.DataContext = this;
            OrderLogList = orderLogs;
            Logger.Instance.LogAdded += OnLogAdded;
        }

        private void OnLogAdded(string logMessage)
        {
            Dispatcher.Invoke(() =>
            {
                // CHANGED: AppendText를 사용하여 텍스트를 맨 뒤에 추가하는 대신,
                // 새로 들어온 로그를 기존 텍스트 앞에 추가합니다.
                LogTextBox.Text = logMessage + Environment.NewLine + LogTextBox.Text;

                // REMOVED: ScrollToEnd()는 더 이상 필요 없으므로 제거합니다.
            });
        }
    }
}