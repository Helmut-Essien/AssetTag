using MobileApp.Views;

namespace MobileApp
{
    public partial class AppShell : Shell
    {
        private readonly IServiceProvider _serviceProvider;

        // Constructor injection for AppShell
        public AppShell(IServiceProvider serviceProvider)
        {
            InitializeComponent();
            _serviceProvider = serviceProvider;
            
            // Set initial content to DI-resolved SplashScreen
            var splashScreen = _serviceProvider.GetRequiredService<SplashScreen>();
            InitialContent.Content = splashScreen;
            
            // Register routes for navigation with factory methods for DI
            Routing.RegisterRoute(nameof(SplashScreen), typeof(SplashScreen));
            Routing.RegisterRoute(nameof(LoginPage), typeof(LoginPage));
            Routing.RegisterRoute(nameof(MainPage), typeof(MainPage));
        }
    }
}
