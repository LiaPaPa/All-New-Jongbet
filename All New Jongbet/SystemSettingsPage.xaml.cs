using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace All_New_Jongbet
{
    public partial class SystemSettingsPage : Page
    {
        private MainWindow _mainWindow;
        public ObservableCollection<StrategyInfo> StrategyList { get; set; }

        public SystemSettingsPage(MainWindow mainWindow, ObservableCollection<AccountInfo> accounts, ObservableCollection<StrategyInfo> strategies)
        {
            InitializeComponent();
            this.DataContext = this;
            _mainWindow = mainWindow;
            AccountsDataGrid.ItemsSource = accounts;
            StrategyList = strategies;

            UpdateAllButtonStates();
        }

        private async void RegisterAccount_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "Key Files (*.txt)|*.txt",
                Title = "Select API Key Files"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                    string keysDirectory = Path.Combine(baseDirectory, "API Keys");
                    Directory.CreateDirectory(keysDirectory);

                    foreach (string filePath in openFileDialog.FileNames)
                    {
                        string fileName = Path.GetFileName(filePath);
                        string destPath = Path.Combine(keysDirectory, fileName);
                        File.Copy(filePath, destPath, true);
                    }

                    MessageBox.Show("Key files have been successfully registered.", "Success");

                    if (_mainWindow != null)
                    {
                        await _mainWindow.LoadApiKeysAndRequestTokensAsync();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"An error occurred while copying files: {ex.Message}", "Error");
                }
            }
        }

        private void AddStrategy_Click(object sender, RoutedEventArgs e)
        {
            if (AccountsDataGrid.SelectedItem is AccountInfo selectedAccount &&
                ConditionsDataGrid.SelectedItem is ConditionInfo selectedCondition)
            {
                // CHANGED: 새로운 전략 번호를 기존 목록의 최대값 + 1로 계산 (목록이 비어있으면 1)
                int nextStrategyNumber = StrategyList.Any() ? StrategyList.Max(s => s.StrategyNumber) + 1 : 1;

                var newStrategy = new StrategyInfo
                {
                    StrategyNumber = nextStrategyNumber, // 번호를 바로 부여
                    AccountNumber = selectedAccount.AccountNumber,
                    Token = selectedAccount.Token,
                    ConditionIndex = selectedCondition.Index,
                    ConditionName = selectedCondition.Name,
                    Status = "Inactive",
                    ConditionSearchResultList = new List<StockItem>()
                };
                StrategyList.Add(newStrategy);
                UpdateAllButtonStates();
            }
            else
            {
                MessageBox.Show("계좌와 조건식을 각각 하나씩 선택해야 합니다.", "알림");
            }
        }

        private void SaveStrategy_Click(object sender, RoutedEventArgs e)
        {
            // CHANGED: 번호 부여 로직을 제거하고, 상태와 날짜만 업데이트
            foreach (var strategy in StrategyList.Where(s => s.Status == "Inactive"))
            {
                strategy.CreationDate = DateTime.Now.ToString("yyyy-MM-dd");
                strategy.Status = "Active";
            }

            SaveStrategiesToFile();
            UpdateAllButtonStates();
        }

        private void DeleteStrategy_Click(object sender, RoutedEventArgs e)
        {
            if (StrategyMatchingDataGrid.SelectedItem is StrategyInfo selectedStrategy)
            {
                StrategyList.Remove(selectedStrategy);
                SaveStrategiesToFile();
                UpdateAllButtonStates();
            }
        }

        private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender == AccountsDataGrid && AccountsDataGrid.SelectedItem is AccountInfo selectedAccount)
            {
                ConditionsDataGrid.ItemsSource = selectedAccount.Conditions;
            }

            UpdateAllButtonStates();
        }

        private void UpdateAllButtonStates()
        {
            AddStrategyButton.IsEnabled = AccountsDataGrid.SelectedItem != null && ConditionsDataGrid.SelectedItem != null;

            // CHANGED
            SaveStrategyButton.IsEnabled = StrategyList.Any(s => s.Status == "Inactive");

            DeleteStrategyButton.IsEnabled = StrategyMatchingDataGrid.SelectedItem != null;
        }

        private void SaveStrategiesToFile()
        {
            try
            {
                string strategyFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Strategy");
                Directory.CreateDirectory(strategyFolderPath);
                string filePath = Path.Combine(strategyFolderPath, "strategies.json");

                string json = JsonConvert.SerializeObject(StrategyList.ToList(), Formatting.Indented);
                File.WriteAllText(filePath, json);
                Logger.Instance.Add("전략 목록을 파일에 저장했습니다.");
            }
            catch (Exception ex)
            {
                Logger.Instance.Add($"전략 파일 저장 중 오류 발생: {ex.Message}");
                MessageBox.Show($"전략 파일 저장 중 오류 발생: {ex.Message}", "Error");
            }
        }

        // NEW: 'Run Rate Limit Test' 버튼 클릭 이벤트 핸들러
        private void RunTest_Click(object sender, RoutedEventArgs e)
        {
            // MainWindow에 있는 테스트 실행 메서드를 호출
            //_mainWindow?.RunRateLimitTestAsync();
        }
    }
}