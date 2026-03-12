using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using AvatarFormsApp.Contracts.Services;
using AvatarFormsApp.Messages;
using AvatarFormsApp.Models;
using System.Collections.ObjectModel;

namespace AvatarFormsApp.ViewModels;

public partial class DashboardPageViewModel : ObservableRecipient
{
    private readonly IQuestionnaireService _questionnaireService;
    private List<Questionnaire> _allQuestionnaires = new();

    [ObservableProperty]
    private ObservableCollection<Questionnaire> filteredQuestionnaires = new();

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private string selectedFilter = "All Questionnaires";

    [ObservableProperty]
    private string selectedSort = "Sort by name (A-Z)";

    [ObservableProperty]
    private bool hasQuestionnaires = true;

    [ObservableProperty]
    private bool isLoading = false;

    public DashboardPageViewModel(IQuestionnaireService questionnaireService)
    {
        _questionnaireService = questionnaireService;
        _ = LoadQuestionnairesAsync(); // Only load data, don't seed
    }

    // Load questionnaires from database
    private async Task LoadQuestionnairesAsync()
    {
        try
        {
            IsLoading = true;
            
            // Get all questionnaires from database
            _allQuestionnaires = await _questionnaireService.GetAllAsync();
            
            // Apply filters and sorting
            ApplyFiltersAndSort();
        }
        catch (Exception ex)
        {
            // TODO: Show error to user
            System.Diagnostics.Debug.WriteLine($"Error loading questionnaires: {ex.Message}");
            _allQuestionnaires = new List<Questionnaire>();
            FilteredQuestionnaires = new ObservableCollection<Questionnaire>();
            HasQuestionnaires = false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    // Called when search text changes
    partial void OnSearchTextChanged(string value)
    {
        ApplyFiltersAndSort();
    }

    // Called when filter selection changes
    partial void OnSelectedFilterChanged(string value)
    {
        ApplyFiltersAndSort();
    }

    // Called when sort selection changes
    partial void OnSelectedSortChanged(string value)
    {
        ApplyFiltersAndSort();
    }

    // Apply all filters and sorting
    private void ApplyFiltersAndSort()
    {
        var filtered = _allQuestionnaires.AsEnumerable();

        // Apply status filter
        if (SelectedFilter == "Pending")
        {
            filtered = filtered.Where(q => q.Status == "Pending");
        }
        else if (SelectedFilter == "Closed")
        {
            filtered = filtered.Where(q => q.Status == "Closed");
        }
        // "All Questionnaires" - no filter needed

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            filtered = filtered.Where(q => 
                q.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }

        // Apply sorting
        if (SelectedSort == "Sort by name (A-Z)")
        {
            filtered = filtered.OrderBy(q => q.Name);
        }
        else if (SelectedSort == "Sort by name (Z-A)")
        {
            filtered = filtered.OrderByDescending(q => q.Name);
        }
        else if (SelectedSort == "Sort by responses")
        {
            // Get response counts for each questionnaire
            filtered = filtered.OrderByDescending(q => GetResponseCountForQuestionnaire(q.Id));
        }
        else
        {
            // Default: most recent first
            filtered = filtered.OrderByDescending(q => q.CreatedDate);
        }

        var result = filtered.ToList();
        FilteredQuestionnaires = new ObservableCollection<Questionnaire>(result);
        HasQuestionnaires = result.Count > 0;
    }

    // Helper method to get response count (synchronous wrapper)
    private int GetResponseCountForQuestionnaire(string questionnaireId)
    {
        // This is a workaround for sorting - in real app you might cache this
        return _questionnaireService.GetResponseCountAsync(questionnaireId).GetAwaiter().GetResult();
    }

    // Refresh questionnaires from database
    public async Task RefreshQuestionnairesAsync()
    {
        await LoadQuestionnairesAsync();
    }

    // Add a new questionnaire
    public async Task AddQuestionnaireAsync(Questionnaire questionnaire)
    {
        try
        {
            await _questionnaireService.AddAsync(questionnaire);
            await LoadQuestionnairesAsync(); // Reload all data
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error adding questionnaire: {ex.Message}");
            throw;
        }
    }

    // Delete a questionnaire
    public async Task DeleteQuestionnaireAsync(string questionnaireId)
    {
        try
        {
            await _questionnaireService.DeleteAsync(questionnaireId);
            await LoadQuestionnairesAsync();
            Messenger.Send(new QuestionnaireDeletedMessage());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error deleting questionnaire: {ex.Message}");
            throw;
        }
    }

    // Seed sample data with questionnaires and questions
    public async Task SeedSampleDataAsync()
    {
        try
        {
            var existingCount = await _questionnaireService.GetAllAsync();
            if (existingCount.Count > 0)
                return; // Already has data

            // Sample 1: DASS Questionnaire
            var dass = new Questionnaire 
            { 
                Name = "DASS Questionnaire", 
                OwnerId = "user1", 
                Status = "Pending", 
                Color = "#4CB3B3",
                Questions = new List<Question>
                {
                    new Question
                    {
                        Text = "Over the past week, how often did you feel down or hopeless?",
                        Type = QuestionType.MCQ,
                        Order = 1,
                        IsRequired = true,
                        QuestionnaireId = "", // Will be set by EF Core
                        Options = new List<QuestionOption>
                        {
                            new QuestionOption { Text = "Not at all", Order = 1, QuestionId = "" },
                            new QuestionOption { Text = "Several days", Order = 2, QuestionId = "" },
                            new QuestionOption { Text = "More than half the days", Order = 3, QuestionId = "" },
                            new QuestionOption { Text = "Nearly every day", Order = 4, QuestionId = "" }
                        }
                    },
                    new Question
                    {
                        Text = "Please describe any additional concerns you have:",
                        Type = QuestionType.OpenEnded,
                        Order = 2,
                        IsRequired = false,
                        QuestionnaireId = ""
                    }
                }
            };

            // Sample 2: Learning Experience Survey
            var learningExp = new Questionnaire 
            { 
                Name = "Learning Experience Survey", 
                OwnerId = "user1", 
                Status = "Pending", 
                Color = "#5B6DF0",
                Questions = new List<Question>
                {
                    new Question
                    {
                        Text = "How would you rate the quality of instruction?",
                        Type = QuestionType.MCQ,
                        Order = 1,
                        IsRequired = true,
                        QuestionnaireId = "",
                        Options = new List<QuestionOption>
                        {
                            new QuestionOption { Text = "Excellent", Order = 1, QuestionId = "" },
                            new QuestionOption { Text = "Good", Order = 2, QuestionId = "" },
                            new QuestionOption { Text = "Fair", Order = 3, QuestionId = "" },
                            new QuestionOption { Text = "Poor", Order = 4, QuestionId = "" }
                        }
                    },
                    new Question
                    {
                        Text = "What improvements would you suggest?",
                        Type = QuestionType.OpenEnded,
                        Order = 2,
                        IsRequired = false,
                        QuestionnaireId = ""
                    }
                }
            };

            // Sample 3: Examination Feedback Survey
            var examFeedback = new Questionnaire 
            { 
                Name = "Examination Feedback Survey", 
                OwnerId = "user1", 
                Status = "Closed", 
                Color = "#FF3B7A",
                Questions = new List<Question>
                {
                    new Question
                    {
                        Text = "The examination was fair and covered course material appropriately:",
                        Type = QuestionType.MCQ,
                        Order = 1,
                        IsRequired = true,
                        QuestionnaireId = "",
                        Options = new List<QuestionOption>
                        {
                            new QuestionOption { Text = "Strongly Agree", Order = 1, QuestionId = "" },
                            new QuestionOption { Text = "Agree", Order = 2, QuestionId = "" },
                            new QuestionOption { Text = "Disagree", Order = 3, QuestionId = "" },
                            new QuestionOption { Text = "Strongly Disagree", Order = 4, QuestionId = "" }
                        }
                    },
                    new Question
                    {
                        Text = "Additional comments about the examination:",
                        Type = QuestionType.OpenEnded,
                        Order = 2,
                        IsRequired = false,
                        QuestionnaireId = ""
                    }
                }
            };

            // Sample 4: Student Health Questionnaire
            var studentHealth = new Questionnaire 
            { 
                Name = "Student Health Questionnaire", 
                OwnerId = "user1", 
                Status = "Pending", 
                Color = "#8E53E7",
                Questions = new List<Question>
                {
                    new Question
                    {
                        Text = "How many hours of sleep do you get on average per night?",
                        Type = QuestionType.MCQ,
                        Order = 1,
                        IsRequired = true,
                        QuestionnaireId = "",
                        Options = new List<QuestionOption>
                        {
                            new QuestionOption { Text = "Less than 4 hours", Order = 1, QuestionId = "" },
                            new QuestionOption { Text = "4-6 hours", Order = 2, QuestionId = "" },
                            new QuestionOption { Text = "6-8 hours", Order = 3, QuestionId = "" },
                            new QuestionOption { Text = "More than 8 hours", Order = 4, QuestionId = "" }
                        }
                    },
                    new Question
                    {
                        Text = "What health concerns would you like support with?",
                        Type = QuestionType.OpenEnded,
                        Order = 2,
                        IsRequired = false,
                        QuestionnaireId = ""
                    }
                }
            };

            // Sample 5: Year End Review
            var yearEnd = new Questionnaire 
            { 
                Name = "Year End Review", 
                OwnerId = "user1", 
                Status = "Closed", 
                Color = "#FFA500",
                Questions = new List<Question>
                {
                    new Question
                    {
                        Text = "How would you rate your overall experience this year?",
                        Type = QuestionType.MCQ,
                        Order = 1,
                        IsRequired = true,
                        QuestionnaireId = "",
                        Options = new List<QuestionOption>
                        {
                            new QuestionOption { Text = "Outstanding", Order = 1, QuestionId = "" },
                            new QuestionOption { Text = "Very Good", Order = 2, QuestionId = "" },
                            new QuestionOption { Text = "Satisfactory", Order = 3, QuestionId = "" },
                            new QuestionOption { Text = "Needs Improvement", Order = 4, QuestionId = "" }
                        }
                    },
                    new Question
                    {
                        Text = "What were your key achievements and learning experiences?",
                        Type = QuestionType.OpenEnded,
                        Order = 2,
                        IsRequired = false,
                        QuestionnaireId = ""
                    }
                }
            };

            // Add all questionnaires with their questions
            await _questionnaireService.AddAsync(dass);
            await _questionnaireService.AddAsync(learningExp);
            await _questionnaireService.AddAsync(examFeedback);
            await _questionnaireService.AddAsync(studentHealth);
            await _questionnaireService.AddAsync(yearEnd);

            System.Diagnostics.Debug.WriteLine("Sample data seeded successfully!");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error seeding data: {ex.Message}");
        }
    }
}
