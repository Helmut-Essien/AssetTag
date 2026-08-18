using MobileApp.ViewModels;

#if ANDROID
using AndroidX.Core.View;
#endif

namespace MobileApp.Views
{
    public partial class LoginPage : ContentPage
    {
        private double _imeInset;

        public LoginPage(LoginViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }

        protected override void OnHandlerChanged()
        {
            base.OnHandlerChanged();
#if ANDROID
            AttachImeListener();
#endif
        }

        protected override void OnSizeAllocated(double width, double height)
        {
            base.OnSizeAllocated(width, height);
            UpdateLoginContentMinHeight();
        }

        private void UpdateLoginContentMinHeight()
        {
            if (LoginContent == null || Height <= 0)
                return;

            LoginContent.MinimumHeightRequest = Math.Max(0, Height - _imeInset);
        }

        private void OnEmailCompleted(object? sender, EventArgs e) => PasswordEntry.Focus();

        private void OnPasswordCompleted(object? sender, EventArgs e)
        {
            if (BindingContext is LoginViewModel vm && vm.LoginCommand.CanExecute(null))
                vm.LoginCommand.Execute(null);
        }

#if ANDROID
        private void AttachImeListener()
        {
            if (Handler?.PlatformView is not Android.Views.View native)
                return;

            ViewCompat.SetOnApplyWindowInsetsListener(native, new ImeInsetsListener(OnImeInsetPx));
            ViewCompat.RequestApplyInsets(native);
        }

        private void OnImeInsetPx(int imeBottomPx)
        {
            var density = DeviceDisplay.MainDisplayInfo.Density;
            if (density <= 0)
                density = 1;

            var imeDip = imeBottomPx / density;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _imeInset = imeDip;
                Padding = new Thickness(0, 0, 0, imeDip);
                UpdateLoginContentMinHeight();
            });
        }

        private sealed class ImeInsetsListener : Java.Lang.Object, IOnApplyWindowInsetsListener
        {
            private readonly Action<int> _onIme;

            public ImeInsetsListener(Action<int> onIme) => _onIme = onIme;

            public WindowInsetsCompat OnApplyWindowInsets(Android.Views.View? v, WindowInsetsCompat? insets)
            {
                var imeBottom = insets?.GetInsets(WindowInsetsCompat.Type.Ime()).Bottom ?? 0;
                _onIme(imeBottom);
                return insets ?? new WindowInsetsCompat.Builder().Build();
            }
        }
#endif
    }
}
