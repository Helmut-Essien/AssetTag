using CommunityToolkit.Mvvm.ComponentModel;

namespace MobileApp.ViewModels
{
    /// <summary>
    /// Base class for all ViewModels providing common functionality
    /// </summary>
    public partial class BaseViewModel : ObservableObject
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNotBusy))]
        private bool isBusy;

        [ObservableProperty]
        private string title = string.Empty;

        public bool IsNotBusy => !IsBusy;
    }
}