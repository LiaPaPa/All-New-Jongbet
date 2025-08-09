// TransactionRepository.cs (새 파일)
// 거래 기록을 JSON 파일로 저장하고 불러오는 기능을 담당합니다.

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace All_New_Jongbet
{
    public static class TransactionRepository
    {
        private static readonly string FolderPath;

        static TransactionRepository()
        {
            // 프로그램 실행 경로에 'transaction' 폴더 생성
            FolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "transaction");
            Directory.CreateDirectory(FolderPath);
        }

        // 계좌번호와 날짜를 기반으로 파일 경로 생성
        private static string GetFilePath(string accountNumber)
        {
            return Path.Combine(FolderPath, $"{accountNumber}_{DateTime.Now:yyyyMMdd}.json");
        }

        // 파일에서 거래 기록 불러오기
        public static List<TransactionRecord> Load(string accountNumber)
        {
            string filePath = GetFilePath(accountNumber);
            if (File.Exists(filePath))
            {
                try
                {
                    string json = File.ReadAllText(filePath);
                    return JsonConvert.DeserializeObject<List<TransactionRecord>>(json) ?? new List<TransactionRecord>();
                }
                catch (Exception ex)
                {
                    Logger.Instance.Add($"[오류] 거래 기록 파일 로딩 실패: {ex.Message}");
                    return new List<TransactionRecord>();
                }
            }
            return new List<TransactionRecord>();
        }

        // 파일에 거래 기록 저장하기
        public static void Save(string accountNumber, List<TransactionRecord> records)
        {
            string filePath = GetFilePath(accountNumber);
            try
            {
                string json = JsonConvert.SerializeObject(records, Formatting.Indented);
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                Logger.Instance.Add($"[오류] 거래 기록 파일 저장 실패: {ex.Message}");
            }
        }

        // 거래 발생 시 기록 추가 또는 업데이트
        public static void AddOrUpdateTransaction(string accountNumber, string stockCode, string stockName, string tradeType, int executedQuantity, double executedPrice, string executedTime)
        {
            var records = Load(accountNumber);
            var record = records.FirstOrDefault(r => r.StockCode == stockCode);

            if (record == null)
            {
                record = new TransactionRecord { StockCode = stockCode, StockName = stockName };
                records.Add(record);
            }

            if (tradeType.Contains("매수"))
            {
                record.BuyQuantity = (record.BuyQuantity ?? 0) + executedQuantity;
                record.BuyAmount = (record.BuyAmount ?? 0) + (executedPrice * executedQuantity);
                record.BuyPrice = record.BuyAmount / record.BuyQuantity; // 평균 매수 단가 업데이트
                record.BuyDate = DateTime.Now.ToString("yyyy-MM-dd");
                record.BuyTime = executedTime;
            }
            else if (tradeType.Contains("매도"))
            {
                record.SellQuantity = (record.SellQuantity ?? 0) + executedQuantity;
                record.SellAmount = (record.SellAmount ?? 0) + (executedPrice * executedQuantity);
                record.SellPrice = record.SellAmount / record.SellQuantity; // 평균 매도 단가 업데이트
                record.SellDate = DateTime.Now.ToString("yyyy-MM-dd");
                record.SellTime = executedTime;
            }

            Save(accountNumber, records);
            Logger.Instance.Add($"[{accountNumber}] 거래 기록 저장: {stockName} ({tradeType})");
        }
    }
}
