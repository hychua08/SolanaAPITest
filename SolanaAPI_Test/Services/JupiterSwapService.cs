using Microsoft.AspNetCore.Components;
using SolanaAPI_Test.Models;
using System.Text.Json;


namespace SolanaAPI_Test.Services
{
    public class JupiterSwapService(HttpClient httpClient, ILogger<JupiterSwapService> logger)
    {
        private const string JUPITER_API_BASE = "https://lite-api.jup.ag/swap/v1";

        public async Task<JupiterQuoteResponse> GetSwapQuoteAsync(SwapQuoteRequest request)
        {
            var queryParams = new Dictionary<string, string>
            {
                ["inputMint"] = request.InputMint,
                ["outputMint"] = request.OutputMint,
                ["amount"] = request.Amount.ToString(),
                ["slippageBps"] = request.SlippageBps.ToString(),

            };

            var url = $"{JUPITER_API_BASE}/quote?{string.Join("&", queryParams.Select(kvp => $"{kvp.Key}={kvp.Value}"))}";

            logger.LogInformation("Fetching quote from Jupiter: {Url}", url);

            var response = await httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode) 
            {
                var error = await response.Content.ReadAsStringAsync();
                logger.LogError("Jupiter quote failed: {Error}", error);
                throw new Exception($"Failed to get quote: {error}");
            }

            var json = await response.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            var quote = JsonSerializer.Deserialize<JupiterQuoteResponse>(json, options);


            return quote ?? new();
        }

        public async Task<SwapResponse> GetSwapTransaction(SwapRequest request)
        {
            var swapRequest = new
            {
                userPublicKey = request.UserPublicKey,
                quoteResponse = request.QuoteResponse,
                wrapAndUnwrap = request.WrapUnwrapSOL,
                DynamicComputeUnitLimit = true,
                prioritizzationFeeLamports = "auto"
            };

            var url = $"{JUPITER_API_BASE}/swap";

            var response = await httpClient.PostAsJsonAsync(url, swapRequest);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                logger.LogError("Jupiter swap transaction failed: {Error}", error);
                throw new Exception($"Failed to get swap transaction: {error}");
            }

            var json = await response.Content.ReadAsStringAsync();
            var swapResponse = JsonSerializer.Deserialize<JupiterSwapResponse>(json);

            return new SwapResponse
            {
                SwapTransaction = swapResponse?.SwapTransaction ?? "",
                LastValidBlockHight = swapResponse?.LastValidBlockHeight ?? ""
            };
        }

        public async Task<Dictionary<string, decimal>> GetTokenPriceAsync(string[] tokenMints)
        {
            var ids = string.Join(",", tokenMints);
            var url = $"https://price.jup.ag/v4/price?ids={ids}";

            var response = await httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Failed to fetch token prices");
                return new Dictionary<string, decimal>();
            }

            var json = await response.Content.ReadAsStringAsync();
            var priceResponse = JsonSerializer.Deserialize<JupiterPriceResponse>(json);

            return priceResponse?.Data?.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Price
            ) ?? new Dictionary<string, decimal>();
        }
    }
}
