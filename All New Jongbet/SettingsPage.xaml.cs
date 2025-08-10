using System;
using System.Windows;
using System.Windows.Controls;
using All_New_Jongbet.Properties;

namespace All_New_Jongbet
{
    public partial class SettingsPage : UserControl
    {
        private readonly TelegramApiService _telegramService;

        public SettingsPage()
        {
            InitializeComponent();
            _telegramService = new TelegramApiService();
            LoadSettings();
        }

        private void LoadSettings()
        {
            EnableToggle.IsChecked = Settings.Default.IsTelegramNotificationEnabled;
            BotTokenTextBox.Text = Settings.Default.TelegramBotToken;
            ChatIdTextBox.Text = Settings.Default.TelegramChatId;
            TimeTextBox.Text = Settings.Default.TelegramNotificationTime;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Settings.Default.IsTelegramNotificationEnabled = EnableToggle.IsChecked ?? false;
                Settings.Default.TelegramBotToken = BotTokenTextBox.Text;
                Settings.Default.TelegramChatId = ChatIdTextBox.Text;
                Settings.Default.TelegramNotificationTime = TimeTextBox.Text;
                Settings.Default.Save();

                MessageBox.Show("설정이 저장되었습니다.", "저장 완료");

                var mainWindow = (MainWindow)Application.Current.MainWindow;
                mainWindow.SetupNotificationTimer();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"설정 저장 중 오류가 발생했습니다: {ex.Message}", "오류");
            }
        }

        private async void TestButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(BotTokenTextBox.Text) || string.IsNullOrEmpty(ChatIdTextBox.Text))
            {
                MessageBox.Show("봇 토큰과 채팅 ID를 모두 입력해주세요.", "알림");
                return;
            }

            string testMessage = $"[Jongbet 알림 테스트]\n\n현재 시간: {DateTime.Now}\n이 메시지가 보인다면 설정이 올바르게 완료된 것입니다.";
            bool success = await _telegramService.SendMessageAsync(BotTokenTextBox.Text, ChatIdTextBox.Text, testMessage);

            if (success)
            {
                MessageBox.Show("텔레그램으로 테스트 메시지를 보냈습니다. 메시지를 확인해주세요.", "전송 성공");
            }
            else
            {
                MessageBox.Show("메시지 전송에 실패했습니다. 봇 토큰과 채팅 ID를 다시 확인해주세요.", "전송 실패");
            }
        }
    }
}