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
}
