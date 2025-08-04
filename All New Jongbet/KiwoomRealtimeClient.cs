using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace All_New_Jongbet
{
    public class KiwoomRealtimeClient : IDisposable
    {
        private readonly ClientWebSocket _ws;
        private readonly string _accessToken;
        private CancellationTokenSource _cts;
        private const string WebSocketUrl = "wss://api.kiwoom.com:10000/api/dostk/websocket";

        public event Action<JObject> OnReceiveData;

        public KiwoomRealtimeClient(string accessToken)
        {
            _accessToken = accessToken;
            _ws = new ClientWebSocket();
        }

        public async Task<bool> ConnectAndLoginAsync()
        {
            _cts = new CancellationTokenSource();
            Logger.Instance.Add("실시간 WebSocket 서버 연결 시도...");
            try
            {
                await _ws.ConnectAsync(new Uri(WebSocketUrl), _cts.Token);
                _ = ReceiveLoopAsync(); // 백그라운드에서 메시지 수신 시작
                var loginPacket = new { trnm = "LOGIN", token = _accessToken };
                await SendMessageAsync(loginPacket);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Instance.Add($"실시간 WebSocket 연결 실패: {ex.Message}");
                return false;
            }
        }

        public Task RegisterRealtimeAsync(string groupNumber, string[] items, string[] types)
        {
            var regPacket = new
            {
                trnm = "REG",
                grp_no = groupNumber,
                refresh = "1",
                data = new[] { new { item = items, type = types } }
            };
            Logger.Instance.Add($"[WebSocket 전송] 실시간 등록 요청: grp_no={groupNumber}");
            return SendMessageAsync(regPacket);
        }

        private async Task ReceiveLoopAsync()
        {
            var buffer = new byte[4096];
            try
            {
                while (_ws.State == WebSocketState.Open)
                {
                    using (var ms = new MemoryStream())
                    {
                        WebSocketReceiveResult result;
                        do
                        {
                            result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                            ms.Write(buffer, 0, result.Count);
                        } while (!result.EndOfMessage);

                        if (ms.Length > 0)
                        {
                            var responseString = Encoding.UTF8.GetString(ms.ToArray());
                            var response = JObject.Parse(responseString);
                            string trnm = response["trnm"]?.ToString();

                            if (trnm == "PING")
                            {
                                await SendMessageAsync(response);
                            }
                            else
                            {
                                if (trnm != "LOGIN") // 로그인 응답은 로그에 남기지 않음
                                {
                                    Logger.Instance.Add($"[WebSocket 수신] {responseString}");
                                }
                                OnReceiveData?.Invoke(response);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (!_cts.IsCancellationRequested)
                {
                    Logger.Instance.Add($"[WebSocket 오류] {ex.Message}");
                }
            }
        }

        private Task SendMessageAsync(object message)
        {
            var jsonMessage = JsonConvert.SerializeObject(message);
            var messageBuffer = Encoding.UTF8.GetBytes(jsonMessage);
            return _ws.SendAsync(new ArraySegment<byte>(messageBuffer), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _ws?.Dispose();
        }
    }
}
