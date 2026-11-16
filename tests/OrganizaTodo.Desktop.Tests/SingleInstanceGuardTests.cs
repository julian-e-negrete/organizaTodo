using OrganizaTodo.Desktop.Lifecycle;

namespace OrganizaTodo.Desktop.Tests;

public class SingleInstanceGuardTests
{
    [Fact]
    public void SecondGuardWithSameName_FailsToAcquireWhileFirstHoldsIt()
    {
        var name = $"organizatodo-test-{Guid.NewGuid():N}";
        using var first = new SingleInstanceGuard(name);
        Assert.True(first.TryAcquire());

        using var second = new SingleInstanceGuard(name);
        Assert.False(second.TryAcquire());
    }

    [Fact]
    public void GuardWithDifferentNames_BothAcquireIndependently()
    {
        using var first = new SingleInstanceGuard($"organizatodo-test-{Guid.NewGuid():N}");
        using var second = new SingleInstanceGuard($"organizatodo-test-{Guid.NewGuid():N}");

        Assert.True(first.TryAcquire());
        Assert.True(second.TryAcquire());
    }
}
