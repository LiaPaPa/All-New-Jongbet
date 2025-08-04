// OrderLogItem.cs
using System;

namespace All_New_Jongbet
{
    public class OrderLogItem
    {
        public DateTime Timestamp { get; set; }
        public string AccountNumber { get; set; }
        public string OrderType { get; set; } // "BUY" or "SELL"
        public string StockCode { get; set; }
        public string StockName { get; set; }
        public double OrderPrice { get; set; }
        public int OrderQuantity { get; set; }

        // XAML에서 바인딩하기 위한 추가 속성
        public string OrderTypeDisplay => OrderType.ToUpper();
        public string TimestampDisplay => Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
        public string BorderBrushKey => OrderType.ToUpper() == "BUY" ? "BuyOrderStatusBrush" : "SellOrderStatusBrush";
    }
}