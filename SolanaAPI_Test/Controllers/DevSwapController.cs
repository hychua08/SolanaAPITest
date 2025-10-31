using Microsoft.AspNetCore.Mvc;
using SolanaAPI_Test.Models;
using SolanaAPI_Test.Services;

namespace SolanaAPI_Test.Controllers
{
    public class DevSwapController(RaydiumSwapService raydiumService, ILogger<SwapController> logger) : ControllerBase
    {

        [HttpPost("dev_quote")]
        //[ProducesResponseType(typeof(SwapQuoteResponse), 200)]
        public async Task<ActionResult<SwapQuoteResponse>> GetQuote([FromBody] SwapQuoteRequest request)
        {
            try
            {
                logger.LogInformation("Getting swap quote: {InputMint} -> {OutputMint}, Amount: {Amount}",
                request.InputMint, request.OutputMint, request.Amount);

                var quote = await raydiumService.GetSwapQuote(request.InputMint, request.OutputMint, request.Amount, request.SlippageBps);

                return Ok(quote);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to get swap quote");
                return StatusCode(500, new { message = "Failed to get quote", error = ex.Message });
            }
        }

    }
}
