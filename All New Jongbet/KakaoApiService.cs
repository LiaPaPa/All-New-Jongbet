using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace All_New_Jongbet
{
    public class KakaoApiService
    {
        public event Action<string> OnTokenAcquired;

        private HttpListener _httpListener;
        // ⚠️ 중요: 이 곳에 발급받은 REST API 키를 한 번만 입력해주세요.
        private const string RestApiKey = "915e26813a8756b83ab6f1a5f812c2dc";
        private const string RedirectUri = "http://localhost:5000/oauth";

        public void AuthorizeAndGetToken()
        {
            string authUrl = $"https://kauth.kakao.com/oauth/authorize?client_id={RestApiKey}&redirect_uri={RedirectUri}&response_type=code";
            Process.Start(authUrl);
            _ = StartListener();
        }

        private async Task StartListener()
        {
            if (_httpListener != null && _httpListener.IsListening)
            {
                _httpListener.Stop();
            }

            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add(RedirectUri.EndsWith("/") ? RedirectUri : RedirectUri + "/");
            _httpListener.Start();

            HttpListenerContext context = await _httpListener.GetContextAsync();
            HttpListenerRequest request = context.Request;
            string authorizationCode = request.QueryString.Get("code");

            var response = context.Response;
            string responseString = "<HTML><BODY><h1>인증 완료</h1><p>이제 프로그램을 확인하세요.</p></BODY></HTML>";
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
            response.ContentLength64 = buffer.Length;
            var output = response.OutputStream;
            output.Write(buffer, 0, buffer.Length);
            output.Close();
            _httpListener.Stop();

            if (!string.IsNullOrEmpty(authorizationCode))
            {
                await GetAccessTokenAsync(authorizationCode);
            }
        }

        private async Task GetAccessTokenAsync(string authorizationCode)
        {
            using (var httpClient = new HttpClient())
            {
                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "authorization_code"),
                    new KeyValuePair<string, string>("client_id", RestApiKey),
                    new KeyValuePair<string, string>("redirect_uri", RedirectUri),
                    new KeyValuePair<string, string>("code", authorizationCode)
                });

                var response = await httpClient.PostAsync("https://kauth.kakao.com/oauth/token", content);
                var responseString = await response.Content.ReadAsStringAsync();

                JObject json = JObject.Parse(responseString);
                string accessToken = json["access_token"]?.ToString();

                if (!string.IsNullOrEmpty(accessToken))
                {
                    Logger.Instance.Add("카카오톡 Access Token 발급 성공.");
                    OnTokenAcquired?.Invoke(accessToken);
                }
                else
                {
                    Logger.Instance.Add($"카카오톡 Access Token 발급 실패: {responseString}");
                }
            }
        }

        // [FIXED] '나에게 보내기' 메시지 전송 메서드 문법 오류 수정
        public async Task<bool> SendMessageToMeAsync(string accessToken, string message)
        {
            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                var templateObject = new JObject
                {
                    { "object_type", "text" },
                    { "text", message },
                    { "link", new JObject
                        {
                            { "web_url", "https://developers.kakao.com" },
                            { "mobile_web_url", "https://developers.kakao.com" }
                        }
                    },
                    { "button_title", "앱으로 바로가기" }
                };

                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("template_object", templateObject.ToString())
                });

                var response = await httpClient.PostAsync("https://kapi.kakao.com/v2/api/talk/memo/default/send", content);
                var responseString = await response.Content.ReadAsStringAsync();

                JObject json = JObject.Parse(responseString);
                if (json["result_code"]?.ToString() == "0")
                {
                    Logger.Instance.Add("카카오톡 메시지 전송 성공.");
                    return true;
                }
                else
                {
                    Logger.Instance.Add($"카카오톡 메시지 전송 실패: {responseString}");
                    return false;
                }
            }
        }
    }
}