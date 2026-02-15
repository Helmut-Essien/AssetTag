using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MobileApp.ViewModels
{
    public class SplashScreenViewModel : INotifyPropertyChanged
    {
        private bool _isLoading;

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged();
            }
        }

        public SplashScreenViewModel()
        {
            IsLoading = true;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}