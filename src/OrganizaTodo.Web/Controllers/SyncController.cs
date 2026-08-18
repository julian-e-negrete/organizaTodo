using Microsoft.AspNetCore.Mvc;
using OrganizaTodo.Web.Models;
using OrganizaTodo.Web.Repositories;
using OrganizaTodo.Web.Services;

namespace OrganizaTodo.Web.Controllers;

[ApiController]
[Route("api/sync")]
public sealed class SyncController(
    IAuthService authService,
    ISyncTokenService syncTokenService,
    ISyncTokenRepository syncTokenRepository) : ControllerBase
{
    [HttpPost("connect")]
    public async Task<IActionResult> Connect([FromBody] ConnectRequest request)
    {
        var (success, _, user) = await authService.LoginAsync(request.Email, request.Password);
        if (!success || user is null)
        {
            return Unauthorized();
        }

        var (plaintextToken, tokenHash) = syncTokenService.GenerateToken();
        await syncTokenRepository.StoreTokenAsync(user.Id, tokenHash);

        return Ok(new ConnectResponse(plaintextToken, user.Id));
    }
}
