using MobileApp.Views;

namespace MobileApp.Services;

public class BarcodeScannerService : IBarcodeScannerService
{
    private readonly IServiceProvider _serviceProvider;

    public BarcodeScannerService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<string?> ScanAsync()
    {
        var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
        if (status != PermissionStatus.Granted)
        {
            status = await Permissions.RequestAsync<Permissions.Camera>();
            if (status != PermissionStatus.Granted)
            {
                await Shell.Current.DisplayAlert(
                    "Permission Denied",
                    "Camera permission is required to scan barcodes. Please enable it in settings.",
                    "OK");
                return null;
            }
        }

        var scannerPage = _serviceProvider.GetRequiredService<BarcodeScannerPage>();
        await Shell.Current.Navigation.PushModalAsync(scannerPage);
        return await scannerPage.GetScanResultAsync();
    }
}
