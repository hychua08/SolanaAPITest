using System.Globalization;
using System.Text.Json.Serialization;

namespace SolanaAPI_Test.Models
{
    public class SwapModels
    {
    }

    public class SwapQuoteRequest
    {
        public string InputMint { get; set; }
        public string OutputMint { get; set; }
        public long Amount { get; set; }
        public int SlippageBps { get; set; } = 50;
    }

    public class SwapQuoteResponse
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

        [JsonPropertyName("priceImpactPct")]
        public string PriceImpactPct { get; set; }

        [JsonPropertyName("routePlan")]
        public List<SwapRoute> RoutePlan { get; set; }

        [JsonPropertyName("quoteId")]
        public string QuoteId {  get; set; }
    }

    public class SwapRoute
    {
        [JsonPropertyName("swapInfo")]
        public string SwapInfo { get; set; }

        [JsonPropertyName("percent")]
        public double Percent { get; set; }

        //[JsonPropertyName("bps")]
        //public int Bps { get; set; }
    }
    //public class SwapInfo
    //{
    //    [JsonPropertyName("ammKey")]
    //    public string AmmKey { get; set; }

    //    [JsonPropertyName("label")]
    //    public string Label { get; set; }

    //    [JsonPropertyName("inputMint")]
    //    public string InputMint { get; set; }

    //    [JsonPropertyName("outputMint")]
    //    public string OutputMint { get; set; }

    //    [JsonPropertyName("inAmount")]
    //    public string InAmount { get; set; }

    //    [JsonPropertyName("outAmount")]
    //    public string OutAmount { get; set; }

    //    [JsonPropertyName("feeAmount")]
    //    public string FeeAmount { get; set; }

    //    [JsonPropertyName("feeMint")]
    //    public string FeeMint { get; set; }
    //}
    public class SwapRequest
    {
        public string UserPublicKey { get; set; }
        public JupiterQuoteResponse QuoteResponse { get; set; }
        public bool WrapUnwrapSOL { get; set; } = true;
    }

    public class SwapResponse
    {
        public string SwapTransaction { get; set; }
        public string LastValidBlockHight { get; set; }
    }

    public class ExcuteSwapRequest
    {
        public string InputMint { get; set; }
        public string OutputMint { get; set; }
        public long Amount { get; set; }
        public string UserPublicKey {  get; set; }
        public string UserPrivateKey { get; set; }
        public int SlippageBps { get; set; } = 50;
    }


}
