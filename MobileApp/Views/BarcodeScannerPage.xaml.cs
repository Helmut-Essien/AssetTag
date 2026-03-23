using ZXing.Net.Maui;

namespace MobileApp.Views;

/// <summary>
/// High-performance barcode scanner page using ZXing.Net.Maui v0.6.0
/// Follows .NET 9 best practices with XAML-based UI and async/await patterns
/// </summary>
public partial class BarcodeScannerPage : ContentPage
{
    private TaskCompletionSource<string?> _scanResultTcs;
    private bool _isProcessing;

    public BarcodeScannerPage()
    {
        InitializeComponent();
        _scanResultTcs = new TaskCompletionSource<string?>();
    }

    /// <summary>
    /// Get the scan result asynchronously
    /// </summary>
    public Task<string?> GetScanResultAsync() => _scanResultTcs.Task;

    private void OnBarcodesDetected(object sender, BarcodeDetectionEventArgs e)
    {
        if (_isProcessing || e.Results == null || e.Results.Length == 0)
            return;

        var barcode = e.Results[0];
        if (string.IsNullOrWhiteSpace(barcode.Value))
            return;

        _isProcessing = true;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                CameraView.IsDetecting = false;

                // Provide haptic feedback
                try
                {
                    HapticFeedback.Default.Perform(HapticFeedbackType.Click);
                }
                catch
                {
                    // Haptic feedback not supported on all devices
                }

                await DisplayAlert("Success", $"Scanned: {barcode.Value}", "OK");

                _scanResultTcs.TrySetResult(barcode.Value);
                await Navigation.PopModalAsync();
            }
            catch (Exception ex)
            {
                _isProcessing = false; // Reset flag on error
                _scanResultTcs.TrySetException(ex);
                await Navigation.PopModalAsync();
            }
        });
    }

    private void OnTorchToggled(object sender, EventArgs e)
    {
        CameraView.IsTorchOn = !CameraView.IsTorchOn;
        TorchButton.Opacity = CameraView.IsTorchOn ? 1.0 : 0.5;
    }

    private async void OnCloseClicked(object sender, EventArgs e)
    {
        CameraView.IsDetecting = false;
        _scanResultTcs.TrySetResult(null);
        await Navigation.PopModalAsync();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        
        // Reset state for new scan session
        _isProcessing = false;
        _scanResultTcs = new TaskCompletionSource<string?>();
        
        CameraView.IsDetecting = true;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        CameraView.IsDetecting = false;

        if (!_scanResultTcs.Task.IsCompleted)
        {
            _scanResultTcs.TrySetResult(null);
        }
    }
}