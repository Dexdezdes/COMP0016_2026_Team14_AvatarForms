using AvatarFormsApp.ViewModels;

using Microsoft.UI.Xaml.Controls;
using AvatarFormsApp.Contracts.Services;
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
        // Use the NavigationService to change the content inside your ShellPage's Frame
        App.GetService<INavigationService>().NavigateTo("CreateQuestionnairePageViewModel");
    }
}
