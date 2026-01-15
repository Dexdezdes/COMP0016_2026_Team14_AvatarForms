using AvatarFormsApp.ViewModels;

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
}
