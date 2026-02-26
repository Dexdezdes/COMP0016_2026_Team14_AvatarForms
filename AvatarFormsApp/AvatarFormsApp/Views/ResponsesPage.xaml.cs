using AvatarFormsApp.ViewModels;
using AvatarFormsApp.Contracts.Services;
using AvatarFormsApp.Models;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace AvatarFormsApp.Views;

public sealed partial class ResponsesPage : Page
{
    public ResponsesPageViewModel ViewModel { get; private set; }

    public ResponsesPage()
    {
        InitializeComponent();
        ViewModel = App.GetService<ResponsesPageViewModel>();
        this.DataContext = ViewModel;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is string questionnaireId)
        {
            await ViewModel.LoadResponseSessionsAsync(questionnaireId);
        }
    }

    private void OnSessionClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ResponseSession session)
        {
            App.GetService<INavigationService>().NavigateTo(
                typeof(ResponseDetailPageViewModel).Name,
                session.Id);
        }
    }
}
