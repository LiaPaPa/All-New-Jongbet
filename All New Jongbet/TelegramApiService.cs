using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using All_New_Jongbet.Properties;
using Newtonsoft.Json.Linq;

namespace All_New_Jongbet
{
    public class TelegramApiService
    {
        private readonly HttpClient _httpClient = new HttpClient();
        private long _lastUpdateId = 0;

        public event Action<string, string> OnMessageReceived;

        public async Task SetCommandsAsync(string botToken)
        {
            var commands = new JArray(
                new JObject { { "command", "daily_report" }, { "description", "데일리 리포트 받기" } },
                new JObject { { "command", "account_status" }, { "description", "전체 계좌 현황 받기" } },
                new JObject { { "command", "asset_trend" }, { "description", "자산 추이 차트 받기" } },
                new JObject { { "command", "start_trading" }, { "description", "자동매매 시작" } },
                new JObject { { "command", "stop_trading" }, { "description", "자동매매 중지" } }
            );

            var content = new JObject { { "commands", commands } };
            var url = $"https://api.telegram.org/bot{botToken}/setMyCommands";

            try
            {
                var response = await _httpClient.PostAsync(url, new StringContent(content.ToString(), System.Text.Encoding.UTF8, "application/json"));
                if (response.IsSuccessStatusCode)
                {
                    Logger.Instance.Add("텔레그램 봇 명령어 메뉴 설정 성공.");
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    Logger.Instance.Add($"텔레그램 봇 명령어 메뉴 설정 실패: {error}");
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Add($"텔레그램 명령어 설정 중 예외 발생: {ex.Message}");
            }
        }

        public async Task StartReceivingMessagesAsync(string botToken, CancellationToken cancellationToken)
        {
            Logger.Instance.Add("텔레그램 메시지 수신을 시작합니다.");
            while (!cancellationToken.IsCancellationRequested)
            {
                var url = $"https://api.telegram.org/bot{botToken}/getUpdates?offset={_lastUpdateId + 1}&timeout=60";
                try
                {
                    var response = await _httpClient.GetAsync(url, cancellationToken);
                    if (response.IsSuccessStatusCode)
                    {
                        var responseString = await response.Content.ReadAsStringAsync();
                        var json = JObject.Parse(responseString);

                        if (json["ok"]?.Value<bool>() == true && json["result"] is JArray updates)
                        {
                            foreach (var update in updates)
                            {
                                _lastUpdateId = update["update_id"]?.Value<long>() ?? _lastUpdateId;
                                var message = update["message"];
                                if (message != null)
                                {
                                    var chatId = message["chat"]?["id"]?.ToString();
                                    var text = message["text"]?.ToString();
                                    OnMessageReceived?.Invoke(chatId, text);
                                }
                            }
                        }
                    }
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Instance.Add($"텔레그램 메시지 수신 중 예외 발생: {ex.Message}");
                    await Task.Delay(5000);
                }
            }
            Logger.Instance.Add("텔레그램 메시지 수신이 중지되었습니다.");
        }

        public async Task ClearPendingMessagesAsync(string botToken)
        {
            var url = $"https://api.telegram.org/bot{botToken}/getUpdates?offset=-1&timeout=1";
            try
            {
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var responseString = await response.Content.ReadAsStringAsync();
                    var json = JObject.Parse(responseString);

                    if (json["ok"]?.Value<bool>() == true && json["result"] is JArray updates && updates.HasValues)
                    {
                        _lastUpdateId = updates.Last["update_id"]?.Value<long>() ?? _lastUpdateId;
                        Logger.Instance.Add($"텔레그램 이전 메시지를 정리했습니다. (Last Update ID: {_lastUpdateId})");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Add($"텔레그램 이전 메시지 정리 중 예외 발생: {ex.Message}");
            }
        }

        public async Task<bool> SendMessageAsync(string botToken, string chatId, string message, bool showKeyboard = false)
        {
            var url = $"https://api.telegram.org/bot{botToken}/sendMessage";

            var messageData = new JObject
            {
                { "chat_id", chatId },
                { "text", message }
            };

            if (showKeyboard)
            {
                var keyboard = new JObject
                {
                    { "keyboard", new JArray(
                        new JArray("Daily Report", "Account Status", "Asset Trend"),
                        new JArray("Start", "Stop")
                      )
                    },
                    { "resize_keyboard", true }
                };
                messageData["reply_markup"] = keyboard;
            }

            var content = new StringContent(messageData.ToString(), System.Text.Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync(url, content);
                if (response.IsSuccessStatusCode)
                {
                    Logger.Instance.Add($"텔레그램 메시지 전송 성공 (Chat ID: {chatId})");
                    return true;
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    Logger.Instance.Add($"텔레그램 메시지 전송 실패: {error}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Add($"텔레그램 메시지 전송 중 예외 발생: {ex.Message}");
                return false;
            }
        }

        // [NEW] 거래 체결 알림 전용 메서드
        public async Task<bool> SendTradeNotificationAsync(string message)
        {
            if (!Settings.Default.IsTelegramNotificationEnabled) return false;

            string botToken = Settings.Default.TelegramBotToken;
            string chatId = Settings.Default.TelegramChatId;

            if (string.IsNullOrEmpty(botToken) || string.IsNullOrEmpty(chatId) || botToken == "BotToken" || chatId == "BotChatID")
            {
                Logger.Instance.Add("[텔레그램] 봇 토큰 또는 채팅 ID가 설정되지 않아 체결 알림을 보낼 수 없습니다.");
                return false;
            }

            return await SendMessageAsync(botToken, chatId, message);
        }

        public async Task<bool> SendPhotoAsync(string botToken, string chatId, string imagePath, string caption = "")
        {
            var url = $"https://api.telegram.org/bot{botToken}/sendPhoto";

            using (var multipartFormContent = new MultipartFormDataContent())
            {
                var fileStreamContent = new StreamContent(File.OpenRead(imagePath));
                fileStreamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");

                multipartFormContent.Add(fileStreamContent, name: "photo", fileName: Path.GetFileName(imagePath));
                multipartFormContent.Add(new StringContent(chatId), "chat_id");
                if (!string.IsNullOrEmpty(caption))
                {
                    multipartFormContent.Add(new StringContent(caption), "caption");
                }

                try
                {
                    var response = await _httpClient.PostAsync(url, multipartFormContent);
                    if (response.IsSuccessStatusCode)
                    {
                        Logger.Instance.Add($"텔레그램 사진 전송 성공 (Chat ID: {chatId})");
                        return true;
                    }
                    else
                    {
                        var error = await response.Content.ReadAsStringAsync();
                        Logger.Instance.Add($"텔레그램 사진 전송 실패: {error}");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Instance.Add($"텔레그램 사진 전송 중 예외 발생: {ex.Message}");
                    return false;
                }
            }
        }
    }
}
