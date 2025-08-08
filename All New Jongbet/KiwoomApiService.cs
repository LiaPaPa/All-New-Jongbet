// KiwoomApiService.cs 파일 전체를 아래 코드로 교체하세요.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
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

        public Task SendWsMessageAsync(ClientWebSocket ws, object message, bool log = true)
        {
            if (ws == null || ws.State != WebSocketState.Open)
            {
                Logger.Instance.Add("[WebSocket 오류] 소켓이 연결되지 않은 상태에서 메시지를 보내려 했습니다.");
                return Task.CompletedTask;
            }

            var jsonMessage = JsonConvert.SerializeObject(message);
            if (log && App.IsDebugMode)
            {
                Logger.Instance.Add($"[WebSocket 전송 Body] {jsonMessage}");
            }
            var messageBuffer = Encoding.UTF8.GetBytes(jsonMessage);
            return ws.SendAsync(new ArraySegment<byte>(messageBuffer), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        public async Task<(string Token, bool IsSuccess)> GetAccessTokenAsync(string appKey, string secretKey)
        {
            string jsonBody = JsonConvert.SerializeObject(new { grant_type = "client_credentials", appkey = appKey, secretkey = secretKey });
            var requestUrl = $"{BaseUrl}/oauth2/token";
            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            try
            {
                Logger.Instance.Add("[TR 요청 시작] 접근토큰 발급 (au10001)");
                HttpResponseMessage response = await httpClient.PostAsync(requestUrl, content);
                var responseString = await response.Content.ReadAsStringAsync();
                Logger.Instance.Add("[TR 수신 완료] 접근토큰 발급 (au10001)");

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
                    account.TotalPurchaseAmount = (double)response.JsonData["tot_pur_amt"];
                    account.TotalEvaluationAmount = (double)response.JsonData["tot_evlt_amt"];
                    account.TotalEvaluationProfitLoss = (double)response.JsonData["tot_evlt_pl"];
                    account.TotalProfitRate = (double)response.JsonData["tot_prft_rt"];
                    account.EstimatedDepositAsset = (double)response.JsonData["prsm_dpst_aset_amt"];

                    var holdings = new List<HoldingStock>();
                    if (response.JsonData["acnt_evlt_remn_indv_tot"] is JArray holdingsJArray)
                    {
                        foreach (var item in holdingsJArray)
                        {
                            var stock = new HoldingStock
                            {
                                StockCode = item["stk_cd"]?.ToString(),
                                StockName = item["stk_nm"]?.ToString(),
                                EvaluationProfitLoss = double.TryParse(item["evltv_prft"]?.ToString(), out var evltvPrft) ? evltvPrft : 0,
                                ProfitRate = double.TryParse(item["prft_rt"]?.ToString(), out var prftRt) ? prftRt : 0,
                                PurchasePrice = double.TryParse(item["pur_pric"]?.ToString(), out var purPric) ? purPric : 0,
                                PreviousClosePrice = double.TryParse(item["pred_close_pric"]?.ToString(), out var predClosePric) ? predClosePric : 0,
                                HoldingQuantity = int.TryParse(item["rmnd_qty"]?.ToString(), out var rmndQty) ? rmndQty : 0,
                                TradableQuantity = int.TryParse(item["trde_able_qty"]?.ToString(), out var trdeAbleQty) ? trdeAbleQty : 0,
                                CurrentPrice = double.TryParse(item["cur_prc"]?.ToString(), out var curPrc) ? curPrc : 0,
                                PurchaseAmount = double.TryParse(item["pur_amt"]?.ToString(), out var purAmt) ? purAmt : 0,
                                EvaluationAmount = double.TryParse(item["evlt_amt"]?.ToString(), out var evltAmt) ? evltAmt : 0
                            };
                            holdings.Add(stock);
                        }
                    }
                    account.HoldingStockList = holdings;
                    Logger.Instance.Add($"[TR 응답] {account.AccountNumber} 계좌평가잔고 조회 성공. 보유종목: {account.HoldingStockList.Count}개");
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
                if (response.IsSuccess && response.JsonData?["daly_prsm_dpst_aset_amt_prst"] is JArray historyArray)
                {
                    allHistory.AddRange(historyArray.ToObject<List<DailyAssetInfo>>());
                    contYn = response.ContYn;
                    nextKey = response.NextKey;
                }
                else
                {
                    contYn = "N";
                }
            } while (contYn == "Y");
            Logger.Instance.Add($"[TR 응답 완료] {account.AccountNumber} 계좌 일별추정예탁자산 조회 성공. 총 {allHistory.Count}일치 데이터 수신.");
            return allHistory.OrderBy(x => x.Date).ToList();
        }

        public async Task<List<OrderHistoryItem>> GetOrderHistoryAsync(AccountInfo account)
        {
            var allOrders = new List<OrderHistoryItem>();
            string contYn = "N";
            string nextKey = "";
            string today = DateTime.Today.ToString("yyyyMMdd");
            do
            {
                var requestBody = new { ord_dt = today, qry_tp = "1", stk_bond_tp = "1", sell_tp = "0", stk_cd = "", fr_ord_no = "", dmst_stex_tp = "%" };
                var response = await SendHttpRequestAsync(account, "kt00007", "/api/dostk/acnt", requestBody, contYn, nextKey);
                if (response.IsSuccess && response.JsonData?["acnt_ord_cntr_prps_dtl"] is JArray ordersArray)
                {
                    var receivedOrders = ordersArray.ToObject<List<OrderHistoryItem>>();
                    if (receivedOrders != null)
                    {
                        for (int i = 0; i < receivedOrders.Count; i++)
                        {
                            int.TryParse(ordersArray[i]["ord_remnq"]?.ToString(), out int unfilledQty);
                            receivedOrders[i].UnfilledQuantity = unfilledQty;
                        }
                        allOrders.AddRange(receivedOrders);
                    }
                    contYn = response.ContYn;
                    nextKey = response.NextKey;
                }
                else
                {
                    contYn = "N";
                }
            } while (contYn == "Y");
            Logger.Instance.Add($"[TR 응답 완료] {account.AccountNumber} 계좌 주문체결내역 조회 성공. 총 {allOrders.Count}건 수신.");
            return allOrders;
        }

        public async Task<List<OrderHistoryItem>> GetUnfilledOrdersAsync(AccountInfo account)
        {
            var requestBody = new { all_stk_tp = "0", trde_tp = "0", stk_cd = "", stex_tp = "0" };
            var response = await SendHttpRequestAsync(account, "ka10075", "/api/dostk/acnt", requestBody);

            if (response.IsSuccess && response.JsonData?["oso"] is JArray ordersArray)
            {
                try
                {
                    var unfilledOrders = ordersArray.ToObject<List<OrderHistoryItem>>();
                    if (unfilledOrders != null)
                    {
                        for (int i = 0; i < unfilledOrders.Count; i++)
                        {
                            int.TryParse(ordersArray[i]["oso_qty"]?.ToString(), out int unfilledQty);
                            unfilledOrders[i].UnfilledQuantity = unfilledQty;
                            unfilledOrders[i].ExecutedQuantity = 0;
                        }
                        Logger.Instance.Add($"[TR 응답] {account.AccountNumber} 계좌 미체결 내역 조회 성공. {unfilledOrders.Count}건 수신.");
                        return unfilledOrders;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Instance.Add($"[TR 오류] 미체결 내역 데이터 파싱 중 오류: {ex.Message}");
                }
            }
            return new List<OrderHistoryItem>();
        }

        public async Task<bool> SendBuyOrderAsync(AccountInfo account, string stockCode, int quantity, double price, string tradeType = "0")
        {
            string sanitizedStockCode = stockCode.TrimStart('A');
            var requestBody = new { dmst_stex_tp = "KRX", stk_cd = sanitizedStockCode, ord_qty = quantity.ToString(), ord_uv = price.ToString(), trde_tp = tradeType };
            var response = await SendOrderRequestAsync(account, "kt10000", requestBody);
            if (response.IsSuccess)
            {
                Logger.Instance.Add($"[주문 성공] {account.AccountNumber} 계좌, {sanitizedStockCode} 종목 매수주문 정상 접수. 주문번호: {response.JsonData?["ord_no"]}");
            }
            return response.IsSuccess;
        }

        public async Task<bool> SendSellOrderAsync(AccountInfo account, string stockCode, int quantity, double price, string tradeType = "0")
        {
            string sanitizedStockCode = stockCode.TrimStart('A');
            string orderPrice = tradeType == "3" ? "0" : price.ToString();
            var requestBody = new { dmst_stex_tp = "KRX", stk_cd = sanitizedStockCode, ord_qty = quantity.ToString(), ord_uv = orderPrice, trde_tp = tradeType };
            var response = await SendOrderRequestAsync(account, "kt10001", requestBody);
            if (response.IsSuccess)
            {
                Logger.Instance.Add($"[주문 성공] {account.AccountNumber} 계좌, {sanitizedStockCode} 종목 매도주문 정상 접수. 주문번호: {response.JsonData?["ord_no"]}");
            }
            return response.IsSuccess;
        }

        public async Task<bool> SendCancelOrderAsync(AccountInfo account, string stockCode, string originalOrderNumber, int cancelQuantity)
        {
            string sanitizedStockCode = stockCode.TrimStart('A');
            var requestBody = new { dmst_stex_tp = "KRX", orig_ord_no = originalOrderNumber, stk_cd = sanitizedStockCode, cncl_qty = cancelQuantity.ToString() };
            var response = await SendOrderRequestAsync(account, "kt10003", requestBody);
            if (response.IsSuccess)
            {
                Logger.Instance.Add($"[주문 성공] {account.AccountNumber} 계좌, {sanitizedStockCode} 종목 주문({originalOrderNumber}) 취소 정상 접수. 주문번호: {response.JsonData?["ord_no"]}");
            }
            return response.IsSuccess;
        }

        public async Task<List<SearchedStock>> GetWatchlistDetailsAsync(AccountInfo account, string[] stockCodes)
        {
            var stocks = new List<SearchedStock>();
            if (stockCodes == null || !stockCodes.Any()) return stocks;

            string codes = string.Join("|", stockCodes);
            var requestBody = new { stk_cd = codes };
            var response = await SendHttpRequestAsync(account, "ka10095", "/api/dostk/stkinfo", requestBody);

            if (response.IsSuccess && response.JsonData?["atn_stk_infr"] is JArray detailsArray)
            {
                foreach (var item in detailsArray)
                {
                    stocks.Add(new SearchedStock(item["stk_cd"]?.ToString(), item["stk_nm"]?.ToString())
                    {
                        CurrentPrice = double.TryParse(item["cur_prc"]?.ToString(), out double curPrc) ? curPrc : 0,
                        PreviousClosePrice = double.TryParse(item["pred_close_pric"]?.ToString(), out double predClosePric) ? predClosePric : 0,
                        Volume = long.TryParse(item["trde_qty"]?.ToString(), out long vol) ? vol : 0,
                        TradingAmount = long.TryParse(item["trde_prica"]?.ToString(), out long amount) ? amount : 0,
                        MarketCap = long.TryParse(item["mac"]?.ToString(), out long marketCap) ? marketCap : 0
                    });
                }
            }
            // [NEW] 응답 결과 요약 로그
            Logger.Instance.Add($"[TR 응답] ka10095: {stocks.Count}개 관심종목 정보 저장 완료.");
            return stocks;
        }

        public async Task<List<DailyChartData>> GetDailyChartAsync(AccountInfo account, string stockCode, string date)
        {
            var requestBody = new { stk_cd = stockCode, base_dt = date, upd_stkpc_tp = "0" };
            var response = await SendHttpRequestAsync(account, "ka10081", "/api/dostk/chart", requestBody);

            if (response.IsSuccess && response.JsonData?["stk_dt_pole_chart_qry"] is JArray chartArray)
            {
                // [CHANGED] 수신된 원본 데이터의 개수만 로그로 출력
                Logger.Instance.Add($" -> [ka10081] {stockCode} 수신 데이터: {chartArray.Count}건");

                var allData = chartArray.ToObject<List<DailyChartData>>();
                var baseDate = DateTime.ParseExact(date, "yyyyMMdd", CultureInfo.InvariantCulture);
                var threeMonthsAgo = baseDate.AddMonths(-3);

                var filteredData = allData
                    .Where(d => !string.IsNullOrEmpty(d.Date) && DateTime.ParseExact(d.Date, "yyyyMMdd", CultureInfo.InvariantCulture) >= threeMonthsAgo)
                    .ToList();

                return filteredData;
            }
            return new List<DailyChartData>();
        }

        private async Task<(bool IsSuccess, JObject JsonData, string ContYn, string NextKey)> SendHttpRequestAsync(AccountInfo account, string apiId, string endpoint, object requestBody, string contYn = "N", string nextKey = "")
        {
            string jsonBody = JsonConvert.SerializeObject(requestBody);
            var requestUrl = $"{BaseUrl}{endpoint}";
            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
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
                Logger.Instance.Add($"[TR 요청 시작] {apiId} - 계좌: {account.AccountNumber}");
                if (App.IsDebugMode) Logger.Instance.Add($" -> Request Body: {jsonBody}");

                var response = await httpClient.SendAsync(requestMessage);
                var responseString = await response.Content.ReadAsStringAsync();
                Logger.Instance.Add($"[TR 수신 완료] {apiId} - 계좌: {account.AccountNumber}");

                // [CHANGED] 응답 Body 전체를 출력하는 대신, 상태 코드만 출력하도록 변경
                if (App.IsDebugMode)
                {
                    Logger.Instance.Add($" -> Response Status: {response.StatusCode}");
                }

                if (!response.IsSuccessStatusCode)
                {
                    Logger.Instance.Add($"[TR 응답] {apiId} 조회 실패: HTTP 상태 코드 {response.StatusCode}");
                    return (false, null, "N", "");
                }

                try
                {
                    var result = JObject.Parse(responseString);
                    var returnCodeToken = result["return_code"];

                    if (returnCodeToken == null)
                    {
                        Logger.Instance.Add($"[TR 오류] {apiId}: 응답에 'return_code' 필드가 없습니다.");
                        return (false, null, "N", "");
                    }

                    if (returnCodeToken.ToString() == "0")
                    {
                        string cont = response.Headers.TryGetValues("cont-yn", out var c) ? c.FirstOrDefault() : "N";
                        string next = response.Headers.TryGetValues("next-key", out var n) ? n.FirstOrDefault() : "";
                        return (true, result, cont, next);
                    }
                    else
                    {
                        Logger.Instance.Add($"[TR 응답] {apiId} 조회 실패: {result["return_msg"]}");
                    }
                }
                catch (JsonReaderException jsonEx)
                {
                    Logger.Instance.Add($"[TR 오류] {apiId} 응답이 유효한 JSON이 아닙니다: {jsonEx.Message}");
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Add($"[TR 오류] {apiId} 조회 중 예외 발생: {ex.Message}");
            }
            return (false, null, "N", "");
        }

        private async Task<(bool IsSuccess, JObject JsonData)> SendOrderRequestAsync(AccountInfo account, string apiId, object requestBody)
        {
            string jsonBody = JsonConvert.SerializeObject(requestBody);
            var requestUrl = $"{BaseUrl}/api/dostk/ordr";
            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUrl) { Content = content };
            requestMessage.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", account.Token);
            requestMessage.Headers.Add("api-id", apiId);

            try
            {
                Logger.Instance.Add($"[주문 요청 시작] {apiId} - 계좌: {account.AccountNumber}");
                if (App.IsDebugMode) Logger.Instance.Add($" -> Order Request Body: {jsonBody}");

                var response = await httpClient.SendAsync(requestMessage);
                var responseString = await response.Content.ReadAsStringAsync();
                Logger.Instance.Add($"[주문 수신 완료] {apiId} - 계좌: {account.AccountNumber}");

                if (App.IsDebugMode)
                {
                    Logger.Instance.Add($" -> Order Response Status: {response.StatusCode}");
                    Logger.Instance.Add($" -> Order Response Body: {responseString}");
                }

                try
                {
                    var result = JObject.Parse(responseString);
                    var returnCodeToken = result["return_code"];
                    if (returnCodeToken == null)
                    {
                        Logger.Instance.Add($"[주문 오류] {apiId}: 응답에 'return_code' 필드가 없습니다.");
                        return (false, null);
                    }

                    if (returnCodeToken.ToString() == "0")
                    {
                        return (true, result);
                    }
                    Logger.Instance.Add($"[주문 실패] {apiId}: {result["return_msg"]}");
                }
                catch (JsonReaderException jsonEx)
                {
                    Logger.Instance.Add($"[주문 오류] {apiId} 응답이 유효한 JSON이 아닙니다: {jsonEx.Message}");
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Add($"[주문 오류] {apiId} 요청 중 예외 발생: {ex.Message}");
            }
            return (false, null);
        }
    }
}
