namespace MobileApp
{
    public partial class App : Application
    {
        private readonly IServiceProvider _serviceProvider;

        // Constructor injection for App
        public App(IServiceProvider serviceProvider)
        {
            InitializeComponent();
            _serviceProvider = serviceProvider;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            // Resolve AppShell from DI container
            var shell = _serviceProvider.GetRequiredService<AppShell>();
            return new Window(shell);
        }
    }
}