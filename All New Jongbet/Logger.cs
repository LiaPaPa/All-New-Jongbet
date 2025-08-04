// Logger.cs
using System;

namespace All_New_Jongbet
{
    public class Logger
    {
        // 싱글톤 패턴: 프로그램 전체에서 단 하나의 Logger 인스턴스만 사용
        private static readonly Lazy<Logger> _instance = new Lazy<Logger>(() => new Logger());
        public static Logger Instance => _instance.Value;

        // 로그가 추가될 때 발생하는 이벤트
        public event Action<string> LogAdded;

        private Logger() { }

        // 로그를 추가하는 메서드
        public void Add(string message)
        {
            // [시간] 메시지 형식으로 만듦
            string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";

            // 이벤트를 구독한 모든 곳에 로그 메시지를 전달
            LogAdded?.Invoke(logMessage);
        }
    }
}