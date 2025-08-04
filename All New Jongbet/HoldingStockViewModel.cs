// HoldingStockViewModel.cs
namespace All_New_Jongbet
{
    // HoldingStock의 모든 속성을 상속받음
    public class HoldingStockViewModel : HoldingStock
    {
        public string AccountNumber { get; set; }

        // 등락률(%)을 계산하는 속성
        public double DailyFluctuationRate
        {
            get
            {
                if (PreviousClosePrice == 0) return 0;
                return (CurrentPrice / PreviousClosePrice - 1) * 100;
            }
        }
    }
}