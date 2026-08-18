namespace MobileApp.Services;

public sealed class MauiNetworkAccessService : INetworkAccessService
{
    public bool HasInternetAccess => Connectivity.NetworkAccess == NetworkAccess.Internet;
}
