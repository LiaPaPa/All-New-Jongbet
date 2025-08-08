// StrategyRepository.cs 라는 이름으로 새 파일을 생성하고 아래 코드를 붙여넣으세요.

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace All_New_Jongbet
{
    public static class StrategyRepository
    {
        private static readonly string FilePath;

        static StrategyRepository()
        {
            string strategyFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Strategy");
            Directory.CreateDirectory(strategyFolderPath);
            FilePath = Path.Combine(strategyFolderPath, "strategies.json");
        }

        public static ObservableCollection<StrategyInfo> Load()
        {
            if (File.Exists(FilePath))
            {
                try
                {
                    string json = File.ReadAllText(FilePath);
                    var loadedStrategies = JsonConvert.DeserializeObject<List<StrategyInfo>>(json);
                    if (loadedStrategies != null)
                    {
                        Logger.Instance.Add("저장된 전략 파일을 성공적으로 불러왔습니다.");
                        return new ObservableCollection<StrategyInfo>(loadedStrategies);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Instance.Add($"[오류] 전략 파일 로딩 중 예외 발생: {ex.Message}");
                }
            }
            return new ObservableCollection<StrategyInfo>();
        }

        public static void Save(ObservableCollection<StrategyInfo> strategies)
        {
            try
            {
                string json = JsonConvert.SerializeObject(strategies.ToList(), Formatting.Indented);
                File.WriteAllText(FilePath, json);
                Logger.Instance.Add("전략 목록을 파일에 저장했습니다.");
            }
            catch (Exception ex)
            {
                Logger.Instance.Add($"[오류] 전략 파일 저장 중 예외 발생: {ex.Message}");
            }
        }
    }
}