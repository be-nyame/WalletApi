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
}