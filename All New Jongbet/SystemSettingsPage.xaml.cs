// SystemSettingsPage.xaml.cs 파일 전체를 아래 코드로 교체하세요.

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

        // [수정] 'Add Strategy' 버튼 클릭 이벤트 핸들러
        private void AddStrategy_Click(object sender, RoutedEventArgs e)
        {
            if (AccountsDataGrid.SelectedItem is AccountInfo selectedAccount &&
                ConditionsDataGrid.SelectedItem is ConditionInfo selectedCondition)
            {
                // 중복되지 않는 새로운 전략 번호 생성 (리스트가 비어있으면 1, 아니면 최대값+1)
                int nextStrategyNumber = StrategyList.Any() ? StrategyList.Max(s => s.StrategyNumber) + 1 : 1;

                var newStrategy = new StrategyInfo
                {
                    StrategyNumber = nextStrategyNumber,
                    AccountNumber = selectedAccount.AccountNumber,
                    ConditionIndex = selectedCondition.Index,
                    ConditionName = selectedCondition.Name,
                    Status = "Inactive", // 초기 상태는 'Inactive'
                    CreationDate = string.Empty,
                    LastExecutionDate = DateTime.MinValue
                };

                StrategyList.Add(newStrategy);
                Logger.Instance.Add($"{newStrategy.StrategyNumber}번 신규 전략을 리스트에 추가했습니다. (상태: Inactive)");
                UpdateAllButtonStates();
            }
            else
            {
                MessageBox.Show("계좌와 조건식을 각각 하나씩 선택해야 합니다.", "알림");
            }
        }

        // [수정] 'Save Strategy' 버튼 클릭 이벤트 핸들러
        private void SaveStrategy_Click(object sender, RoutedEventArgs e)
        {
            var inactiveStrategies = StrategyList.Where(s => s.Status == "Inactive").ToList();
            if (!inactiveStrategies.Any())
            {
                MessageBox.Show("저장할 신규(Inactive) 전략이 없습니다.", "알림");
                return;
            }

            foreach (var strategy in inactiveStrategies)
            {
                strategy.CreationDate = DateTime.Now.ToString("yyyy-MM-dd");
                strategy.Status = "Active"; // 상태를 'Active'로 변경
                Logger.Instance.Add($"{strategy.StrategyNumber}번 전략의 상태를 Active로 변경하고 저장합니다.");
            }

            // 수정된 전체 전략 리스트를 파일에 저장
            StrategyRepository.Save(StrategyList);
            MessageBox.Show("신규 전략이 성공적으로 저장 및 활성화되었습니다.", "저장 완료");

            UpdateAllButtonStates();
        }

        private void DeleteStrategy_Click(object sender, RoutedEventArgs e)
        {
            if (StrategyMatchingDataGrid.SelectedItem is StrategyInfo selectedStrategy)
            {
                StrategyList.Remove(selectedStrategy);
                StrategyRepository.Save(StrategyList); // 삭제 후에도 파일 저장
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
            SaveStrategyButton.IsEnabled = StrategyList.Any(s => s.Status == "Inactive");
            DeleteStrategyButton.IsEnabled = StrategyMatchingDataGrid.SelectedItem != null;
        }

        private void RunTest_Click(object sender, RoutedEventArgs e)
        {
            // _mainWindow?.RunRateLimitTestAsync();
        }
    }
}