using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MobileApp.Services;
using Shared.DTOs;
using System.Collections.ObjectModel;

namespace MobileApp.ViewModels
{
    /// <summary>
    /// ViewModel for the update available page
    /// Follows .NET 9 MAUI best practices with MVVM pattern
    /// </summary>
    public partial class UpdateAvailableViewModel : BaseViewModel
    {
        private readonly IVersionCheckService _versionCheckService;
        private readonly VersionCheckResponseDto _versionInfo;

        [ObservableProperty]
        private string currentVersion = string.Empty;

        [ObservableProperty]
        private string latestVersion = string.Empty;

        [ObservableProperty]
        private string subtitle = string.Empty;

        [ObservableProperty]
        private bool isMandatory;

        [ObservableProperty]
        private bool isDownloading;

        [ObservableProperty]
        private double downloadProgress;

        [ObservableProperty]
        private string downloadProgressText = string.Empty;

        [ObservableProperty]
        private string fileSizeText = string.Empty;

        [ObservableProperty]
        private string updateButtonText = "Download Update";

        [ObservableProperty]
        private bool hasReleaseNotes;

        [ObservableProperty]
        private ObservableCollection<string> features = new();

        public bool HasFeatures => Features.Count > 0;
        public bool CanSkip => !IsMandatory;
        public bool IsNotDownloading => !IsDownloading;
        public bool ShowFileSize => !IsDownloading && _versionInfo.FileSize > 0;

        public UpdateAvailableViewModel(
            IVersionCheckService versionCheckService,
            VersionCheckResponseDto versionInfo)
        {
            _versionCheckService = versionCheckService;
            _versionInfo = versionInfo;

            Title = versionInfo.IsMandatory ? "Critical Update Required" : "Update Available";
            CurrentVersion = _versionCheckService.GetCurrentVersion();
            LatestVersion = versionInfo.LatestVersion;
            IsMandatory = versionInfo.IsMandatory;
            HasReleaseNotes = !string.IsNullOrEmpty(versionInfo.ReleaseNotesUrl);

            // Set subtitle
            Subtitle = versionInfo.IsMandatory
                ? "This update is required to continue using the app"
                : $"A new version is available";

            // Format file size
            if (versionInfo.FileSize > 0)
            {
                var sizeMB = versionInfo.FileSize / (1024.0 * 1024.0);
                FileSizeText = $"Download size: {sizeMB:F1} MB";
            }

            // Load features
            if (versionInfo.Features?.Length > 0)
            {
                foreach (var feature in versionInfo.Features)
                {
                    Features.Add(feature);
                }
            }
        }

        /// <summary>
        /// Download and install the update
        /// </summary>
        [RelayCommand]
        private async Task DownloadUpdateAsync()
        {
            if (IsDownloading) return;

            try
            {
                IsDownloading = true;
                UpdateButtonText = "Downloading...";
                DownloadProgress = 0;
                DownloadProgressText = "Starting download...";

                // Create progress reporter
                var progress = new Progress<double>(percent =>
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        DownloadProgress = percent;
                        var percentInt = (int)(percent * 100);
                        DownloadProgressText = $"{percentInt}% complete";
                    });
                });

                // Download and install
                var (success, message) = await _versionCheckService.DownloadAndInstallUpdateAsync(_versionInfo, progress);

                if (success)
                {
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        await Shell.Current.DisplayAlert(
                            "Update Ready",
                            "The update has been downloaded successfully. Please complete the installation when prompted.",
                            "OK");

                        // Close the update page
                        await Shell.Current.Navigation.PopModalAsync();
                    });
                }
                else
                {
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        await Shell.Current.DisplayAlert(
                            "Download Failed",
                            $"Failed to download update: {message}\n\nPlease check your internet connection and try again.",
                            "OK");
                    });
                }
            }
            catch (Exception ex)
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await Shell.Current.DisplayAlert(
                        "Error",
                        $"An error occurred: {ex.Message}",
                        "OK");
                });
            }
            finally
            {
                IsDownloading = false;
                UpdateButtonText = "Download Update";
                DownloadProgressText = string.Empty;
            }
        }

        /// <summary>
        /// Open release notes in browser
        /// </summary>
        [RelayCommand]
        private async Task ViewReleaseNotesAsync()
        {
            if (string.IsNullOrEmpty(_versionInfo.ReleaseNotesUrl))
                return;

            try
            {
                await Browser.OpenAsync(_versionInfo.ReleaseNotesUrl, BrowserLaunchMode.SystemPreferred);
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert(
                    "Error",
                    $"Could not open release notes: {ex.Message}",
                    "OK");
            }
        }

        /// <summary>
        /// Dismiss the update prompt (only for non-mandatory updates)
        /// </summary>
        [RelayCommand]
        private async Task RemindLaterAsync()
        {
            if (IsMandatory)
                return;

            try
            {
                await Shell.Current.Navigation.PopModalAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error dismissing update page: {ex.Message}");
            }
        }

        partial void OnIsDownloadingChanged(bool value)
        {
            OnPropertyChanged(nameof(IsNotDownloading));
            OnPropertyChanged(nameof(ShowFileSize));
        }

        partial void OnFeaturesChanged(ObservableCollection<string> value)
        {
            OnPropertyChanged(nameof(HasFeatures));
        }
    }
}