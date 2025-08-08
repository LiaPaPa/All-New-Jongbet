// KiwoomRealtimeClient.cs 파일 전체를 아래 코드로 교체하세요.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace All_New_Jongbet
{
    public class KiwoomRealtimeClient : IDisposable
    {
        private readonly ClientWebSocket _ws;
        public ClientWebSocket WebSocket => _ws;
        private readonly string _accessToken;
        private CancellationTokenSource _cts;
        private const string WebSocketUrl = "wss://api.kiwoom.com:10000/api/dostk/websocket";

        // [수정] 가장 최근 요청 정보를 Tuple (설명, 그룹번호, 아이템로그) 형태로 저장
        private Tuple<string, string, string> _lastRequestDetails = null;
        private readonly object _lock = new object();

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
                _ = ReceiveLoopAsync();
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

        // [수정] 요청을 보내기 직전에 상세 정보를 Tuple에 기록합니다.
        public Task RegisterRealtimeAsync(string groupNumber, string[] items, string[] types)
        {
            var regPacket = new
            {
                trnm = "REG",
                grp_no = groupNumber,
                refresh = "1",
                data = new[] { new { item = items, type = types } }
            };

            string description = GetRealtimeTypeDescription(types.FirstOrDefault());
            string itemLog = items.Length > 1 ? $"{items.Length}개 종목" : (items.FirstOrDefault() ?? "");

            lock (_lock)
            {
                // 요청 상세 정보를 Tuple로 저장
                _lastRequestDetails = Tuple.Create(description, groupNumber, itemLog);
            }

            Logger.Instance.Add($"[{description}] 실시간 등록 요청 (grp_no={groupNumber}, items={itemLog})");

            return SendMessageAsync(regPacket);
        }

        private string GetRealtimeTypeDescription(string typeCode)
        {
            switch (typeCode)
            {
                case "00": return "주문체결";
                case "04": return "잔고정보";
                case "0B": return "주식체결";
                case "0D": return "주식호가잔량";
                default: return "기타 실시간 항목";
            }
        }

        // [수정] REG 응답을 받았을 때, 저장해둔 상세 정보를 사용하여 로그를 출력합니다.
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
                                if (trnm == "LOGIN")
                                {
                                    if (response["return_code"]?.ToString() == "0")
                                        Logger.Instance.Add("[WebSocket 수신] 실시간 서버 로그인 성공");
                                    else
                                        Logger.Instance.Add($"[WebSocket 수신] 실시간 서버 로그인 실패: {response["return_msg"]}");
                                }
                                else if (trnm == "REG")
                                {
                                    if (response["return_code"]?.ToString() == "0")
                                    {
                                        Tuple<string, string, string> requestDetails;
                                        lock (_lock)
                                        {
                                            requestDetails = _lastRequestDetails;
                                            _lastRequestDetails = null; // 사용 후 초기화
                                        }

                                        if (requestDetails != null)
                                        {
                                            string description = requestDetails.Item1;
                                            string grpNo = requestDetails.Item2;
                                            string itemsLog = requestDetails.Item3;
                                            Logger.Instance.Add($"[{description}] 실시간 등록 성공!! (grp_no={grpNo}, items={itemsLog})");
                                        }
                                        else
                                        {
                                            Logger.Instance.Add("[실시간 항목] 실시간 등록 성공!!"); // 비상시 대체 로그
                                        }
                                    }
                                    else
                                    {
                                        Logger.Instance.Add($"[WebSocket 수신] 실시간 항목 등록 실패: {response["return_msg"]}");
                                    }
                                }
                                else if (App.IsDebugMode)
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