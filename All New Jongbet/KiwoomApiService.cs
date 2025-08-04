using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace All_New_Jongbet
{
    public class KiwoomApiService
    {
        private static readonly HttpClient httpClient = new HttpClient();
        private const string BaseUrl = "https://api.kiwoom.com";
        private const string WebSocketUrl = "wss://api.kiwoom.com:10000/api/dostk/websocket";

        // --- HTTP 요청 메서드 ---

        public async Task<(string Token, bool IsSuccess)> GetAccessTokenAsync(string appKey, string secretKey)
        {
            Logger.Instance.Add("[TR 요청] 접근토큰 발급 (au10001)");
            var requestUrl = $"{BaseUrl}/oauth2/token";
            var requestBody = new { grant_type = "client_credentials", appkey = appKey, secretkey = secretKey };
            var jsonBody = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            try
            {
                HttpResponseMessage response = await httpClient.PostAsync(requestUrl, content);
                var responseString = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode)
                {
                    dynamic result = JsonConvert.DeserializeObject(responseString);
                    Logger.Instance.Add("[TR 응답] 접근토큰 발급 성공");
                    return (result.token, true);
                }
                else
                {
                    Logger.Instance.Add($"[TR 응답] 접근토큰 발급 실패: {response.StatusCode} - {responseString}");
                    return (null, false);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Add($"[TR 오류] 접근토큰 발급 중 예외 발생: {ex.Message}");
                return (null, false);
            }
        }

        public async Task<bool> GetAccountBalanceAsync(AccountInfo account)
        {
            var requestBody = new { qry_tp = "1", dmst_stex_tp = "KRX" };
            var response = await SendHttpRequestAsync(account, "kt00018", "/api/dostk/acnt", requestBody);
            if (response.IsSuccess && response.JsonData != null)
            {
                try
                {
                    account.TotalPurchaseAmount = (double)response.JsonData.tot_pur_amt;
                    account.TotalEvaluationAmount = (double)response.JsonData.tot_evlt_amt;
                    account.TotalEvaluationProfitLoss = (double)response.JsonData.tot_evlt_pl;
                    account.TotalProfitRate = (double)response.JsonData.tot_prft_rt;
                    account.EstimatedDepositAsset = (double)response.JsonData.prsm_dpst_aset_amt;
                    account.HoldingStockList = response.JsonData.acnt_evlt_remn_indv_tot != null ? ((JArray)response.JsonData.acnt_evlt_remn_indv_tot).ToObject<List<HoldingStock>>() : new List<HoldingStock>();
                    Logger.Instance.Add($"[TR 응답] 계좌평가잔고 조회 성공. 보유종목: {account.HoldingStockList.Count}개");
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Instance.Add($"[TR 오류] 계좌평가잔고 데이터 파싱 중 오류: {ex.Message}");
                }
            }
            return false;
        }

        public async Task<List<DailyAssetInfo>> GetDailyAssetHistoryAsync(AccountInfo account, string startDate, string endDate)
        {
            var allHistory = new List<DailyAssetInfo>();
            string contYn = "N";
            string nextKey = "";
            do
            {
                var requestBody = new { start_dt = startDate, end_dt = endDate };
                var response = await SendHttpRequestAsync(account, "kt00002", "/api/dostk/acnt", requestBody, contYn, nextKey);
                if (response.IsSuccess && response.JsonData != null)
                {
                    if (response.JsonData.daly_prsm_dpst_aset_amt_prst != null)
                    {
                        allHistory.AddRange(((JArray)response.JsonData.daly_prsm_dpst_aset_amt_prst).ToObject<List<DailyAssetInfo>>());
                    }
                    contYn = response.ContYn;
                    nextKey = response.NextKey;
                }
                else
                {
                    contYn = "N";
                }
            } while (contYn == "Y");
            Logger.Instance.Add($"[TR 응답 완료] 일별추정예탁자산 조회 성공. 총 {allHistory.Count}일치 데이터 수신.");
            return allHistory.OrderBy(x => x.Date).ToList();
        }

        public async Task<List<OrderHistoryItem>> GetOrderHistoryAsync(AccountInfo account)
        {
            var allOrders = new List<OrderHistoryItem>();
            string contYn = "N";
            string nextKey = "";
            string today = DateTime.Today.ToString("yyyyMMdd");
            Logger.Instance.Add($"[TR 요청 시작] 주문체결내역 조회 (kt00007) - 계좌: {account.AccountNumber}");
            do
            {
                var requestBody = new { ord_dt = today, qry_tp = "1", stk_bond_tp = "1", sell_tp = "0", stk_cd = "", fr_ord_no = "", dmst_stex_tp = "%" };
                var response = await SendHttpRequestAsync(account, "kt00007", "/api/dostk/acnt", requestBody, contYn, nextKey);
                if (response.IsSuccess && response.JsonData != null)
                {
                    if (response.JsonData.ord_dtl != null)
                    {
                        allOrders.AddRange(((JArray)response.JsonData.ord_dtl).ToObject<List<OrderHistoryItem>>());
                    }
                    contYn = response.ContYn;
                    nextKey = response.NextKey;
                }
                else
                {
                    contYn = "N";
                }
            } while (contYn == "Y");
            Logger.Instance.Add($"[TR 응답 완료] 주문체결내역 조회 성공. 총 {allOrders.Count}건 수신.");
            return allOrders;
        }

        public async Task<StockBasicInfo> GetStockBasicInfoAsync(AccountInfo account, string stockCode)
        {
            var requestBody = new { stk_cd = stockCode };
            var response = await SendHttpRequestAsync(account, "ka10001", "/api/dostk/stk", requestBody);
            if (response.IsSuccess && response.JsonData != null)
            {
                return response.JsonData.ToObject<StockBasicInfo>();
            }
            return null;
        }

        public async Task<List<DailyChartData>> GetDailyChartAsync(AccountInfo account, string stockCode)
        {
            var requestBody = new { biz_dt = DateTime.Today.ToString("yyyyMMdd"), stk_cd = stockCode };
            var response = await SendHttpRequestAsync(account, "ka10081", "/api/dostk/chart", requestBody);
            if (response.IsSuccess && response.JsonData != null && response.JsonData.chart_data != null)
            {
                return ((JArray)response.JsonData.chart_data).ToObject<List<DailyChartData>>();
            }
            return new List<DailyChartData>();
        }

        // --- 웹소켓 요청 메서드 ---

        // ✅ NEW: 누락되었던 GetConditionListAsync 메서드를 다시 추가했습니다.
        public async Task<List<ConditionInfo>> GetConditionListAsync(AccountInfo account)
        {
            Logger.Instance.Add($"[TR 요청] 조건검색 목록 조회 (ka10171) - 계좌: {account.AccountNumber}");
            using (var ws = new ClientWebSocket())
            {
                try
                {
                    ws.Options.SetRequestHeader("authorization", $"Bearer {account.Token}");
                    await ws.ConnectAsync(new Uri(WebSocketUrl), CancellationToken.None);
                    var loginPacket = new { trnm = "LOGIN", token = account.Token };
                    await SendWsMessageAsync(ws, loginPacket);
                    var loginResponse = await ReceiveWsMessageAsync(ws);
                    if (loginResponse["return_code"]?.ToString() != "0")
                    {
                        Logger.Instance.Add($"[WebSocket 오류] 조건검색 로그인 실패: {loginResponse["return_msg"]}");
                        return new List<ConditionInfo>();
                    }

                    var requestPacket = new { trnm = "CNSRLST" };
                    await SendWsMessageAsync(ws, requestPacket);
                    var response = await ReceiveWsMessageAsync(ws);
                    if (response["return_code"]?.ToString() == "0")
                    {
                        var conditions = new List<ConditionInfo>();
                        JArray dataArray = (JArray)response["data"];
                        foreach (var item in dataArray)
                        {
                            conditions.Add(new ConditionInfo { Index = item[0].ToString(), Name = item[1].ToString() });
                        }
                        return conditions;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Instance.Add($"[WebSocket 오류] 조건검색 목록 조회 중 오류: {ex.Message}");
                }
                finally
                {
                    if (ws.State == WebSocketState.Open) await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                }
            }
            return new List<ConditionInfo>();
        }

        public async Task<List<SearchedStock>> GetConditionSearchResultAsync(AccountInfo account, string conditionName, string conditionIndex)
        {
            Logger.Instance.Add($"[TR 요청] 조건검색 결과 요청 (ka10172) - 조건: {conditionName}");
            using (var ws = new ClientWebSocket())
            {
                try
                {
                    ws.Options.SetRequestHeader("authorization", $"Bearer {account.Token}");
                    await ws.ConnectAsync(new Uri(WebSocketUrl), CancellationToken.None);
                    var loginPacket = new { trnm = "LOGIN", token = account.Token };
                    await SendWsMessageAsync(ws, loginPacket);
                    await ReceiveWsMessageAsync(ws);
                    var requestPacket = new { trnm = "CNSSTK", cnsr_name = conditionName, cnsr_idx = conditionIndex };
                    await SendWsMessageAsync(ws, requestPacket);
                    var response = await ReceiveWsMessageAsync(ws);
                    if (response["return_code"]?.ToString() == "0")
                    {
                        var stocks = new List<SearchedStock>();
                        JArray dataArray = (JArray)response["data"];
                        foreach (var item in dataArray)
                        {
                            stocks.Add(new SearchedStock(item[0].ToString(), item[1].ToString()));
                        }
                        return stocks;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Instance.Add($"[WebSocket 오류] 조건검색 결과 요청 중 오류: {ex.Message}");
                }
                finally
                {
                    if (ws.State == WebSocketState.Open) await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                }
            }
            return new List<SearchedStock>();
        }

        // --- 헬퍼 메서드 ---

        private async Task<(bool IsSuccess, dynamic JsonData, string ContYn, string NextKey)> SendHttpRequestAsync(AccountInfo account, string apiId, string endpoint, object requestBody, string contYn = "N", string nextKey = "")
        {
            Logger.Instance.Add($"[TR 요청] {apiId} - 계좌: {account.AccountNumber}");
            var requestUrl = $"{BaseUrl}{endpoint}";
            var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUrl) { Content = content };
            requestMessage.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", account.Token);
            requestMessage.Headers.Add("api-id", apiId);
            if (contYn == "Y" || !string.IsNullOrEmpty(nextKey))
            {
                requestMessage.Headers.Add("cont-yn", "Y");
                requestMessage.Headers.Add("next-key", nextKey);
            }
            try
            {
                var response = await httpClient.SendAsync(requestMessage);
                var responseString = await response.Content.ReadAsStringAsync();
                dynamic result = JsonConvert.DeserializeObject(responseString);
                if (response.IsSuccessStatusCode && result.return_code.ToString() == "0")
                {
                    string cont = response.Headers.TryGetValues("cont-yn", out var c) ? c.FirstOrDefault() : "N";
                    string next = response.Headers.TryGetValues("next-key", out var n) ? n.FirstOrDefault() : "";
                    return (true, result, cont, next);
                }
                Logger.Instance.Add($"[TR 응답] {apiId} 조회 실패: {result.return_msg}");
            }
            catch (Exception ex)
            {
                Logger.Instance.Add($"[TR 오류] {apiId} 조회 중 예외 발생: {ex.Message}");
            }
            return (false, null, "N", "");
        }

        private async Task<JObject> ReceiveWsMessageAsync(ClientWebSocket ws)
        {
            using (var ms = new MemoryStream())
            {
                var buffer = new byte[8192];
                WebSocketReceiveResult result;
                do
                {
                    result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);
                var responseString = Encoding.UTF8.GetString(ms.ToArray());
                Logger.Instance.Add($"[WebSocket 수신] {responseString}");
                return JObject.Parse(responseString);
            }
        }

        private Task SendWsMessageAsync(ClientWebSocket ws, object message)
        {
            var jsonMessage = JsonConvert.SerializeObject(message);
            Logger.Instance.Add($"[WebSocket 전송] {jsonMessage}");
            var messageBuffer = Encoding.UTF8.GetBytes(jsonMessage);
            return ws.SendAsync(new ArraySegment<byte>(messageBuffer), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}
