using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WalletApi.Application.DTOs.Wallet;
using WalletApi.Application.Interfaces;

namespace WalletApi.API.Controllers;

[ApiController]
[Route("api/v1/wallets")]
[Authorize] // All endpoints require a valid JWT
public class WalletsController : ControllerBase
{
    private readonly IWalletService _wallets;
    public WalletsController(IWalletService wallets) => _wallets = wallets;

    // Extracts the user's ID from the JWT claims
    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? User.FindFirstValue("sub")!);

    [HttpGet]
    public async Task<IActionResult> GetWallet(CancellationToken ct) =>
        Ok(await _wallets.GetWalletAsync(CurrentUserId, ct));
    
    [HttpPost("topup")]
    public async Task<IActionResult> TopUp([FromBody] TopUpRequest req, CancellationToken ct) =>
        Ok(await _wallets.TopUpAsync(CurrentUserId, req, ct));
    
    [HttpPost("transfer")]
    public async Task<IActionResult> Transfer([FromBody] TransferRequest req, CancellationToken ct)
    {
        await _wallets.TransferAsync(CurrentUserId, req, ct);
        return NoContent();
    }
    
    [HttpGet("transactions")]
    public async Task<IActionResult> GetTransactions(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(await _wallets.GetTransactionsAsync(CurrentUserId, page, pageSize, ct));
}