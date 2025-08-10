using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace All_New_Jongbet
{
    public partial class HoldingsWindow : Window
    {
        public ObservableCollection<HoldingStockViewModel> Holdings { get; set; }

        public HoldingsWindow(AccountInfo account)
        {
            InitializeComponent();
            this.DataContext = this;
            this.Owner = Application.Current.MainWindow;

            TitleText.Text = $"[{account.AccountNumber}] Holdings";

            Holdings = new ObservableCollection<HoldingStockViewModel>();
            if (account.HoldingStockList != null)
            {
                foreach (var stock in account.HoldingStockList)
                {
                    // [CHANGED] HoldingStock의 모든 속성을 HoldingStockViewModel로 복사
                    Holdings.Add(new HoldingStockViewModel
                    {
                        StockCode = stock.StockCode,
                        StockName = stock.StockName,
                        CurrentPrice = stock.CurrentPrice,
                        PreviousClosePrice = stock.PreviousClosePrice,
                        PurchasePrice = stock.PurchasePrice, // 매수가 추가
                        ProfitRate = stock.ProfitRate,       // 수익률 추가
                    });
                }
            }
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