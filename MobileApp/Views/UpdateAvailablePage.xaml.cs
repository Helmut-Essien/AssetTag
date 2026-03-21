using MobileApp.ViewModels;
using MobileApp.Services;
using Shared.DTOs;

namespace MobileApp.Views
{
    public partial class UpdateAvailablePage : ContentPage
    {
        public UpdateAvailablePage(IVersionCheckService versionCheckService, VersionCheckResponseDto versionInfo)
        {
            InitializeComponent();
            BindingContext = new UpdateAvailableViewModel(versionCheckService, versionInfo);
        }
    }
}