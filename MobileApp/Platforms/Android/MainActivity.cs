using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using AndroidX.Core.View;
using Plugin.Fingerprint;

namespace MobileApp
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            
            // Initialize Plugin.Fingerprint for biometric authentication
            CrossFingerprint.SetCurrentActivityResolver(() => this);
            
            // Set status bar color to match splash screen dark background (#1a1a1a)
            // Suppress warnings for Android API compatibility - we handle version checks properly
            #pragma warning disable CA1416, CA1422
            if (Window != null)
            {
                // Use modern API for Android 11+ (API 30+), fallback for older versions
                if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
                {
                    // Modern approach for Android 11+
                    Window.SetDecorFitsSystemWindows(false);
                }
                else
                {
                    // Legacy approach for Android 10 and below
                    Window.SetStatusBarColor(Android.Graphics.Color.Rgb(26, 26, 26));
                }
                
                // Set status bar icons to light color for dark background (Android 6+)
                if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
                {
                    var windowInsetsController = WindowCompat.GetInsetsController(Window, Window.DecorView);
                    if (windowInsetsController != null)
                    {
                        windowInsetsController.AppearanceLightStatusBars = false;
                    }
                }
            }
            #pragma warning restore CA1416, CA1422
        }
    }
}
