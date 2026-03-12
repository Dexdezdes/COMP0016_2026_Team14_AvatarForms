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

namespace AvatarFormsApp.Views;

public sealed partial class ShellPage : Page
{
    public Frame NavigationFrameControl => NavigationFrame;
    public ShellPageViewModel ViewModel
    {
        get;
    }

    public ShellPage(ShellPageViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();

        ViewModel.NavigationService.Frame = NavigationFrame;
        ViewModel.NavigationViewService.Initialize(NavigationViewControl);

        // Subscribe to questionnaire collection changes
        ViewModel.AvailableQuestionnaires.CollectionChanged += OnQuestionnairesCollectionChanged;

        // Initialize the questionnaire menu items
        UpdateQuestionnaireMenuItems();
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
}
