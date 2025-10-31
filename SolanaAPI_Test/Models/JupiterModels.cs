using System.Text.Json.Serialization;

namespace SolanaAPI_Test.Models
{
    public class JupiterQuoteResponse
    {
        [JsonPropertyName("inputMint")]
        public string InputMint { get; set; }

        [JsonPropertyName("outputMint")]
        public string OutputMint { get; set; }

        [JsonPropertyName("inAmount")]
        public string InAmount { get; set; }

        [JsonPropertyName("outAmount")]
        public string OutAmount { get; set; }

        [JsonPropertyName("otherAmountThreshold")]
        public string OtherAmountThreshold { get; set; }

        [JsonPropertyName("swapMode")]
        public string SwapMode { get; set; }

        [JsonPropertyName("slippageBps")]
        public int SlippageBps { get; set; }

        [JsonPropertyName("platformFee")]
        public object PlatformFee { get; set; }

        [JsonPropertyName("priceImpactPct")]
        public string PriceImpactPct { get; set; }

        [JsonPropertyName("routePlan")]
        public List<RoutePlan> RoutePlan { get; set; }

        [JsonPropertyName("contextSlot")]
        public long ContextSlot { get; set; }

        [JsonPropertyName("timeTaken")]
        public double TimeTaken { get; set; }

        [JsonPropertyName("swapUsdValue")]
        public string SwapUsdValue { get; set; }

        [JsonPropertyName("simplerRouteUsed")]
        public bool SimplerRouteUsed { get; set; }

        [JsonPropertyName("mostReliableAmmsQuoteReport")]
        public MostReliableAmmsQuoteReport MostReliableAmmsQuoteReport { get; set; }

        [JsonPropertyName("useIncurredSlippageForQuoting")]
        public object UseIncurredSlippageForQuoting { get; set; }

        [JsonPropertyName("otherRoutePlans")]
        public object OtherRoutePlans { get; set; }

        [JsonPropertyName("loadedLongtailToken")]
        public bool LoadedLongtailToken { get; set; }

        [JsonPropertyName("instructionVersion")]
        public string InstructionVersion { get; set; }
    }

    public class RoutePlan
    {
        [JsonPropertyName("swapInfo")]
        public SwapInfo SwapInfo { get; set; }

        [JsonPropertyName("percent")]
        public int Percent { get; set; }

        [JsonPropertyName("bps")]
        public int Bps { get; set; }
    }

    public class SwapInfo
    {
        [JsonPropertyName("ammKey")]
        public string AmmKey { get; set; }

        [JsonPropertyName("label")]
        public string Label { get; set; }

        [JsonPropertyName("inputMint")]
        public string InputMint { get; set; }

        [JsonPropertyName("outputMint")]
        public string OutputMint { get; set; }

        [JsonPropertyName("inAmount")]
        public string InAmount { get; set; }

        [JsonPropertyName("outAmount")]
        public string OutAmount { get; set; }

        [JsonPropertyName("feeAmount")]
        public string FeeAmount { get; set; }

        [JsonPropertyName("feeMint")]
        public string FeeMint { get; set; }
    }

    public class MostReliableAmmsQuoteReport
    {
        [JsonPropertyName("info")]
        public Dictionary<string, string> Info { get; set; }
    }

    public class JupiterSwapInfo
    {
        [JsonPropertyName("label")]
        public string Label { get; set; }
    }

    public class JupiterSwapResponse
    {
        [JsonPropertyName("swapTransaction")]
        public string SwapTransaction { get; set; }

        [JsonPropertyName("lastValidBlockHeight")]
        public string LastValidBlockHeight { get; set; }
    }

    public class JupiterPriceResponse
    {
        [JsonPropertyName("data")]
        public Dictionary<string, JupiterTokenPrice> Data { get; set; }
    }

    public class JupiterTokenPrice
    {
        [JsonPropertyName("price")]
        public decimal Price { get; set; }
    }

}
