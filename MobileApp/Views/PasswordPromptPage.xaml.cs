namespace MobileApp.Views;

public partial class PasswordPromptPage : ContentPage
{
    private TaskCompletionSource<string?> _resultTcs = new();

    public PasswordPromptPage()
    {
        InitializeComponent();
    }

    public void Configure(string title, string message)
    {
        TitleLabel.Text = title;
        MessageLabel.Text = message;
        PasswordEntry.Text = string.Empty;
        _resultTcs = new TaskCompletionSource<string?>();
    }

    public Task<string?> GetResultAsync() => _resultTcs.Task;

    private async void OnConfirmClicked(object? sender, EventArgs e)
    {
        _resultTcs.TrySetResult(PasswordEntry.Text);
        await Navigation.PopModalAsync();
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        _resultTcs.TrySetResult(null);
        await Navigation.PopModalAsync();
    }

    protected override bool OnBackButtonPressed()
    {
        _resultTcs.TrySetResult(null);
        return base.OnBackButtonPressed();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _resultTcs.TrySetResult(null);
    }
}
