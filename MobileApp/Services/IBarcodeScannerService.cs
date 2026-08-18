namespace MobileApp.Services;

public interface IBarcodeScannerService
{
    /// <summary>
    /// Requests camera permission, opens the scanner, and returns the scanned value (or null if cancelled).
    /// </summary>
    Task<string?> ScanAsync();
}
