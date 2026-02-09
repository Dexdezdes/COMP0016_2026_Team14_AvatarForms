using CommunityToolkit.Mvvm.ComponentModel;
using AvatarFormsApp.Contracts.Services;
using AvatarFormsApp.Models;
using System.Collections.ObjectModel;

namespace AvatarFormsApp.ViewModels;

public partial class QuestionnaireDetailPageViewModel : ObservableRecipient
{
    private readonly IQuestionnaireService _questionnaireService;

    [ObservableProperty]
    private Questionnaire? questionnaire;

    [ObservableProperty]
    private ObservableCollection<Question> questions = new();

    [ObservableProperty]
    private bool isLoading = false;

    [ObservableProperty]
    private string pageTitle = "Questionnaire";

    public QuestionnaireDetailPageViewModel(IQuestionnaireService questionnaireService)
    {
        _questionnaireService = questionnaireService;
    }

    public async Task LoadQuestionnaireAsync(string questionnaireId)
    {
        try
        {
            IsLoading = true;

            Questionnaire = await _questionnaireService.GetByIdAsync(questionnaireId);
            
            if (Questionnaire != null)
            {
                PageTitle = Questionnaire.Name;
                Questions = new ObservableCollection<Question>(
                    Questionnaire.Questions.OrderBy(q => q.Order)
                );
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading questionnaire: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }
}
