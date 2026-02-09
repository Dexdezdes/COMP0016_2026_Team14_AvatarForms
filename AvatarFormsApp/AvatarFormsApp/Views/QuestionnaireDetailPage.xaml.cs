using AvatarFormsApp.ViewModels;
using AvatarFormsApp.Contracts.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace AvatarFormsApp.Views;

public sealed partial class QuestionnaireDetailPage : Page
{
    public QuestionnaireDetailPageViewModel ViewModel { get; private set; }

    public QuestionnaireDetailPage()
    {
        InitializeComponent();
        ViewModel = App.GetService<QuestionnaireDetailPageViewModel>();
        this.DataContext = ViewModel;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is string questionnaireId)
        {
            await ViewModel.LoadQuestionnaireAsync(questionnaireId);
        }
    }

    private void OnBackClick(object sender, RoutedEventArgs e)
    {
        App.GetService<INavigationService>().NavigateTo(typeof(DashboardPageViewModel).Name);
    }
}
