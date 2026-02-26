using AvatarFormsApp.Contracts.Services;
using AvatarFormsApp.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace AvatarFormsApp.Views;

public sealed partial class CreateQuestionnairePage : Page
{
    public CreateQuestionnairePageViewModel ViewModel { get; }

    private StorageFile? _pickedFile;

    public CreateQuestionnairePage()
    {
        ViewModel = App.GetService<CreateQuestionnairePageViewModel>();
        InitializeComponent();
    }

    // ── File picker (CSV / XLSX) ──────────────────────────────────────────────

    private async void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
        InitializeWithWindow.Initialize(picker, hwnd);

        picker.FileTypeFilter.Add(".csv");
        picker.FileTypeFilter.Add(".xlsx");

        _pickedFile = await picker.PickSingleFileAsync();
        PickedFileText.Text = _pickedFile?.Name ?? "No file chosen.";
    }

    // ── Upload / Parse link button ────────────────────────────────────────────

    private async void UploadButton_Click(object sender, RoutedEventArgs e)
    {
        // ParseLinkCommand is bound in XAML; this handler exists as a fallback
        // if you prefer code-behind over x:Bind Command.
        if (ViewModel.ParseLinkCommand.CanExecute(null))
            await ViewModel.ParseLinkCommand.ExecuteAsync(null);
    }

    // ── Create Questionnaire button ───────────────────────────────────────────

    private async void Create_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.CreateQuestionnaireCommand.CanExecute(null))
            await ViewModel.CreateQuestionnaireCommand.ExecuteAsync(null);

        // Navigate back on success (ViewModel sets HasParsedQuestions = false)
        if (!ViewModel.HasParsedQuestions && !ViewModel.IsBusy)
        {
            var navigationService = App.GetService<INavigationService>();
            if (navigationService.CanGoBack)
                navigationService.GoBack();
        }
    }

    // ── Clear / Cancel ────────────────────────────────────────────────────────

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        _pickedFile = null;
        PickedFileText.Text = string.Empty;
        ViewModel.Clear();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        var navigationService = App.GetService<INavigationService>();
        if (navigationService.CanGoBack)
            navigationService.GoBack();
    }
}
