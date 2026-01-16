using AvatarFormsApp.ViewModels;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace AvatarFormsApp.Views;

public sealed partial class CreateQuestionnairePage : Page
{
    public CreateQuestionnairePageViewModel ViewModel
    {
        get;
    }

    private StorageFile? _pickedFile;

    public CreateQuestionnairePage()
    {
        ViewModel = App.GetService<CreateQuestionnairePageViewModel>();
        InitializeComponent();
    }

    private async void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        // Initialize picker with window handle (WinUI3 requires this)
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        InitializeWithWindow.Initialize(picker, hwnd);

        picker.FileTypeFilter.Add(".csv");
        picker.FileTypeFilter.Add(".xlsx");

        _pickedFile = await picker.PickSingleFileAsync();

        if (_pickedFile != null)
        {
            PickedFileText.Text = _pickedFile.Name;
        }
        else
        {
            PickedFileText.Text = "No file chosen.";
        }
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        _pickedFile = null;
        PickedFileText.Text = string.Empty;
        LinkTextBox.Text = string.Empty;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        var navigationService = App.GetService<AvatarFormsApp.Contracts.Services.INavigationService>();
        if (navigationService.CanGoBack)
        {
            navigationService.GoBack();
        }
    }

    private void Create_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Implement creation logic (validate inputs, upload file, save questionnaire)
        // For now just navigate back to the list page.
        var navigationService = App.GetService<AvatarFormsApp.Contracts.Services.INavigationService>();
        if (navigationService.CanGoBack)
        {
            navigationService.GoBack();
        }
    }
}