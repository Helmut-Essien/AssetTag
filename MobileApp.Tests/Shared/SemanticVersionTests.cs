using Shared.Helpers;
using Xunit;

namespace MobileApp.Tests.Shared;

public sealed class SemanticVersionTests
{
    [Theory]
    [InlineData("mobile-v1.0.2-rc.1", "1.0.2-rc.1")]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData("1.0.0", "1.0.0")]
    public void FromMobileTag_StripsPrefix(string tag, string expected)
    {
        Assert.Equal(expected, SemanticVersion.FromMobileTag(tag));
    }

    [Fact]
    public void Compare_PreReleaseIsLowerThanStable()
    {
        Assert.True(SemanticVersion.Compare("1.0.2-rc.1", "1.0.2") < 0);
        Assert.True(SemanticVersion.Compare("1.0.2", "1.0.2-rc.1") > 0);
    }

    [Fact]
    public void Compare_NumericCoreOrder()
    {
        Assert.True(SemanticVersion.Compare("1.0.1", "1.0.2") < 0);
        Assert.Equal(0, SemanticVersion.Compare("1.0.2", "1.0.2"));
        Assert.True(SemanticVersion.Compare("1.1.0", "1.0.9") > 0);
    }

    [Theory]
    [InlineData("beta", SemanticVersion.BetaChannel)]
    [InlineData("BETA", SemanticVersion.BetaChannel)]
    [InlineData("stable", SemanticVersion.StableChannel)]
    [InlineData("", SemanticVersion.StableChannel)]
    [InlineData(null, SemanticVersion.StableChannel)]
    public void NormalizeChannel_UnknownBecomesStable(string? input, string expected)
    {
        Assert.Equal(expected, SemanticVersion.NormalizeChannel(input));
    }
}
