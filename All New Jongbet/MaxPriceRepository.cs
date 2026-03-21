using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;

namespace All_New_Jongbet
{
    public static class MaxPriceRepository
    {
        private static readonly string FilePath;
        // Key: {AccountNumber}_{StockCode}
        private static ConcurrentDictionary<string, double> _maxPriceMap;

        static MaxPriceRepository()
        {
            string folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
            Directory.CreateDirectory(folderPath);
            FilePath = Path.Combine(folderPath, "max_prices.json");
            _maxPriceMap = new ConcurrentDictionary<string, double>();
        }

        public static void Load()
        {
            if (File.Exists(FilePath))
            {
                try
                {
                    string json = File.ReadAllText(FilePath);
                    var dict = JsonConvert.DeserializeObject<Dictionary<string, double>>(json);
                    if (dict != null)
                    {
                        _maxPriceMap = new ConcurrentDictionary<string, double>(dict);
                        Logger.Instance.Add($"매수 최고가 기록 {dict.Count}건을 불러왔습니다.");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Instance.Add($"[오류] MaxPrice 파일 로딩 실패: {ex.Message}");
                }
            }
        }

        public static void Save()
        {
            try
            {
                string json = JsonConvert.SerializeObject(_maxPriceMap, Formatting.Indented);
                File.WriteAllText(FilePath, json);
            }
            catch (Exception ex)
            {
                Logger.Instance.Add($"[오류] MaxPrice 파일 저장 실패: {ex.Message}");
            }
        }

        public static double GetMaxPrice(string accountNumber, string stockCode, double currentPrice)
        {
            string key = $"{accountNumber}_{stockCode}";
            if (_maxPriceMap.TryGetValue(key, out double maxPrice))
            {
                return maxPrice;
            }
            return currentPrice;
        }

        public static void UpdateMaxPrice(string accountNumber, string stockCode, double currentPrice)
        {
            string key = $"{accountNumber}_{stockCode}";
            bool isUpdated = false;
            
            _maxPriceMap.AddOrUpdate(key, currentPrice, (k, oldMax) =>
            {
                if (currentPrice > oldMax)
                {
                    isUpdated = true;
                    return currentPrice;
                }
                return oldMax;
            });

            if (isUpdated || !_maxPriceMap.ContainsKey(key))
            {
                // 처음 추가되거나 값 갱신된 경우 저장 수행
                // 빈번한 I/O가 발생할 수 있지만 잔고 수량이 적어 무리가 가지 않음
                Save();
            }
        }

        public static void Cleanup(string accountNumber, HashSet<string> currentHoldingStockCodes)
        {
            bool isChanged = false;
            foreach (var key in _maxPriceMap.Keys)
            {
                if (key.StartsWith($"{accountNumber}_"))
                {
                    string stockCode = key.Substring(accountNumber.Length + 1);
                    if (!currentHoldingStockCodes.Contains(stockCode))
                    {
                        _maxPriceMap.TryRemove(key, out _);
                        isChanged = true;
                    }
                }
            }
            
            if (isChanged)
            {
                Save();
                Logger.Instance.Add($"[{accountNumber}] 보유하지 않은 종목의 최고가 기록을 초기화(Cleanup) 했습니다.");
            }
        }
    }
}
