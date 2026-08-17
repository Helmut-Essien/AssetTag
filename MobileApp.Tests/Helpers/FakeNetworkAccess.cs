using MobileApp.Services;

namespace MobileApp.Tests.Helpers;

public sealed class FakeNetworkAccess : INetworkAccessService
{
    public bool HasInternetAccess { get; set; } = true;
}
