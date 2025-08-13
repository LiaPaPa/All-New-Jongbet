using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace All_New_Jongbet
{
    public partial class HoldingsWindow : Window
    {
        // [CHANGED] ViewModel 대신 AccountInfo를 직접 바인딩하도록 변경
        public AccountInfo Account { get; set; }

        public HoldingsWindow(AccountInfo account)
        {
            InitializeComponent();

            // [CHANGED] DataContext를 AccountInfo 객체로 설정
            this.Account = account;
            this.DataContext = this.Account;

            this.Owner = Application.Current.MainWindow;
            TitleText.Text = $"[{account.AccountNumber}] Holdings";
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;
        private void MaximizeButton_Click(object sender, RoutedEventArgs e) => this.WindowState = this.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        private void CloseButton_Click(object sender, RoutedEventArgs e) => this.Close();
    }
}
