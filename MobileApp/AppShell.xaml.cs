using MobileApp.Views;

namespace MobileApp
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            
            // Register routes for navigation
            Routing.RegisterRoute(nameof(SplashScreen), typeof(SplashScreen));
        }
    }
}
