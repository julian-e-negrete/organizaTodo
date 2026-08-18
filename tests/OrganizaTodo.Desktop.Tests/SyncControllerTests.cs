using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OrganizaTodo.Web.Controllers;
using OrganizaTodo.Web.Models;
using OrganizaTodo.Web.Models.Domain;
using OrganizaTodo.Web.Models.ViewModels;
using OrganizaTodo.Web.Repositories;
using OrganizaTodo.Web.Services;
using OrganizaTodo.Web.Sync;

namespace OrganizaTodo.Desktop.Tests;

// Fast, no-Postgres unit tests for SyncController.Sync — exercised through hand-written fakes
// (this repo has no mocking library) rather than the real DB-backed services, so Finding 3
// (JSON error responses instead of the HTML error page) and Finding 5 (the pull watermark race)
// can be verified deterministically without booting Kestrel or Postgres.
public class SyncControllerTests
{
    private const int TestUserId = 42;
    private const string ValidToken = "valid-token";

    private sealed class FakeAuthService : IAuthService
    {
        public Task<(bool Success, string? Error)> RegisterAsync(RegisterViewModel model) =>
            throw new NotImplementedException("Sync() never calls RegisterAsync");

        public Task<(bool Success, string? Error, User? User)> LoginAsync(string email, string password) =>
            throw new NotImplementedException("Sync() never calls LoginAsync");
    }

    // Identity hash function: lets FakeSyncTokenRepository key directly off the plaintext
    // bearer token below without needing to replicate real hashing.
    private sealed class FakeSyncTokenService : ISyncTokenService
    {
        public (string PlaintextToken, string TokenHash) GenerateToken() =>
            throw new NotImplementedException("Sync() never calls GenerateToken");

        public string HashToken(string plaintextToken) => plaintextToken;
    }

    private sealed class FakeSyncTokenRepository : ISyncTokenRepository
    {
        public Task StoreTokenAsync(int userId, string tokenHash) =>
            throw new NotImplementedException("Sync() never calls StoreTokenAsync");

        public Task<int?> ResolveUserIdAsync(string tokenHash) =>
            Task.FromResult(tokenHash == ValidToken ? (int?)TestUserId : null);

        public Task TouchLastUsedAsync(string tokenHash) => Task.CompletedTask;
    }

    private sealed class FakeSyncPushService(Exception? throwOnApply = null) : ISyncPushService
    {
        public Task ApplyChangesAsync(int userId, IReadOnlyDictionary<string, IReadOnlyList<SyncRowChange>> changesByTable, CancellationToken ct = default)
        {
            if (throwOnApply is not null)
            {
                throw throwOnApply;
            }

            return Task.CompletedTask;
        }
    }

    // Records the instant PullChangesAsync actually started, then optionally sleeps before
    // returning — simulating a pull query that takes real wall-clock time to execute, exactly
    // the window Finding 5's fix needs to close.
    private sealed class FakeSyncPullService(TimeSpan delay) : ISyncPullService
    {
        public DateTimeOffset? PullStartedAt { get; private set; }

        public async Task<Dictionary<string, IReadOnlyList<dynamic>>> PullChangesAsync(int userId, DateTimeOffset since, CancellationToken ct = default)
        {
            PullStartedAt = DateTimeOffset.UtcNow;
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, ct);
            }

            return new Dictionary<string, IReadOnlyList<dynamic>>();
        }
    }

    private static SyncController BuildController(ISyncPushService pushService, ISyncPullService pullService)
    {
        var controller = new SyncController(new FakeAuthService(), new FakeSyncTokenService(), new FakeSyncTokenRepository(), pullService, pushService)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
        controller.Request.Headers.Authorization = $"Bearer {ValidToken}";
        return controller;
    }

    private static SyncRequest EmptyRequest(DateTimeOffset since) =>
        new(since, new Dictionary<string, List<SyncRowChangeDto>>());

    // ------------------------------------------------------------------
    // Finding 3: every rejection/error must come back as clean JSON
    // (ProblemDetails via an ObjectResult), never let an exception bubble
    // out to app.UseExceptionHandler("/Home/Error")'s HTML page.
    // ------------------------------------------------------------------

    [Fact]
    public async Task Sync_PushRejectsWithInvalidOperationException_Returns409ConflictProblemDetails()
    {
        var controller = BuildController(
            new FakeSyncPushService(new InvalidOperationException("Rejected sync row: ownership violation")),
            new FakeSyncPullService(TimeSpan.Zero));

        var result = await controller.Sync(EmptyRequest(DateTimeOffset.MinValue));

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("Rejected sync row: ownership violation", problem.Detail);
    }

    [Fact]
    public async Task Sync_PushThrowsUnexpectedException_Returns500ProblemDetails_NotHtml()
    {
        // A genuinely unexpected failure (not the modeled InvalidOperationException ownership
        // rejection) — e.g. SyncPushService's TablesByJsonKey[jsonKey] throwing
        // KeyNotFoundException for an unregistered table name. Must still come back as JSON.
        var controller = BuildController(
            new FakeSyncPushService(new KeyNotFoundException("The given key was not present in the dictionary.")),
            new FakeSyncPullService(TimeSpan.Zero));

        var result = await controller.Sync(EmptyRequest(DateTimeOffset.MinValue));

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
        Assert.IsType<ProblemDetails>(objectResult.Value);
    }

    [Fact]
    public async Task Sync_SuccessfulPushAndPull_StillReturnsOk()
    {
        var controller = BuildController(new FakeSyncPushService(), new FakeSyncPullService(TimeSpan.Zero));

        var result = await controller.Sync(EmptyRequest(DateTimeOffset.MinValue));

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.IsType<SyncResponse>(ok.Value);
    }

    // ------------------------------------------------------------------
    // Finding 5: the syncedAt watermark must be captured BEFORE the pull
    // query runs, not after — otherwise a row committed while the pull is
    // executing is silently lost from every future sync.
    // ------------------------------------------------------------------

    [Fact]
    public async Task Sync_CapturesWatermarkBeforePullRuns_NotAfter()
    {
        // The fake pull service records its own start time, then sleeps 200ms before
        // returning — simulating real query latency. With the fix, SyncedAt is captured
        // immediately before calling PullChangesAsync, so it must land at-or-before that
        // recorded start time. Before the fix (SyncedAt = UtcNow captured AFTER
        // PullChangesAsync returns), SyncedAt would land ~200ms AFTER PullStartedAt, failing
        // this assertion.
        var pullService = new FakeSyncPullService(TimeSpan.FromMilliseconds(200));
        var controller = BuildController(new FakeSyncPushService(), pullService);

        var result = await controller.Sync(EmptyRequest(DateTimeOffset.MinValue));

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<SyncResponse>(ok.Value);

        Assert.NotNull(pullService.PullStartedAt);
        Assert.True(
            response.SyncedAt <= pullService.PullStartedAt!.Value.AddMilliseconds(5),
            $"Expected SyncedAt ({response.SyncedAt:O}) at or before the pull's recorded start time ({pullService.PullStartedAt:O})");
    }
}
