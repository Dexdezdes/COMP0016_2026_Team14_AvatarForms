using System.Reflection;
using AvatarFormsApp.Contracts.Services;
using AvatarFormsApp.Models;
using AvatarFormsApp.ViewModels;
using Xunit;

namespace AvatarFormsApp.Tests;

// ── Fake service ──────────────────────────────────────────────────────────────

internal class FakeQuestionnaireServiceForResponses : IQuestionnaireService
{
    public Questionnaire? QuestionnaireToReturn { get; set; }
    public List<ResponseSession> SessionsToReturn { get; set; } = new();
    public bool ThrowOnLoad { get; set; } = false;

    public Task<Questionnaire?> GetByIdAsync(string id)
    {
        if (ThrowOnLoad) throw new Exception("Simulated failure");
        return Task.FromResult(QuestionnaireToReturn);
    }

    public Task<List<ResponseSession>> GetResponseSessionsAsync(string questionnaireId)
    {
        if (ThrowOnLoad) throw new Exception("Simulated failure");
        return Task.FromResult(SessionsToReturn);
    }

    public Task<List<Questionnaire>> GetAllAsync() => Task.FromResult(new List<Questionnaire>());
    public Task<Questionnaire> AddAsync(Questionnaire q) => Task.FromResult(q);
    public Task<Questionnaire> UpdateAsync(Questionnaire q) => Task.FromResult(q);
    public Task<bool> DeleteAsync(string id) => Task.FromResult(true);
    public Task<List<Questionnaire>> SearchAsync(string s) => Task.FromResult(new List<Questionnaire>());
    public Task<List<Questionnaire>> GetByStatusAsync(string s) => Task.FromResult(new List<Questionnaire>());
    public Task<List<Questionnaire>> GetByOwnerAsync(string s) => Task.FromResult(new List<Questionnaire>());
    public Task<Questionnaire?> GetWithQuestionsAsync(string id) => Task.FromResult<Questionnaire?>(null);
    public Task<Questionnaire?> GetWithResponsesAsync(string id) => Task.FromResult<Questionnaire?>(null);
    public Task<int> GetResponseCountAsync(string id) => Task.FromResult(0);
    public Task<ResponseSession?> GetResponseSessionByIdAsync(string id) => Task.FromResult<ResponseSession?>(null);
}

// ── Tests ─────────────────────────────────────────────────────────────────────

public class ResponsesPageViewModelTest
{
    private readonly FakeQuestionnaireServiceForResponses _fakeService = new();
    private readonly ResponsesPageViewModel _sut;

    public ResponsesPageViewModelTest()
    {
        _sut = new ResponsesPageViewModel(_fakeService);
    }

    private string InvokeEscapeCsv(string? text)
    {
        var method = typeof(ResponsesPageViewModel)
            .GetMethod("Escapecsv", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (string)method.Invoke(_sut, new object?[] { text })!;
    }

    // ── Initial state ─────────────────────────────────────────────────────────

    [Fact]
    public void InitialState_QuestionnaireName_IsEmpty()
    {
        Assert.Equal(string.Empty, _sut.QuestionnaireName);
    }

    [Fact]
    public void InitialState_QuestionnaireColor_IsDefault()
    {
        Assert.Equal("#4CB3B3", _sut.QuestionnaireColor);
    }

    [Fact]
    public void InitialState_ResponseSessions_IsEmpty()
    {
        Assert.Empty(_sut.ResponseSessions);
    }

    [Fact]
    public void InitialState_HasResponseSessions_IsFalse()
    {
        Assert.False(_sut.HasResponseSessions);
    }

    [Fact]
    public void InitialState_IsLoading_IsFalse()
    {
        Assert.False(_sut.IsLoading);
    }

    // ── LoadResponseSessionsAsync — happy path ────────────────────────────────

    [Fact]
    public async Task Load_SetsQuestionnaireName()
    {
        _fakeService.QuestionnaireToReturn = new Questionnaire { Id = "q1", Name = "My Survey", Color = "#FF0000", OwnerId = "u1" };
        _fakeService.SessionsToReturn = new List<ResponseSession>();

        await _sut.LoadResponseSessionsAsync("q1");

        Assert.Equal("My Survey", _sut.QuestionnaireName);
    }

    [Fact]
    public async Task Load_SetsQuestionnaireColor()
    {
        _fakeService.QuestionnaireToReturn = new Questionnaire { Id = "q1", Name = "Survey", Color = "#FF0000", OwnerId = "u1" };
        _fakeService.SessionsToReturn = new List<ResponseSession>();

        await _sut.LoadResponseSessionsAsync("q1");

        Assert.Equal("#FF0000", _sut.QuestionnaireColor);
    }

    [Fact]
    public async Task Load_SetsResponseSessions()
    {
        _fakeService.QuestionnaireToReturn = new Questionnaire { Id = "q1", Name = "Survey", Color = "#000", OwnerId = "u1" };
        _fakeService.SessionsToReturn = new List<ResponseSession>
        {
            new ResponseSession { Id = "s1", QuestionnaireId = "q1" },
            new ResponseSession { Id = "s2", QuestionnaireId = "q1" }
        };

        await _sut.LoadResponseSessionsAsync("q1");

        Assert.Equal(2, _sut.ResponseSessions.Count);
    }

    [Fact]
    public async Task Load_SetsHasResponseSessions_True_WhenSessionsExist()
    {
        _fakeService.QuestionnaireToReturn = new Questionnaire { Id = "q1", Name = "Survey", Color = "#000", OwnerId = "u1" };
        _fakeService.SessionsToReturn = new List<ResponseSession> { new ResponseSession { Id = "s1", QuestionnaireId = "q1" } };

        await _sut.LoadResponseSessionsAsync("q1");

        Assert.True(_sut.HasResponseSessions);
    }

    [Fact]
    public async Task Load_SetsHasResponseSessions_False_WhenNoSessions()
    {
        _fakeService.QuestionnaireToReturn = new Questionnaire { Id = "q1", Name = "Survey", Color = "#000", OwnerId = "u1" };
        _fakeService.SessionsToReturn = new List<ResponseSession>();

        await _sut.LoadResponseSessionsAsync("q1");

        Assert.False(_sut.HasResponseSessions);
    }

    [Fact]
    public async Task Load_IsLoading_IsFalse_AfterCompletion()
    {
        _fakeService.QuestionnaireToReturn = new Questionnaire { Id = "q1", Name = "Survey", Color = "#000", OwnerId = "u1" };
        _fakeService.SessionsToReturn = new List<ResponseSession>();

        await _sut.LoadResponseSessionsAsync("q1");

        Assert.False(_sut.IsLoading);
    }

    [Fact]
    public async Task Load_HandlesNullQuestionnaire_Gracefully()
    {
        _fakeService.QuestionnaireToReturn = null;
        _fakeService.SessionsToReturn = new List<ResponseSession>();

        var ex = await Record.ExceptionAsync(() => _sut.LoadResponseSessionsAsync("q1"));

        Assert.Null(ex);
    }

    [Fact]
    public async Task Load_DoesNotChangeName_WhenQuestionnaireIsNull()
    {
        _fakeService.QuestionnaireToReturn = null;
        _fakeService.SessionsToReturn = new List<ResponseSession>();

        await _sut.LoadResponseSessionsAsync("q1");

        Assert.Equal(string.Empty, _sut.QuestionnaireName);
    }

    [Fact]
    public async Task Load_CanBeCalledMultipleTimes()
    {
        _fakeService.QuestionnaireToReturn = new Questionnaire { Id = "q1", Name = "First", Color = "#000", OwnerId = "u1" };
        _fakeService.SessionsToReturn = new List<ResponseSession>();
        await _sut.LoadResponseSessionsAsync("q1");

        _fakeService.QuestionnaireToReturn = new Questionnaire { Id = "q2", Name = "Second", Color = "#111", OwnerId = "u1" };
        await _sut.LoadResponseSessionsAsync("q2");

        Assert.Equal("Second", _sut.QuestionnaireName);
    }

    // ── LoadResponseSessionsAsync — error handling ────────────────────────────

    [Fact]
    public async Task Load_OnException_HasResponseSessions_IsFalse()
    {
        _fakeService.ThrowOnLoad = true;

        await _sut.LoadResponseSessionsAsync("q1");

        Assert.False(_sut.HasResponseSessions);
    }

    [Fact]
    public async Task Load_OnException_ResponseSessions_IsEmpty()
    {
        _fakeService.ThrowOnLoad = true;

        await _sut.LoadResponseSessionsAsync("q1");

        Assert.Empty(_sut.ResponseSessions);
    }

    [Fact]
    public async Task Load_OnException_IsLoading_IsFalse()
    {
        _fakeService.ThrowOnLoad = true;

        await _sut.LoadResponseSessionsAsync("q1");

        Assert.False(_sut.IsLoading);
    }

    [Fact]
    public async Task Load_OnException_DoesNotThrow()
    {
        _fakeService.ThrowOnLoad = true;

        var ex = await Record.ExceptionAsync(() => _sut.LoadResponseSessionsAsync("q1"));

        Assert.Null(ex);
    }

    // ── EscapeCsv ─────────────────────────────────────────────────────────────

    [Fact]
    public void EscapeCsv_ReturnsEmpty_ForNull()
    {
        Assert.Equal(string.Empty, InvokeEscapeCsv(null));
    }

    [Fact]
    public void EscapeCsv_ReturnsEmpty_ForEmptyString()
    {
        Assert.Equal(string.Empty, InvokeEscapeCsv(string.Empty));
    }

    [Fact]
    public void EscapeCsv_ReturnsUnchanged_ForPlainText()
    {
        Assert.Equal("Hello World", InvokeEscapeCsv("Hello World"));
    }

    [Fact]
    public void EscapeCsv_DoublesQuotes()
    {
        Assert.Equal("say \"\"hello\"\"", InvokeEscapeCsv("say \"hello\""));
    }

    [Fact]
    public void EscapeCsv_DoublesMultipleQuotes()
    {
        Assert.Equal("\"\"a\"\",\"\"b\"\"", InvokeEscapeCsv("\"a\",\"b\""));
    }

    [Fact]
    public void EscapeCsv_HandlesTextWithNoSpecialChars()
    {
        Assert.Equal("simple text 123", InvokeEscapeCsv("simple text 123"));
    }

    [Fact]
    public void EscapeCsv_HandlesOnlyQuotes()
    {
        Assert.Equal("\"\"", InvokeEscapeCsv("\""));
    }
}
