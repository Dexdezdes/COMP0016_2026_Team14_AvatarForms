using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using AvatarFormsApp.Contracts.Services;
using AvatarFormsApp.Models;
using AvatarFormsApp.Services;
using AvatarFormsApp.ViewModels;
using Xunit;

namespace AvatarFormsApp.Tests.ViewModels;

internal class FakeQuestionnaireService : IQuestionnaireService
{
    public List<Questionnaire> Saved { get; } = new();
    public bool ThrowOnAdd { get; set; } = false;

    public Task<List<Questionnaire>> GetAllAsync() => Task.FromResult(new List<Questionnaire>());
    public Task<Questionnaire?> GetByIdAsync(string id) => Task.FromResult<Questionnaire?>(null);
    public Task<Questionnaire> AddAsync(Questionnaire q)
    {
        if (ThrowOnAdd) throw new Exception("Simulated add failure");
        Saved.Add(q);
        return Task.FromResult(q);
    }
    public Task<Questionnaire> UpdateAsync(Questionnaire q) => Task.FromResult(q);
    public Task<bool> DeleteAsync(string id) => Task.FromResult(true);
    public Task<List<Questionnaire>> SearchAsync(string s) => Task.FromResult(new List<Questionnaire>());
    public Task<List<Questionnaire>> GetByStatusAsync(string s) => Task.FromResult(new List<Questionnaire>());
    public Task<List<Questionnaire>> GetByOwnerAsync(string s) => Task.FromResult(new List<Questionnaire>());
    public Task<Questionnaire?> GetWithQuestionsAsync(string id) => Task.FromResult<Questionnaire?>(null);
    public Task<Questionnaire?> GetWithResponsesAsync(string id) => Task.FromResult<Questionnaire?>(null);
    public Task<int> GetResponseCountAsync(string id) => Task.FromResult(0);
    public Task<List<ResponseSession>> GetResponseSessionsAsync(string id) => Task.FromResult(new List<ResponseSession>());
    public Task<ResponseSession?> GetResponseSessionByIdAsync(string id) => Task.FromResult<ResponseSession?>(null);
}

public class CreateQuestionnaireViewModelTests
{
    private static CreateQuestionnairePageViewModel CreateVm(FakeQuestionnaireService? svc = null)
        => new(svc ?? new FakeQuestionnaireService(), new FormLinkParserService());

    private static QuestionType InvokeMapQuestionType(string type)
    {
        var method = typeof(CreateQuestionnairePageViewModel)
            .GetMethod("MapQuestionType", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (QuestionType)method.Invoke(null, new object[] { type })!;
    }

    // ── Clear ─────────────────────────────────────────────────────────────────

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

    [Fact]
    public void Clear_OnEmptyVm_DoesNotThrow()
    {
        var vm = CreateVm();
        var ex = Record.Exception(() => vm.Clear());
        Assert.Null(ex);
    }

    // ── RemoveQuestion ────────────────────────────────────────────────────────

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
    public void RemoveQuestion_SetsHasParsedQuestionsTrue_WhenItemsRemain()
    {
        var vm = CreateVm();
        var q1 = new ParsedQuestion { Index = 1, Title = "Q1" };
        var q2 = new ParsedQuestion { Index = 2, Title = "Q2" };
        vm.ParsedQuestions.Add(q1);
        vm.ParsedQuestions.Add(q2);

        vm.RemoveQuestion(q1);

        Assert.True(vm.HasParsedQuestions);
    }

    [Fact]
    public void RemoveQuestion_DoesNothing_WhenQuestionNotInList()
    {
        var vm = CreateVm();
        vm.ParsedQuestions.Add(new ParsedQuestion { Index = 1, Title = "Q1" });

        vm.RemoveQuestion(new ParsedQuestion { Title = "NotInList" });

        Assert.Single(vm.ParsedQuestions);
    }

    [Fact]
    public void RemoveQuestion_LastItem_LeavesEmptyList()
    {
        var vm = CreateVm();
        var q = new ParsedQuestion { Index = 1, Title = "Only" };
        vm.ParsedQuestions.Add(q);

        vm.RemoveQuestion(q);

        Assert.Empty(vm.ParsedQuestions);
    }

    // ── RemoveOption ──────────────────────────────────────────────────────────

    [Fact]
    public void RemoveOption_RemovesOptionFromQuestion()
    {
        var vm = CreateVm();
        var q = new ParsedQuestion { Title = "Q1" };
        var opt = new EditableOption { Text = "A" };
        q.Options.Add(opt);
        q.Options.Add(new EditableOption { Text = "B" });

        vm.RemoveOption(q, opt);

        Assert.Single(q.Options);
        Assert.Equal("B", q.Options[0].Text);
    }

    [Fact]
    public void RemoveOption_DoesNotThrow_WhenOptionNotInList()
    {
        var vm = CreateVm();
        var q = new ParsedQuestion { Title = "Q1" };
        q.Options.Add(new EditableOption { Text = "A" });

        var ex = Record.Exception(() => vm.RemoveOption(q, new EditableOption { Text = "NotHere" }));
        Assert.Null(ex);
    }

    [Fact]
    public void RemoveOption_LeavesEmptyList_WhenLastOptionRemoved()
    {
        var vm = CreateVm();
        var q = new ParsedQuestion { Title = "Q1" };
        var opt = new EditableOption { Text = "Only" };
        q.Options.Add(opt);

        vm.RemoveOption(q, opt);

        Assert.Empty(q.Options);
    }

    // ── AddOption ─────────────────────────────────────────────────────────────

    [Fact]
    public void AddOption_AddsEmptyOptionToQuestion()
    {
        var vm = CreateVm();
        var q = new ParsedQuestion { Title = "Q1" };

        vm.AddOption(q);

        Assert.Single(q.Options);
        Assert.Equal(string.Empty, q.Options[0].Text);
    }

    [Fact]
    public void AddOption_AddsMultipleOptions()
    {
        var vm = CreateVm();
        var q = new ParsedQuestion { Title = "Q1" };

        vm.AddOption(q);
        vm.AddOption(q);
        vm.AddOption(q);

        Assert.Equal(3, q.Options.Count);
    }

    // ── AddQuestion ───────────────────────────────────────────────────────────

    [Fact]
    public void AddQuestion_McqTrue_AddsQuestionWithChoiceType()
    {
        var vm = CreateVm();

        vm.AddQuestion(isMcq: true);

        Assert.Single(vm.ParsedQuestions);
        Assert.Equal("Question.Choice", vm.ParsedQuestions[0].Type);
    }

    [Fact]
    public void AddQuestion_McqTrue_AddsOneDefaultOption()
    {
        var vm = CreateVm();

        vm.AddQuestion(isMcq: true);

        Assert.Single(vm.ParsedQuestions[0].Options);
    }

    [Fact]
    public void AddQuestion_McqFalse_AddsOpenEndedQuestion()
    {
        var vm = CreateVm();

        vm.AddQuestion(isMcq: false);

        Assert.Single(vm.ParsedQuestions);
        Assert.Equal("OpenEnded", vm.ParsedQuestions[0].Type);
    }

    [Fact]
    public void AddQuestion_McqFalse_HasNoOptions()
    {
        var vm = CreateVm();

        vm.AddQuestion(isMcq: false);

        Assert.Empty(vm.ParsedQuestions[0].Options);
    }

    [Fact]
    public void AddQuestion_SetsHasParsedQuestionsTrue()
    {
        var vm = CreateVm();

        vm.AddQuestion(isMcq: false);

        Assert.True(vm.HasParsedQuestions);
    }

    [Fact]
    public void AddQuestion_IndexesCorrectly()
    {
        var vm = CreateVm();

        vm.AddQuestion(isMcq: false);
        vm.AddQuestion(isMcq: false);

        Assert.Equal(1, vm.ParsedQuestions[0].Index);
        Assert.Equal(2, vm.ParsedQuestions[1].Index);
    }

    [Fact]
    public void AddQuestion_SetsGeneralSection()
    {
        var vm = CreateVm();

        vm.AddQuestion(isMcq: false);

        Assert.Equal("General", vm.ParsedQuestions[0].Section);
    }

    [Fact]
    public void AddQuestion_GeneratesUniqueId()
    {
        var vm = CreateVm();

        vm.AddQuestion(isMcq: false);
        vm.AddQuestion(isMcq: false);

        Assert.NotEqual(vm.ParsedQuestions[0].Id, vm.ParsedQuestions[1].Id);
    }

    // ── CreateQuestionnaireAsync — validation ─────────────────────────────────

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

    [Fact]
    public async Task CreateQuestionnaire_SetsIsBusy_FalseAfterCompletion()
    {
        var fakeService = new FakeQuestionnaireService();
        var vm = new CreateQuestionnairePageViewModel(fakeService, new FormLinkParserService());
        vm.ParsedQuestions.Add(new ParsedQuestion { Index = 1, Title = "Q1", Type = "OpenEnded" });
        vm.QuestionnaireName = "My Form";
        vm.QuestionnaireDescription = "Desc";

        await vm.CreateQuestionnaireAsync();

        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task CreateQuestionnaire_OnException_SetsErrorMessage()
    {
        var fakeService = new FakeQuestionnaireService { ThrowOnAdd = true };
        var vm = new CreateQuestionnairePageViewModel(fakeService, new FormLinkParserService());
        vm.ParsedQuestions.Add(new ParsedQuestion { Index = 1, Title = "Q1", Type = "OpenEnded" });
        vm.QuestionnaireName = "My Form";
        vm.QuestionnaireDescription = "Desc";

        await vm.CreateQuestionnaireAsync();

        Assert.Contains("error", vm.StatusMessage.ToLower());
    }

    [Fact]
    public async Task CreateQuestionnaire_OnException_IsBusy_IsFalse()
    {
        var fakeService = new FakeQuestionnaireService { ThrowOnAdd = true };
        var vm = new CreateQuestionnairePageViewModel(fakeService, new FormLinkParserService());
        vm.ParsedQuestions.Add(new ParsedQuestion { Index = 1, Title = "Q1", Type = "OpenEnded" });
        vm.QuestionnaireName = "My Form";
        vm.QuestionnaireDescription = "Desc";

        await vm.CreateQuestionnaireAsync();

        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task CreateQuestionnaire_SavesCorrectName()
    {
        var fakeService = new FakeQuestionnaireService();
        var vm = new CreateQuestionnairePageViewModel(fakeService, new FormLinkParserService());
        vm.ParsedQuestions.Add(new ParsedQuestion { Index = 1, Title = "Q1", Type = "OpenEnded" });
        vm.QuestionnaireName = "My Specific Form";
        vm.QuestionnaireDescription = "Desc";

        await vm.CreateQuestionnaireAsync();

        Assert.Equal("My Specific Form", fakeService.Saved[0].Name);
    }

    [Fact]
    public async Task CreateQuestionnaire_SavesCorrectDescription()
    {
        var fakeService = new FakeQuestionnaireService();
        var vm = new CreateQuestionnairePageViewModel(fakeService, new FormLinkParserService());
        vm.ParsedQuestions.Add(new ParsedQuestion { Index = 1, Title = "Q1", Type = "OpenEnded" });
        vm.QuestionnaireName = "Form";
        vm.QuestionnaireDescription = "My Specific Desc";

        await vm.CreateQuestionnaireAsync();

        Assert.Equal("My Specific Desc", fakeService.Saved[0].Description);
    }

    [Fact]
    public async Task CreateQuestionnaire_AssignsColor_FromPalette()
    {
        var fakeService = new FakeQuestionnaireService();
        var vm = new CreateQuestionnairePageViewModel(fakeService, new FormLinkParserService());
        vm.ParsedQuestions.Add(new ParsedQuestion { Index = 1, Title = "Q1", Type = "OpenEnded" });
        vm.QuestionnaireName = "Form";
        vm.QuestionnaireDescription = "Desc";

        await vm.CreateQuestionnaireAsync();

        Assert.False(string.IsNullOrEmpty(fakeService.Saved[0].Color));
        Assert.StartsWith("#", fakeService.Saved[0].Color);
    }

    [Fact]
    public async Task CreateQuestionnaire_ClearsQuestions_AfterSuccess()
    {
        var fakeService = new FakeQuestionnaireService();
        var vm = new CreateQuestionnairePageViewModel(fakeService, new FormLinkParserService());
        vm.ParsedQuestions.Add(new ParsedQuestion { Index = 1, Title = "Q1", Type = "OpenEnded" });
        vm.QuestionnaireName = "Form";
        vm.QuestionnaireDescription = "Desc";

        await vm.CreateQuestionnaireAsync();

        Assert.False(vm.HasParsedQuestions);
    }

    // ── MapQuestionType ───────────────────────────────────────────────────────

    [Theory]
    [InlineData("Question.Choice", QuestionType.MCQ)]
    [InlineData("Question.MultiChoice", QuestionType.MCQ)]
    [InlineData("Question.MatrixChoice", QuestionType.MCQ)]
    [InlineData("Question.Ranking", QuestionType.MCQ)]
    [InlineData("MultipleChoice", QuestionType.MCQ)]
    [InlineData("Checkboxes", QuestionType.MCQ)]
    [InlineData("Dropdown", QuestionType.MCQ)]
    [InlineData("LinearScale", QuestionType.MCQ)]
    [InlineData("Grid", QuestionType.MCQ)]
    [InlineData("OpenEnded", QuestionType.OpenEnded)]
    [InlineData("ShortText", QuestionType.OpenEnded)]
    [InlineData("Paragraph", QuestionType.OpenEnded)]
    [InlineData("Question.Text", QuestionType.OpenEnded)]
    [InlineData("unknown", QuestionType.OpenEnded)]
    [InlineData("", QuestionType.OpenEnded)]
    public void MapQuestionType_ReturnsCorrectType(string input, QuestionType expected)
    {
        Assert.Equal(expected, InvokeMapQuestionType(input));
    }

    // ── ParseLinkAsync — early return (no WebView2 needed) ────────────────────

    [Fact]
    public async Task ParseLinkAsync_SetsStatusMessage_WhenFormLinkIsEmpty()
    {
        var vm = CreateVm();
        vm.FormLink = string.Empty;

        await vm.ParseLinkAsync(null!);

        Assert.Contains("Please enter a form link", vm.StatusMessage);
    }

    [Fact]
    public async Task ParseLinkAsync_SetsStatusMessage_WhenFormLinkIsWhitespace()
    {
        var vm = CreateVm();
        vm.FormLink = "   ";

        await vm.ParseLinkAsync(null!);

        Assert.Contains("Please enter a form link", vm.StatusMessage);
    }

    [Fact]
    public async Task ParseLinkAsync_DoesNotSetIsBusy_WhenLinkIsEmpty()
    {
        var vm = CreateVm();
        vm.FormLink = string.Empty;

        await vm.ParseLinkAsync(null!);

        Assert.False(vm.IsBusy);
    }
}
