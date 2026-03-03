namespace MobileApp.Services
{
    /// <summary>
    /// Service interface for handling all navigation operations in the application.
    /// Abstracts Shell navigation and provides a centralized, testable navigation layer.
    /// </summary>
    public interface INavigationService
    {
        /// <summary>
        /// Navigate to the main application tabs after successful authentication
        /// </summary>
        Task ShowMainTabsAsync();

        /// <summary>
        /// Navigate to the login page (hide tabs)
        /// </summary>
        Task ShowLoginAsync();

        /// <summary>
        /// Navigate to a specific route using Shell navigation
        /// </summary>
        /// <param name="route">The route to navigate to</param>
        Task NavigateToAsync(string route);

        /// <summary>
        /// Navigate back to the previous page
        /// </summary>
        Task GoBackAsync();

        /// <summary>
        /// Navigate to a specific tab by route
        /// </summary>
        /// <param name="tabRoute">The tab route (e.g., "///MainTabs/Home")</param>
        Task NavigateToTabAsync(string tabRoute);

        /// <summary>
        /// Display an alert dialog
        /// </summary>
        Task DisplayAlertAsync(string title, string message, string cancel);

        /// <summary>
        /// Display a confirmation dialog with Yes/No options
        /// </summary>
        Task<bool> DisplayConfirmAsync(string title, string message, string accept, string cancel);

        /// <summary>
        /// Display a prompt dialog for user input
        /// </summary>
        Task<string?> DisplayPromptAsync(string title, string message, string accept, string cancel, string? placeholder = null, Keyboard? keyboard = null);
    }
}