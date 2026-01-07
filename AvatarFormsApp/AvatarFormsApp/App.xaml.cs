using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace AvatarFormsApp;

public partial class App : Application
{
    protected Window? MainWindow { get; private set; }

    public App()
    {
        this.InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainWindow = new Window();

        // Create a Frame to act as the navigation context
        var rootFrame = new Frame();
        
        // Set the Frame as the content of the MainWindow
        MainWindow.Content = rootFrame;

        // Navigate to MainPage
        if (rootFrame.Content == null)
        {
            rootFrame.Navigate(typeof(MainPage), args.Arguments);
        }

        MainWindow.Activate();
    }
}