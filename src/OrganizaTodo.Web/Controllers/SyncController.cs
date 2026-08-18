using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using OrganizaTodo.Web.Models;
using OrganizaTodo.Web.Repositories;
using OrganizaTodo.Web.Services;
using OrganizaTodo.Web.Sync;

namespace OrganizaTodo.Web.Controllers;

[ApiController]
[Route("api/sync")]
public sealed class SyncController(
    IAuthService authService,
    ISyncTokenService syncTokenService,
    ISyncTokenRepository syncTokenRepository,
    ISyncPullService syncPullService,
    ISyncPushService syncPushService) : ControllerBase
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

    [HttpPost("")]
    public async Task<IActionResult> Sync([FromBody] SyncRequest request)
    {
        var authHeader = Request.Headers.Authorization.ToString();
        if (!authHeader.StartsWith("Bearer ", StringComparison.Ordinal))
        {
            return Unauthorized();
        }

        var plaintextToken = authHeader["Bearer ".Length..];
        var tokenHash = syncTokenService.HashToken(plaintextToken);
        var userId = await syncTokenRepository.ResolveUserIdAsync(tokenHash);
        if (userId is null)
        {
            return Unauthorized();
        }

        await syncTokenRepository.TouchLastUsedAsync(tokenHash);

        var changes = request.Changes.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<SyncRowChange>)kvp.Value
                .Select(dto => new SyncRowChange(dto.SyncId, dto.UpdatedAt, dto.IsDeleted, NormalizeFields(dto.Fields)))
                .ToList());

        await syncPushService.ApplyChangesAsync(userId.Value, changes);
        var pulledChanges = await syncPullService.PullChangesAsync(userId.Value, request.Since);

        return Ok(new SyncResponse(DateTimeOffset.UtcNow, pulledChanges));
    }

    // [FromBody] model binding deserializes SyncRowChangeDto.Fields (a Dictionary<string, object?>)
    // via System.Text.Json, whose default behavior for an "object"-typed slot is to box a
    // System.Text.Json.JsonElement rather than a plain CLR value. Left as-is, that breaks two
    // things downstream in SyncPushService: the ownership check `Equals(fields["UserId"], userId)`
    // always returns false (a boxed JsonElement is never .Equals to a boxed int, even for the
    // legitimate owner), and passing a JsonElement straight into a Dapper/Npgsql parameter isn't
    // supported. Unwrap each JsonElement into the plain CLR value it represents before the
    // payload reaches SyncPushService.
    //
    // Whole numbers are unboxed as `int` when they fit, specifically so a payload's "UserId"
    // field ends up typed exactly as `int` — SyncPushService's ownership check does a plain
    // `Equals(fields["UserId"], userId)` against the `int userId` from the bearer token, and
    // `object.Equals` never treats a boxed `long`/`decimal` as equal to a boxed `int` even when
    // the numeric values match. Falling back to `long` then `decimal` still lets non-Int32 whole
    // numbers and fractional values (e.g. Amount) round-trip correctly for every other column.
    private static Dictionary<string, object?> NormalizeFields(Dictionary<string, object?> fields) =>
        fields.ToDictionary(kvp => kvp.Key, kvp => NormalizeJsonValue(kvp.Value));

    private static object? NormalizeJsonValue(object? value)
    {
        if (value is not JsonElement element)
        {
            return value;
        }

        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            // Each branch is cast to `object` explicitly — without it, C#'s conditional
            // (ternary) expression unifies int/long/decimal branches to their common type
            // (decimal, since int and long both convert to it implicitly), silently widening
            // a successfully-parsed `int` into a boxed `decimal` even when TryGetInt32
            // succeeds. That widening broke SyncPushService's ownership check, which compares
            // this value to a plain `int` via `Equals` — a boxed `decimal` is never `.Equals`
            // to a boxed `int`, even when numerically identical.
            JsonValueKind.Number => element.TryGetInt32(out var intValue) ? (object)intValue
                : element.TryGetInt64(out var longValue) ? longValue
                : element.GetDecimal(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => NormalizeJsonValue(p.Value)),
            JsonValueKind.Array => element.EnumerateArray().Select(e => NormalizeJsonValue(e)).ToList(),
            _ => null,
        };
    }
}
