// TransactionRecord.cs (새 파일)
// 거래 기록 데이터 구조를 정의하는 클래스입니다.

namespace All_New_Jongbet
{
    public class TransactionRecord
    {
        public string StockCode { get; set; }
        public string StockName { get; set; }

        public double? BuyPrice { get; set; }
        public int? BuyQuantity { get; set; }
        public double? BuyAmount { get; set; }
        public string BuyDate { get; set; }
        public string BuyTime { get; set; }

        public double? SellPrice { get; set; }
        public int? SellQuantity { get; set; }
        public double? SellAmount { get; set; }
        public string SellDate { get; set; }
        public string SellTime { get; set; }
    }
}
