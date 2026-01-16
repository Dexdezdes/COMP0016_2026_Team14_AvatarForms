using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.IO;
using AvatarFormsApp.Helpers;

#if WINDOWS
using WinUIEx;
using Windows.UI.ViewManagement;
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
#if WINDOWS
    private Microsoft.UI.Dispatching.DispatcherQueue dispatcherQueue;
    private UISettings settings;
#endif

    public MainWindow()
    {
        this.InitializeComponent();

    #if WINDOWS
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets/WindowIcon.ico"));
        Title = "AppDisplayName".GetLocalized();

        dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        settings = new UISettings();
    #endif

    }

}
