using Microsoft.AspNetCore.Mvc;
using SolanaAPI_Test.Models;
using Solnet.Rpc;
using System;
using System.Text;
using System.Text.Json;

namespace SolanaAPI_Test.Services
{
    public class RaydiumSwapService
    {
        private readonly IRpcClient _rpcClient;
        private readonly HttpClient _httpClient;
        private const string RAYDIUM_API_BASE = "https://transaction-v1.raydium.io";

        public RaydiumSwapService(HttpClient httpClient)
        {
            _rpcClient = ClientFactory.GetClient(Cluster.DevNet);
            _httpClient = new HttpClient();
        }

        public async Task<SwapQuote> GetSwapQuote(string inputMint,string outputMint,decimal amount,int slippage = 50, string txVersion ="V0") // 0.5%
        {
            var url = $"{RAYDIUM_API_BASE}/compute/swap-base-in?" +
                      $"inputMint={inputMint}" +
                      $"&outputMint={outputMint}" +
                      $"&amount={amount}" +
                      $"&slippageBps={slippage}" +
                      $"&txVersion={txVersion}";

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<SwapQuote>(content);
        }

        // 获取交换指令
        public async Task<SwapInstructions> GetSwapInstructions(
            SwapQuote quote,
            string userPublicKey)
        {
            var url = $"{RAYDIUM_API_BASE}/swap/instructions";

            var payload = new
            {
                quote = quote,
                userPublicKey = userPublicKey,
                wrapSol = true,
                unwrapSol = true,
                computeUnitPriceMicroLamports = 100000
            };

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json"
            );

            var response = await _httpClient.PostAsync(url, jsonContent);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<SwapInstructions>(content);
        }

        public class SwapQuote
        {
            public string InputMint { get; set; }
            public string OutputMint { get; set; }
            public string InAmount { get; set; }
            public string OutAmount { get; set; }
            public decimal PriceImpact { get; set; }
            public object[] RoutePlan { get; set; }
        }

        public class SwapInstructions
        {
            public Instruction[] Instructions { get; set; }
            public string[] Signers { get; set; }
        }

        public class Instruction
        {
            public string ProgramId { get; set; }
            public object[] Keys { get; set; }
            public string Data { get; set; }
        }
    }
}
