using MobileApp.Services;
using Shared.DTOs;
using Xunit;

namespace MobileApp.Tests.Services;

public sealed class TokenRefreshResultTests
{
    [Fact]
    public void Ok_IsSuccessAndNotTransient()
    {
        var result = TokenRefreshResult.Ok(new TokenResponseDTO("a", "r"), "refreshed");
        Assert.True(result.Succeeded);
        Assert.False(result.IsTransientFailure);
        Assert.NotNull(result.Token);
    }

    [Fact]
    public void Transient_KeepsSession()
    {
        var result = TokenRefreshResult.Transient("offline");
        Assert.False(result.Succeeded);
        Assert.True(result.IsTransientFailure);
        Assert.Null(result.Token);
    }

    [Fact]
    public void InvalidSession_IsHardFailure()
    {
        var result = TokenRefreshResult.InvalidSession("revoked");
        Assert.False(result.Succeeded);
        Assert.False(result.IsTransientFailure);
    }
}

public sealed class SyncProgressEventArgsTests
{
    [Theory]
    [InlineData(SyncPhase.Starting, 0.05)]
    [InlineData(SyncPhase.PullingCategories, 0.45)]
    [InlineData(SyncPhase.PullingLocations, 0.55)]
    [InlineData(SyncPhase.PullingDepartments, 0.65)]
    [InlineData(SyncPhase.Finalizing, 0.95)]
    [InlineData(SyncPhase.Completed, 1.0)]
    [InlineData(SyncPhase.Failed, 0)]
    public void NormalizedProgress_MatchesPhase(SyncPhase phase, double expected)
    {
        var args = new SyncProgressEventArgs { Phase = phase };
        Assert.Equal(expected, args.NormalizedProgress, 3);
    }

    [Fact]
    public void PushingChanges_ScalesWithItemCount()
    {
        var args = new SyncProgressEventArgs
        {
            Phase = SyncPhase.PushingChanges,
            CurrentItem = 5,
            TotalItems = 10
        };

        Assert.Equal(0.25, args.NormalizedProgress, 3);
    }
}
