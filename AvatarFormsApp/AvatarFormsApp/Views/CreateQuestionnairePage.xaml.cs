using AvatarFormsApp.Contracts.Services;
using AvatarFormsApp.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace AvatarFormsApp.Views;

public sealed partial class CreateQuestionnairePage : Page
{
    public CreateQuestionnairePageViewModel ViewModel { get; }

    private static readonly HttpClient _http = new(new HttpClientHandler
    {
        AllowAutoRedirect = true,   // follows bit.ly and other shorteners
        MaxAutomaticRedirections = 10,
    });

    private CancellationTokenSource? _urlCheckCts;

    public CreateQuestionnairePage()
    {
        ViewModel = App.GetService<CreateQuestionnairePageViewModel>();
        InitializeComponent();
    }

    // ── Auto-parse when URL is confirmed reachable ────────────────────────────

    private async void LinkTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var text = LinkTextBox.Text.Trim();

        // Cancel any in-flight HEAD check from a previous keystroke
        _urlCheckCts?.Cancel();
        _urlCheckCts?.Dispose();
        _urlCheckCts = new CancellationTokenSource();
        var cts = _urlCheckCts;

        // Must at least look like a URL before we bother hitting the network
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            ViewModel.StatusMessage = string.Empty;
            return;
        }

        ViewModel.StatusMessage = "Checking link…";

        try
        {
            // HEAD request: no body downloaded, follows redirects, very fast.
            // 5s timeout — if a real URL doesn't respond in 5s something is wrong.
            using var req = new HttpRequestMessage(HttpMethod.Head, uri);
            using var response = await _http.SendAsync(req,
                HttpCompletionOption.ResponseHeadersRead,
                new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token);

            // If this keystroke was superseded by a newer one, bail out silently
            if (cts.IsCancellationRequested) return;

            // Any HTTP response (200, 301, 403, even 404) means a real server answered
            ViewModel.StatusMessage = string.Empty;
            ViewModel.FormLink = text;   // sync ViewModel with the resolved text

            if (!ViewModel.IsBusy)
                await ViewModel.ParseLinkAsync(HiddenWebView);
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer keystroke — do nothing
        }
        catch (HttpRequestException)
        {
            if (!cts.IsCancellationRequested)
                ViewModel.StatusMessage = "URL does not appear to be reachable.";
        }
        catch (Exception ex)
        {
            if (!cts.IsCancellationRequested)
                ViewModel.StatusMessage = $"Could not verify URL: {ex.Message}";
        }
    }

    // ── Create Questionnaire ──────────────────────────────────────────────────

    private async void Create_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.CreateQuestionnaireAsync();

        if (!ViewModel.HasParsedQuestions && !ViewModel.IsBusy)
        {
            var nav = App.GetService<INavigationService>();
            if (nav.CanGoBack) nav.GoBack();
        }
    }

    // ── File picker ───────────────────────────────────────────────────────────

    private async void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));
        picker.FileTypeFilter.Add(".csv");
        picker.FileTypeFilter.Add(".xlsx");

        var file = await picker.PickSingleFileAsync();
        PickedFileText.Text = file?.Name ?? "No file chosen.";
    }

    // ── Clear / Cancel ────────────────────────────────────────────────────────

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        _urlCheckCts?.Cancel();
        ViewModel.Clear();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        var nav = App.GetService<INavigationService>();
        if (nav.CanGoBack) nav.GoBack();
    }

    // ── Parsed question actions ───────────────────────────────────────────────

    private void RemoveQuestion_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: AvatarFormsApp.Services.ParsedQuestion question })
            ViewModel.RemoveQuestion(question);
    }

    private void RemoveOption_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: AvatarFormsApp.Services.EditableOption option }) return;

        var question = ViewModel.ParsedQuestions.FirstOrDefault(q => q.Options.Contains(option));
        if (question is not null)
            ViewModel.RemoveOption(question, option);
    }
}
