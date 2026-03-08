using AvatarFormsApp.Contracts.Services;
using AvatarFormsApp.Views;
using AvatarFormsApp.Messages;
using AvatarFormsApp.Models;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;

using Microsoft.UI.Xaml.Navigation;
using System.Collections.ObjectModel;

namespace AvatarFormsApp.ViewModels;

public partial class ShellPageViewModel : ObservableRecipient
{
    private readonly IQuestionnaireService _questionnaireService;

    [ObservableProperty]
    private bool isBackEnabled;

    [ObservableProperty]
    private object? selected;

    public INavigationService NavigationService { get; }
    public INavigationViewService NavigationViewService { get; }

    public ObservableCollection<Questionnaire> AvailableQuestionnaires { get; } = new ObservableCollection<Questionnaire>();

    public ShellPageViewModel(
        INavigationService navigationService, 
        INavigationViewService navigationViewService,
        IQuestionnaireService questionnaireService)
    {
        NavigationService = navigationService;
        NavigationService.Navigated += OnNavigated;
        NavigationViewService = navigationViewService;
        _questionnaireService = questionnaireService;

        IsActive = true;
        _ = LoadQuestionnairesAsync();
    }

    protected override void OnActivated()
    {
        Messenger.Register<QuestionnaireCreatedMessage>(this, (r, msg) =>
            _ = ((ShellPageViewModel)r).RefreshQuestionnairesAsync());
    }

    private async Task LoadQuestionnairesAsync()
    {
        try
        {
            var questionnaires = await _questionnaireService.GetAllAsync();
            
            AvailableQuestionnaires.Clear();
            foreach (var questionnaire in questionnaires)
            {
                AvailableQuestionnaires.Add(questionnaire);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading questionnaires for navigation: {ex.Message}");
        }
    }

    private void OnNavigated(object sender, NavigationEventArgs e)
    {
        IsBackEnabled = NavigationService.CanGoBack;
        var selectedItem = NavigationViewService.GetSelectedItem(e.SourcePageType);
        if (selectedItem != null)
        {
            Selected = selectedItem;
        }
    }

    // Call this method when a new questionnaire is added to refresh the list
    public async Task RefreshQuestionnairesAsync()
    {
        await LoadQuestionnairesAsync();
    }
}
