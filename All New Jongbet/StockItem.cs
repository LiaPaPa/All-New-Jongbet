// StockItem.cs
using Newtonsoft.Json;

namespace All_New_Jongbet
{
    public class StockItem
    {
        [JsonProperty("9001")] // JSON 필드 이름과 매핑
        public string Code { get; set; }

        [JsonProperty("302")]
        public string Name { get; set; }

        [JsonProperty("10")]
        public string CurrentPrice { get; set; }

        [JsonProperty("12")]
        public string FluctuationRate { get; set; }

        [JsonProperty("13")]
        public string Volume { get; set; }
    }
}