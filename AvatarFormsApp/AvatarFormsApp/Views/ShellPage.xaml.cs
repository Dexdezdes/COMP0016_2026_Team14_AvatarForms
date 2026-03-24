using AvatarFormsApp.Contracts.Services;
using AvatarFormsApp.Helpers;
using AvatarFormsApp.ViewModels;
using AvatarFormsApp.Models;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

using Windows.System;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Windows.Media.SpeechRecognition;

namespace AvatarFormsApp.Views;

public sealed partial class ShellPage : Page
{
    private readonly ILocalSettingsService _localSettingsService;
    public Frame NavigationFrameControl => NavigationFrame;
    public ShellPageViewModel ViewModel
    {
        get;
    }

    public ShellPage(ShellPageViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();

        _localSettingsService = App.GetService<ILocalSettingsService>();

        ViewModel.NavigationService.Frame = NavigationFrame;
        ViewModel.NavigationViewService.Initialize(NavigationViewControl);

        // Subscribe to questionnaire collection changes
        ViewModel.AvailableQuestionnaires.CollectionChanged += OnQuestionnairesCollectionChanged;

        // Initialize the questionnaire menu items
        UpdateQuestionnaireMenuItems();

        ViewModel.NavigationService.Navigated += ShellNavigationService_Navigated;

        _ = InitializeSettingsAsync();
    }

    private void ShellNavigationService_Navigated(object sender, Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        if (e.SourcePageType == typeof(AvatarPage))
        {
            NavigationViewControl.IsSettingsVisible = false;
        }
        else
        {
            NavigationViewControl.IsSettingsVisible = true;
            _ = InitializeSettingsAsync(); // Refresh settings state just in case it was changed
        }
    }

    private void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (NavigationFrame.Content == null)
        {
            NavigationFrame.Navigate(typeof(DashboardPage));
        }

        KeyboardAccelerators.Add(BuildKeyboardAccelerator(VirtualKey.Left, VirtualKeyModifiers.Menu));
        KeyboardAccelerators.Add(BuildKeyboardAccelerator(VirtualKey.GoBack));
    }


    private static KeyboardAccelerator BuildKeyboardAccelerator(VirtualKey key, VirtualKeyModifiers? modifiers = null)
    {
        var keyboardAccelerator = new KeyboardAccelerator() { Key = key };

        if (modifiers.HasValue)
        {
            keyboardAccelerator.Modifiers = modifiers.Value;
        }

        keyboardAccelerator.Invoked += OnKeyboardAcceleratorInvoked;

        return keyboardAccelerator;
    }

    private static void OnKeyboardAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        var navigationService = App.GetService<INavigationService>();

        var result = navigationService.GoBack();

        args.Handled = result;
    }

    private void OnItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.IsSettingsInvoked)
        {
            if (ShellSettingsOverlay != null)
            {
                ShellSettingsOverlay.Visibility = ShellSettingsOverlay.Visibility == Visibility.Visible 
                    ? Visibility.Collapsed 
                    : Visibility.Visible;
            }
            return;
        }

        if (args.InvokedItemContainer is NavigationViewItem item)
        {
            // Sub-items under Responses have a questionnaire ID as Tag (not a page key)
            if (Nav_Responses.MenuItems.Contains(item) && item.Tag is string questionnaireId)
            {
                ViewModel.NavigationService.NavigateTo(
                    typeof(ResponsesPageViewModel).Name,
                    questionnaireId);
            }
            else if (item.Tag is string pageKey)
            {
                ViewModel.NavigationService.NavigateTo(pageKey);
            }
        }
    }

    private void OnQuestionnairesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateQuestionnaireMenuItems();
    }

    private void UpdateQuestionnaireMenuItems()
    {
        Nav_Responses.MenuItems.Clear();

        foreach (var questionnaire in ViewModel.AvailableQuestionnaires)
        {
            var menuItem = new NavigationViewItem
            {
                Content = questionnaire.Name,
                Tag = questionnaire.Id
            };

            Nav_Responses.MenuItems.Add(menuItem);
        }
    }
    private async Task InitializeSettingsAsync()
    {
        var languages = SpeechRecognizer.SupportedTopicLanguages;
        if (LanguageComboBox != null)
        {
            LanguageComboBox.ItemsSource = languages;

            var savedLang = await _localSettingsService.ReadSettingAsync<string>("SelectedSpeechLanguage");
            if (!string.IsNullOrEmpty(savedLang))
            {
                var match = languages.FirstOrDefault(l => l.LanguageTag == savedLang);
                if (match != null)
                {
                    LanguageComboBox.SelectedItem = match;
                }
            }

            if (LanguageComboBox.SelectedItem == null && languages.Count > 0)
            {
                var defaultLang = languages.FirstOrDefault(l => l.LanguageTag == SpeechRecognizer.SystemSpeechLanguage.LanguageTag) ?? languages[0];
                LanguageComboBox.SelectedItem = defaultLang;
            }
        }

        var savedAvatar = await _localSettingsService.ReadSettingAsync<string>("SelectedAvatar");
        if (!string.IsNullOrEmpty(savedAvatar))
        {
            if (AvatarComboBox != null)
            {
                foreach (ComboBoxItem item in AvatarComboBox.Items)
                {
                    if (item.Tag is string tag && tag == savedAvatar)
                    {
                        AvatarComboBox.SelectedItem = item;
                        break;
                    }
                }
            }
        }
        else if (AvatarComboBox != null)
        {
            AvatarComboBox.SelectedIndex = 0;
        }

        var savedVoice = await _localSettingsService.ReadSettingAsync<string>("SelectedVoice");
        if (!string.IsNullOrEmpty(savedVoice))
        {
            if (VoiceComboBox != null)
            {
                foreach (ComboBoxItem item in VoiceComboBox.Items)
                {
                    if (item.Tag is string tag && tag == savedVoice)
                    {
                        VoiceComboBox.SelectedItem = item;
                        break;
                    }
                }
            }
        }
        else if (VoiceComboBox != null)
        {
            VoiceComboBox.SelectedIndex = 0;
        }

        var savedAutoSend = await _localSettingsService.ReadSettingAsync<bool?>("AutoSendEnabled");
        if (savedAutoSend != null && AutoSendToggle != null)
        {
            AutoSendToggle.IsOn = savedAutoSend.Value;
        }
    }

    private void OnCloseShellSettingsClicked(object sender, RoutedEventArgs e)
    {
        if (ShellSettingsOverlay != null)
        {
            ShellSettingsOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private void OnAutoSendToggled(object sender, RoutedEventArgs e)
    {
        if (AutoSendToggle != null)
        {
            _ = _localSettingsService?.SaveSettingAsync("AutoSendEnabled", AutoSendToggle.IsOn);
        }
    }

    private void OnAvatarSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox cb && cb.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            _ = _localSettingsService?.SaveSettingAsync("SelectedAvatar", tag);
        }
    }

    private void OnVoiceSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox cb && cb.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            _ = _localSettingsService?.SaveSettingAsync("SelectedVoice", tag);
        }
    }

    private void OnLanguageSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox cb && cb.SelectedItem is Windows.Globalization.Language lang)
        {
            _ = _localSettingsService?.SaveSettingAsync("SelectedSpeechLanguage", lang.LanguageTag);
        }
    }
}
