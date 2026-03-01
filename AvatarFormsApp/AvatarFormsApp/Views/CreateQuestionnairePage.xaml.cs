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

    public CreateQuestionnairePage()
    {
        ViewModel = App.GetService<CreateQuestionnairePageViewModel>();
        InitializeComponent();
    }

    // ── Upload / Parse link ───────────────────────────────────────────────────

    private async void UploadButton_Click(object sender, RoutedEventArgs e)
    {
        // Pass the hidden WebView2 to the ViewModel so the service can use it
        await ViewModel.ParseLinkAsync(HiddenWebView);
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

    private void ClearButton_Click(object sender, RoutedEventArgs e) => ViewModel.Clear();

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        var nav = App.GetService<INavigationService>();
        if (nav.CanGoBack) nav.GoBack();
    }
}
