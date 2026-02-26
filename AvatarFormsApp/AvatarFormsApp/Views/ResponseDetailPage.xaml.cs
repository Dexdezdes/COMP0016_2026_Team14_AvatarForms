using AvatarFormsApp.ViewModels;
using AvatarFormsApp.Contracts.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace AvatarFormsApp.Views;

public sealed partial class ResponseDetailPage : Page
{
    public ResponseDetailPageViewModel ViewModel { get; private set; }

    public ResponseDetailPage()
    {
        InitializeComponent();
        ViewModel = App.GetService<ResponseDetailPageViewModel>();
        this.DataContext = ViewModel;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is string sessionId)
        {
            await ViewModel.LoadSessionAsync(sessionId);
        }
    }

    private void OnBackClick(object sender, RoutedEventArgs e)
    {
        App.GetService<INavigationService>().GoBack();
    }
}
