using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.IO;
using AvatarFormsApp.Helpers;

#if WINDOWS
using WinUIEx;
#endif

namespace AvatarFormsApp.Views;

public class MainWindowBase :
#if WINDOWS
    WinUIEx.WindowEx
#else
    Microsoft.UI.Xaml.Window
#endif
{ }

public sealed partial class MainWindow : MainWindowBase
{
    public MainWindow()
    {
        this.InitializeComponent();

    #if WINDOWS
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets/WindowIcon.ico"));
        Title = "AppDisplayName".GetLocalized();

        ApplyTitleBarColorScheme();
    #endif

    }

#if WINDOWS
    private void ApplyTitleBarColorScheme()
    {
        var foreground = Windows.UI.Color.FromArgb(255, 255, 255, 255);
        var inactiveForeground = Windows.UI.Color.FromArgb(153, 255, 255, 255);
        var background = Windows.UI.Color.FromArgb(255, 32, 32, 32);
        var inactiveBackground = Windows.UI.Color.FromArgb(255, 45, 45, 45);

        AppWindow.TitleBar.ForegroundColor = foreground;
        AppWindow.TitleBar.InactiveForegroundColor = inactiveForeground;
        AppWindow.TitleBar.BackgroundColor = background;
        AppWindow.TitleBar.InactiveBackgroundColor = inactiveBackground;

        AppWindow.TitleBar.ButtonForegroundColor = foreground;
        AppWindow.TitleBar.ButtonInactiveForegroundColor = inactiveForeground;
        AppWindow.TitleBar.ButtonBackgroundColor = background;
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = inactiveBackground;
        AppWindow.TitleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(30, 255, 255, 255);
        AppWindow.TitleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(20, 255, 255, 255);
        AppWindow.TitleBar.ButtonHoverForegroundColor = background;
        AppWindow.TitleBar.ButtonPressedForegroundColor = foreground;

    }
#endif
}
