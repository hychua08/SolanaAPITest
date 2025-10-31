using Microsoft.AspNetCore.Mvc;
using Org.BouncyCastle.Bcpg;
using SolanaAPI_Test.Models;
using SolanaAPI_Test.Services;

namespace SolanaAPI_Test.Controllers
{
    public class SwapController(JupiterSwapService jupiterService, ILogger<SwapController> logger) : ControllerBase
    {
        public static class TokenAddresses
        {
            public const string dwSOL = "So11111111111111111111111111111111111111112";
            public const string dRAY = "DRay3aNHKdjZ4P4DoRnyxdKh6jBrf4HpjfDkQF7MFPpR";
            public const string dUSDC = "USDCoctVLVnvTXBEuP9s8hntucdJokbo17RwHuNXemT";
            public const string USDC = "EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v";
            public const string USDT = "Es9vMFrzaCERmJfrF4H2FYD4KCoNkY11McCe8BenwNYB";
            public const string BONK = "DezXAZ8z7PnrnRJjz3wXBoRgixCa6xjnB7YaB1pPB263";
        }

        [HttpPost("quote")]
        //[ProducesResponseType(typeof(SwapQuoteResponse), 200)]
        public async Task<ActionResult<SwapQuoteResponse>> GetQuote([FromBody] SwapQuoteRequest request)
        {
            try
            {
                logger.LogInformation("Getting swap quote: {InputMint} -> {OutputMint}, Amount: {Amount}",
                request.InputMint, request.OutputMint, request.Amount);

                var quote = await jupiterService.GetSwapQuoteAsync(request);

                return Ok(quote);
            }
            catch (Exception ex) 
            {
                logger.LogError(ex, "Failed to get swap quote");
                return StatusCode(500, new { message = "Failed to get quote", error = ex.Message });
            }
        }

        [HttpPost("transaction")]
        [ProducesResponseType(typeof(SwapResponse), 200)]
        public async Task<ActionResult<SwapResponse>> GetSwapTransaction([FromBody] SwapRequest request)
        {
            try
            {
                logger.LogInformation("Creating swap transaction for user: {UserPublicKey}", request.UserPublicKey);

                var transaction = await jupiterService.GetSwapTransaction(request);

                return Ok(transaction);

            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to create swap transaction");
                return StatusCode(500, new { message = "Failed to create transaction", error = ex.Message });
            }
        }

        [HttpGet("prices")]
        [ProducesResponseType(typeof(Dictionary<string, decimal>), 200)]
        public async Task<ActionResult<Dictionary<string, decimal>>> GetPrices([FromQuery] string tokens)
        {
            try
            {
                var tokenArray = tokens.Split(",");
                var prices = await jupiterService.GetTokenPriceAsync(tokenArray);

                return Ok(prices);
            }
            catch (Exception ex) {
                logger.LogError(ex, "Failed to get token prices");
                return StatusCode(500, new { message = "Failed to get prices", error = ex.Message });
            } 
        }

        [HttpPost("sol-to-usdc")]
        public async Task<ActionResult<SwapQuoteResponse>> SwapSolToUsdc([FromBody] SimpleSwapRequest request)
        {
            try
            {

                var amountInLamports = (long)(request.AmountInSol * 1_000_000_000);

                var quoteRequest = new SwapQuoteRequest
                {
                    InputMint = TokenAddresses.dwSOL,
                    OutputMint = TokenAddresses.dRAY,
                    Amount = amountInLamports,
                    SlippageBps = request.SlippageBps
                };

                var quote = await jupiterService.GetSwapQuoteAsync(quoteRequest);

                var outAmountUsdc = decimal.Parse(quote.OutAmount) / 1_000_000;

                return Ok(new
                {
                    quote,
                    estimatedOutput = $"{outAmountUsdc:F2} USDC",
                    priceImpact = $"{decimal.Parse(quote.PriceImpactPct): F2}%"
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to swap SOL to USDC");
                return StatusCode(500, new { message = "Swap failed", error = ex.Message });
            }
        }


        public class SimpleSwapRequest
        {
            public decimal AmountInSol { get; set; }
            public int SlippageBps { get; set; } = 50;
        }
    }
}
