using System;
using System.IO;

namespace All_New_Jongbet
{
    public class Logger
    {
        // 싱글톤 패턴: 프로그램 전체에서 단 하나의 Logger 인스턴스만 사용
        private static readonly Lazy<Logger> _instance = new Lazy<Logger>(() => new Logger());
        public static Logger Instance => _instance.Value;

        // 로그가 추가될 때 발생하는 이벤트
        public event Action<string> LogAdded;

        // 파일 쓰기 동기화를 위한 락 객체
        private readonly object _lockObject = new object();
        private readonly string _logDirectory;

        private Logger() 
        { 
            _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Log");
            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }
        }

        // 로그를 추가하는 메서드
        public void Add(string message)
        {
            // [시간] 메시지 형식으로 만듦
            string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";

            // 이벤트를 구독한 모든 곳에 로그 메시지를 전달
            LogAdded?.Invoke(logMessage);

            // 파일에 실시간 기록
            try
            {
                lock (_lockObject)
                {
                    string filePath = Path.Combine(_logDirectory, $"{DateTime.Now:yyyyMMdd}_log.txt");
                    File.AppendAllText(filePath, logMessage + Environment.NewLine);
                }
            }
            catch
            {
                // 로깅 쓰기 실패 시 예외 무시 (메인 로직 방해 방지)
            }
        }
    }
}