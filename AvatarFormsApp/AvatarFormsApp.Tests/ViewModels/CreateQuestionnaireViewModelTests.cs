using System.Collections.Generic;
using System.Threading.Tasks;
using AvatarFormsApp.Contracts.Services;
using AvatarFormsApp.Models;
using AvatarFormsApp.Services;
using AvatarFormsApp.ViewModels;
using Xunit;

namespace AvatarFormsApp.Tests.ViewModels;

// Fake service - no mocking framework needed
internal class FakeQuestionnaireService : IQuestionnaireService
{
    public List<Questionnaire> Saved { get; } = new();

    public Task<List<Questionnaire>> GetAllAsync() => Task.FromResult(new List<Questionnaire>());
    public Task<Questionnaire?> GetByIdAsync(string id) => Task.FromResult<Questionnaire?>(null);
    public Task<Questionnaire> AddAsync(Questionnaire q) { Saved.Add(q); return Task.FromResult(q); }
    public Task<Questionnaire> UpdateAsync(Questionnaire q) => Task.FromResult(q);
    public Task<bool> DeleteAsync(string id) => Task.FromResult(true);
    public Task<List<Questionnaire>> SearchAsync(string searchTerm) => Task.FromResult(new List<Questionnaire>());
    public Task<List<Questionnaire>> GetByStatusAsync(string status) => Task.FromResult(new List<Questionnaire>());
    public Task<List<Questionnaire>> GetByOwnerAsync(string ownerId) => Task.FromResult(new List<Questionnaire>());
    public Task<Questionnaire?> GetWithQuestionsAsync(string id) => Task.FromResult<Questionnaire?>(null);
    public Task<Questionnaire?> GetWithResponsesAsync(string id) => Task.FromResult<Questionnaire?>(null);
    public Task<int> GetResponseCountAsync(string questionnaireId) => Task.FromResult(0);
    public Task<List<ResponseSession>> GetResponseSessionsAsync(string questionnaireId) => Task.FromResult(new List<ResponseSession>());
    public Task<ResponseSession?> GetResponseSessionByIdAsync(string sessionId) => Task.FromResult<ResponseSession?>(null);
}

public class CreateQuestionnaireViewModelTests
{
    private static CreateQuestionnairePageViewModel CreateVm() =>
        new(new FakeQuestionnaireService(), new FormLinkParserService());

    // --- Clear() ---

    [Fact]
    public void Clear_ResetsAllFields()
    {
        var vm = CreateVm();
        vm.FormLink = "https://example.com";
        vm.QuestionnaireName = "Test";
        vm.QuestionnaireDescription = "Desc";
        vm.StatusMessage = "Something";
        vm.ParsedQuestions.Add(new ParsedQuestion { Title = "Q1" });

        vm.Clear();

        Assert.Equal(string.Empty, vm.FormLink);
        Assert.Equal(string.Empty, vm.QuestionnaireName);
        Assert.Equal(string.Empty, vm.QuestionnaireDescription);
        Assert.Equal(string.Empty, vm.StatusMessage);
        Assert.False(vm.HasParsedQuestions);
        Assert.Empty(vm.ParsedQuestions);
    }

    // --- RemoveQuestion() ---

    [Fact]
    public void RemoveQuestion_RemovesCorrectItem()
    {
        var vm = CreateVm();
        var q1 = new ParsedQuestion { Index = 1, Title = "Q1" };
        var q2 = new ParsedQuestion { Index = 2, Title = "Q2" };
        vm.ParsedQuestions.Add(q1);
        vm.ParsedQuestions.Add(q2);

        vm.RemoveQuestion(q1);

        Assert.Single(vm.ParsedQuestions);
        Assert.Equal("Q2", vm.ParsedQuestions[0].Title);
    }

    [Fact]
    public void RemoveQuestion_ReindexesRemainingItems()
    {
        var vm = CreateVm();
        vm.ParsedQuestions.Add(new ParsedQuestion { Index = 1, Title = "Q1" });
        vm.ParsedQuestions.Add(new ParsedQuestion { Index = 2, Title = "Q2" });
        vm.ParsedQuestions.Add(new ParsedQuestion { Index = 3, Title = "Q3" });

        vm.RemoveQuestion(vm.ParsedQuestions[0]);

        Assert.Equal(1, vm.ParsedQuestions[0].Index);
        Assert.Equal(2, vm.ParsedQuestions[1].Index);
    }

    [Fact]
    public void RemoveQuestion_SetsHasParsedQuestionsToFalse_WhenListBecomesEmpty()
    {
        var vm = CreateVm();
        var q = new ParsedQuestion { Index = 1, Title = "Q1" };
        vm.ParsedQuestions.Add(q);
        vm.HasParsedQuestions = true;

        vm.RemoveQuestion(q);

        Assert.False(vm.HasParsedQuestions);
    }

    [Fact]
    public void RemoveQuestion_DoesNothing_WhenQuestionNotInList()
    {
        var vm = CreateVm();
        vm.ParsedQuestions.Add(new ParsedQuestion { Index = 1, Title = "Q1" });

        vm.RemoveQuestion(new ParsedQuestion { Title = "NotInList" });

        Assert.Single(vm.ParsedQuestions);
    }

    // --- CreateQuestionnaireAsync() validation ---

    [Fact]
    public async Task CreateQuestionnaire_SetsStatusMessage_WhenNoParsedQuestions()
    {
        var vm = CreateVm();

        await vm.CreateQuestionnaireAsync();

        Assert.Contains("parse a form link", vm.StatusMessage);
    }

    [Fact]
    public async Task CreateQuestionnaire_SetsStatusMessage_WhenNameIsEmpty()
    {
        var vm = CreateVm();
        vm.ParsedQuestions.Add(new ParsedQuestion { Index = 1, Title = "Q1" });
        vm.QuestionnaireName = string.Empty;

        await vm.CreateQuestionnaireAsync();

        Assert.Contains("name", vm.StatusMessage.ToLower());
    }

    [Fact]
    public async Task CreateQuestionnaire_SetsStatusMessage_WhenDescriptionIsEmpty()
    {
        var vm = CreateVm();
        vm.ParsedQuestions.Add(new ParsedQuestion { Index = 1, Title = "Q1" });
        vm.QuestionnaireName = "My Form";
        vm.QuestionnaireDescription = string.Empty;

        await vm.CreateQuestionnaireAsync();

        Assert.Contains("description", vm.StatusMessage.ToLower());
    }

    [Fact]
    public async Task CreateQuestionnaire_SavesAndSetsSuccessMessage_WhenValid()
    {
        var fakeService = new FakeQuestionnaireService();
        var vm = new CreateQuestionnairePageViewModel(fakeService, new FormLinkParserService());
        vm.ParsedQuestions.Add(new ParsedQuestion { Index = 1, Title = "Q1", Type = "MultipleChoice" });
        vm.QuestionnaireName = "My Form";
        vm.QuestionnaireDescription = "A description";

        await vm.CreateQuestionnaireAsync();

        Assert.Single(fakeService.Saved);
        Assert.Contains("successfully", vm.StatusMessage.ToLower());
        Assert.False(vm.HasParsedQuestions);
    }
}
