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
            if (Window != null)
            {
                Window.SetStatusBarColor(Android.Graphics.Color.Rgb(26, 26, 26));
                
                // Make status bar icons light colored for dark background
                if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
                {
                    var windowInsetsController = WindowCompat.GetInsetsController(Window, Window.DecorView);
                    if (windowInsetsController != null)
                    {
                        windowInsetsController.AppearanceLightStatusBars = false;
                    }
                }
            }
        }
    }
}
