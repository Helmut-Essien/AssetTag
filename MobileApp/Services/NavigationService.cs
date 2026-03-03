namespace MobileApp.Services
{
    /// <summary>
    /// Concrete implementation of INavigationService that handles all navigation operations.
    /// Provides a centralized, testable layer for navigation throughout the application.
    /// </summary>
    public class NavigationService : INavigationService
    {
        private readonly IServiceProvider _serviceProvider;

        public NavigationService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// Navigate to the main application tabs after successful authentication
        /// </summary>
        public async Task ShowMainTabsAsync()
        {
            if (Shell.Current is AppShell appShell)
            {
                await appShell.ShowMainTabsAsync();
            }
        }

        /// <summary>
        /// Navigate to the login page (hide tabs)
        /// </summary>
        public async Task ShowLoginAsync()
        {
            if (Shell.Current is AppShell appShell)
            {
                await appShell.ShowLoginAsync();
            }
        }

        /// <summary>
        /// Navigate to a specific route using Shell navigation
        /// </summary>
        public async Task NavigateToAsync(string route)
        {
            if (string.IsNullOrWhiteSpace(route))
            {
                throw new ArgumentException("Route cannot be null or empty", nameof(route));
            }

            await Shell.Current.GoToAsync(route);
        }

        /// <summary>
        /// Navigate back to the previous page
        /// </summary>
        public async Task GoBackAsync()
        {
            await Shell.Current.GoToAsync("..");
        }

        /// <summary>
        /// Navigate to a specific tab by route
        /// </summary>
        public async Task NavigateToTabAsync(string tabRoute)
        {
            if (string.IsNullOrWhiteSpace(tabRoute))
            {
                throw new ArgumentException("Tab route cannot be null or empty", nameof(tabRoute));
            }

            await Shell.Current.GoToAsync(tabRoute);
        }

        /// <summary>
        /// Display an alert dialog
        /// </summary>
        public async Task DisplayAlertAsync(string title, string message, string cancel)
        {
            await Shell.Current.DisplayAlert(title, message, cancel);
        }

        /// <summary>
        /// Display a confirmation dialog with Yes/No options
        /// </summary>
        public async Task<bool> DisplayConfirmAsync(string title, string message, string accept, string cancel)
        {
            return await Shell.Current.DisplayAlert(title, message, accept, cancel);
        }

        /// <summary>
        /// Display a prompt dialog for user input
        /// </summary>
        public async Task<string?> DisplayPromptAsync(
            string title, 
            string message, 
            string accept, 
            string cancel, 
            string? placeholder = null, 
            Keyboard? keyboard = null)
        {
            return await Shell.Current.DisplayPromptAsync(
                title, 
                message, 
                accept, 
                cancel, 
                placeholder, 
                keyboard: keyboard ?? Keyboard.Default);
        }
    }
}