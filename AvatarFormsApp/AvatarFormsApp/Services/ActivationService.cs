using AvatarFormsApp.Activation;
using AvatarFormsApp.Contracts.Services;
using AvatarFormsApp.Views;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AvatarFormsApp.Services;

public class ActivationService : IActivationService
{
    private readonly ActivationHandler<LaunchActivatedEventArgs> _defaultHandler;
    private readonly IEnumerable<IActivationHandler> _activationHandlers;
    private readonly IThemeSelectorService _themeSelectorService;
    private UIElement? _shell = null;

    public ActivationService(ActivationHandler<LaunchActivatedEventArgs> defaultHandler, IEnumerable<IActivationHandler> activationHandlers, IThemeSelectorService themeSelectorService)
    {
        _defaultHandler = defaultHandler;
        _activationHandlers = activationHandlers;
        _themeSelectorService = themeSelectorService;
    }

    public async Task ActivateAsync(object activationArgs)
    {
        try
        {
            await InitializeAsync();

            var navigationService = App.GetService<INavigationService>();
            
            var frame = navigationService.Frame;

            if (frame != null)
            {
                if (frame.Content == null)
                {
                    var shell = App.GetService<ShellPage>();
                    frame.Content = shell;
                }
            }
            else
            {
                _shell = App.GetService<ShellPage>();
                App.MainWindow.Content = _shell;
            }

            await HandleActivationAsync(activationArgs);
            App.MainWindow.Activate();
            await StartupAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ACTIVATION ERROR: {ex.Message}");
            throw;
        }
    }

    private async Task HandleActivationAsync(object activationArgs)
    {
        var activationHandler = _activationHandlers.FirstOrDefault(h => h.CanHandle(activationArgs));

        if (activationHandler != null)
        {
            await activationHandler.HandleAsync(activationArgs);
        }

        if (_defaultHandler.CanHandle(activationArgs))
        {
            await _defaultHandler.HandleAsync(activationArgs);
        }
    }

    private async Task InitializeAsync()
    {
        await _themeSelectorService.InitializeAsync().ConfigureAwait(false);
        await Task.CompletedTask;
    }

    private async Task StartupAsync()
    {
        await _themeSelectorService.SetRequestedThemeAsync();
        await Task.CompletedTask;
    }
}
