using AvatarFormsApp.ViewModels;
using AvatarFormsApp.Contracts.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AvatarFormsApp.Views;

public sealed partial class DashboardPage : Page
{
    public DashboardPageViewModel ViewModel { get; private set; }

    public DashboardPage()
    {
        InitializeComponent();
        
        ViewModel = App.GetService<DashboardPageViewModel>();
        this.DataContext = ViewModel;
    }

    private void OnUploadClick(object sender, RoutedEventArgs e)
    { 
        App.GetService<INavigationService>().NavigateTo("CreateQuestionnairePageViewModel");
    }

    private void OnFilterButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string filterTag)
        {
            // Update the ViewModel filter property directly
            ViewModel.SelectedFilter = filterTag;
        }
    }

    private async void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string questionnaireId)
        {
            // Show confirmation dialog
            var dialog = new ContentDialog
            {
                Title = "Delete Questionnaire",
                Content = "Are you sure you want to delete this questionnaire? This action cannot be undone.",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                try
                {
                    await ViewModel.DeleteQuestionnaireAsync(questionnaireId);
                }
                catch (Exception ex)
                {
                    // Show error dialog
                    var errorDialog = new ContentDialog
                    {
                        Title = "Error",
                        Content = $"Failed to delete questionnaire: {ex.Message}",
                        CloseButtonText = "OK",
                        XamlRoot = this.XamlRoot
                    };
                    await errorDialog.ShowAsync();
                }
            }
        }
    }
}
