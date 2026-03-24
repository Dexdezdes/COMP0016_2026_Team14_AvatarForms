using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AvatarFormsApp.Data;
using AvatarFormsApp.DTOs;
using AvatarFormsApp.Models;
using AvatarFormsApp.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace AvatarFormsApp.Tests.Services;

public class ResponseAPIServiceTests : IAsyncDisposable
{
    private readonly ResponseAPIService _sut;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly AppDbContext _dbContext;
    private readonly List<int> _usedPorts = new();

    public ResponseAPIServiceTests()
    {
        // Setup In-Memory Database with unique name per test instance
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AppDbContext(options);

        // Setup Dependency Injection Mocks
        _mockServiceProvider = new Mock<IServiceProvider>();

        var serviceScope = new Mock<IServiceScope>();
        serviceScope.Setup(x => x.ServiceProvider).Returns(_mockServiceProvider.Object);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(x => x.CreateScope()).Returns(serviceScope.Object);

        _mockServiceProvider.Setup(x => x.GetService(typeof(IServiceScopeFactory)))
            .Returns(scopeFactory.Object);
        _mockServiceProvider.Setup(x => x.GetService(typeof(AppDbContext)))
            .Returns(_dbContext);

        _sut = new ResponseAPIService(_mockServiceProvider.Object);
    }

    private int GetAvailablePort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        _usedPorts.Add(port);
        return port;
    }

    private async Task<Question> SeedQuestionAsync(string questionnaireId, int order, string questionType = "text")
    {
        var question = new Question
        {
            Id = $"q-{Guid.NewGuid()}",
            QuestionnaireId = questionnaireId,
            Order = order,
            Text = $"Question {order}",
            // Mapping string to Enum for compatibility
            Type = questionType.ToLower() == "mcq" ? QuestionType.MCQ : QuestionType.OpenEnded
        };

        if (questionType == "mcq")
        {
            question.Options = new List<QuestionOption>
            {
                new QuestionOption { Id = $"opt-{Guid.NewGuid()}", QuestionId = question.Id, Text = "Option A", Order = 1 },
                new QuestionOption { Id = $"opt-{Guid.NewGuid()}", QuestionId = question.Id, Text = "Option B", Order = 2 }
            };
        }

        _dbContext.Questions.Add(question);
        await _dbContext.SaveChangesAsync();
        return question;
    }

    private async Task<T?> GetResponseBodyAsync<T>(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrEmpty(content))
            return default;

        return JsonSerializer.Deserialize<T>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    #region Happy Path Tests

    [Fact]
    public async Task HandleResponse_ValidTextAnswer_SavesToDbAndReturnsOk()
    {
        // Arrange
        int port = GetAvailablePort();
        await _sut.StartServerAsync(port);

        var questionnaireId = "test-q-1";
        var question = await SeedQuestionAsync(questionnaireId, 1, "text");

        var payload = new ResponseTransferDto
        {
            QuestionnaireId = questionnaireId,
            Question = question.Text,
            Answer = "My text answer",
            QuestionOrder = 1,
            QuestionType = "text"
        };

        using var client = new HttpClient();

        // Act
        var response = await client.PostAsJsonAsync($"http://localhost:{port}/response", payload);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var responseBody = await GetResponseBodyAsync<Dictionary<string, object>>(response);
        Assert.NotNull(responseBody);
        Assert.True(responseBody.ContainsKey("success"));

        var savedResponse = await _dbContext.Responses.FirstOrDefaultAsync();
        Assert.NotNull(savedResponse);
        Assert.Equal("My text answer", savedResponse.AnswerText);
        Assert.Equal(question.Id, savedResponse.QuestionId);
    }

    [Fact]
    public async Task HandleResponse_ValidMcqAnswer_SavesSelectedOptionAsAnswer()
    {
        // Arrange
        int port = GetAvailablePort();
        await _sut.StartServerAsync(port);

        var questionnaireId = "test-q-mcq";
        var question = await SeedQuestionAsync(questionnaireId, 1, "mcq");

        var payload = new ResponseTransferDto
        {
            QuestionnaireId = questionnaireId,
            Question = question.Text,
            Answer = "ignored",  // Should be ignored for MCQ
            SelectedOption = "Option A",  // This should be used
            QuestionOrder = 1,
            QuestionType = "mcq"
        };

        using var client = new HttpClient();

        // Act
        var response = await client.PostAsJsonAsync($"http://localhost:{port}/response", payload);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var savedResponse = await _dbContext.Responses.FirstOrDefaultAsync();
        Assert.NotNull(savedResponse);
        Assert.Equal("Option A", savedResponse.AnswerText);  // Should use SelectedOption, not Answer
    }

    [Fact]
    public async Task HandleResponse_McqWithoutSelectedOption_FallsBackToAnswer()
    {
        // Arrange
        int port = GetAvailablePort();
        await _sut.StartServerAsync(port);

        var questionnaireId = "test-q-mcq-fallback";
        var question = await SeedQuestionAsync(questionnaireId, 1, "mcq");

        var payload = new ResponseTransferDto
        {
            QuestionnaireId = questionnaireId,
            Question = question.Text,
            Answer = "Fallback answer",
            SelectedOption = null,  // No selected option
            QuestionOrder = 1,
            QuestionType = "mcq"
        };

        using var client = new HttpClient();

        // Act
        var response = await client.PostAsJsonAsync($"http://localhost:{port}/response", payload);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var savedResponse = await _dbContext.Responses.FirstOrDefaultAsync();
        Assert.NotNull(savedResponse);
        Assert.Equal("Fallback answer", savedResponse.AnswerText);
    }

    [Fact]
    public async Task HandleResponse_CreatesNewSession_WhenFirstResponse()
    {
        // Arrange
        int port = GetAvailablePort();
        await _sut.StartServerAsync(port);

        var questionnaireId = "new-session-test";
        await SeedQuestionAsync(questionnaireId, 1);

        var payload = new ResponseTransferDto
        {
            QuestionnaireId = questionnaireId,
            Question = "Question 1",
            Answer = "Answer 1",
            QuestionOrder = 1
        };

        using var client = new HttpClient();

        // Act
        await client.PostAsJsonAsync($"http://localhost:{port}/response", payload);

        // Assert
        var session = await _dbContext.ResponseSessions.FirstOrDefaultAsync();
        Assert.NotNull(session);
        Assert.Equal(questionnaireId, session.QuestionnaireId);
        Assert.False(session.IsComplete);
    }

    [Fact]
    public async Task HandleResponse_ReusesSession_ForSubsequentResponses()
    {
        // Arrange
        int port = GetAvailablePort();
        await _sut.StartServerAsync(port);

        var questionnaireId = "reuse-session-test";
        await SeedQuestionAsync(questionnaireId, 1);
        await SeedQuestionAsync(questionnaireId, 2);

        using var client = new HttpClient();

        // Act - Send two responses
        await client.PostAsJsonAsync($"http://localhost:{port}/response", new ResponseTransferDto
        {
            QuestionnaireId = questionnaireId,
            Question = "Question 1",
            Answer = "Answer 1",
            QuestionOrder = 1
        });

        await client.PostAsJsonAsync($"http://localhost:{port}/response", new ResponseTransferDto
        {
            QuestionnaireId = questionnaireId,
            Question = "Question 2",
            Answer = "Answer 2",
            QuestionOrder = 2
        });

        // Assert - Should have one session with two responses
        var sessions = await _dbContext.ResponseSessions.ToListAsync();
        Assert.Single(sessions);

        var responses = await _dbContext.Responses.Where(r => r.ResponseSessionId == sessions[0].Id).ToListAsync();
        Assert.Equal(2, responses.Count);
    }

    #endregion

    #region Event and Completion Tests

    [Fact]
    public async Task HandleResponse_TriggersEvent_WhenAllResponsesReceived()
    {
        // Arrange
        int port = GetAvailablePort();
        await _sut.StartServerAsync(port);
        _sut.SetExpectedQuestionCount(2);

        var questionnaireId = "event-test";
        await SeedQuestionAsync(questionnaireId, 1);
        await SeedQuestionAsync(questionnaireId, 2);

        var eventTrigger = new TaskCompletionSource<bool>();
        _sut.AllResponsesReceived += () => eventTrigger.TrySetResult(true);

        using var client = new HttpClient();

        // Act - Send first response (not complete yet)
        await client.PostAsJsonAsync($"http://localhost:{port}/response", new ResponseTransferDto
        {
            QuestionnaireId = questionnaireId,
            Question = "Question 1",
            Answer = "Answer 1",
            QuestionOrder = 1
        });

        // Event should NOT have fired yet
        Assert.False(eventTrigger.Task.IsCompleted);

        // Send second response (should complete)
        await client.PostAsJsonAsync($"http://localhost:{port}/response", new ResponseTransferDto
        {
            QuestionnaireId = questionnaireId,
            Question = "Question 2",
            Answer = "Answer 2",
            QuestionOrder = 2
        });

        // Wait for event with timeout
        var completedTask = await Task.WhenAny(eventTrigger.Task, Task.Delay(5000));
        Assert.True(eventTrigger.Task.IsCompleted, "Event should have been triggered");

        // Verify session is marked complete
        var session = await _dbContext.ResponseSessions.FirstAsync();
        Assert.True(session.IsComplete);
    }

    [Fact]
    public async Task HandleResponse_DoesNotTriggerEvent_WhenExpectedCountNotReached()
    {
        // Arrange
        int port = GetAvailablePort();
        await _sut.StartServerAsync(port);
        _sut.SetExpectedQuestionCount(3);  // Expecting 3

        var questionnaireId = "no-event-test";
        await SeedQuestionAsync(questionnaireId, 1);

        var eventTrigger = new TaskCompletionSource<bool>();
        _sut.AllResponsesReceived += () => eventTrigger.TrySetResult(true);

        using var client = new HttpClient();

        // Act - Send only 1 of 3 expected responses
        await client.PostAsJsonAsync($"http://localhost:{port}/response", new ResponseTransferDto
        {
            QuestionnaireId = questionnaireId,
            Question = "Question 1",
            Answer = "Answer 1",
            QuestionOrder = 1
        });

        // Wait a bit to ensure event doesn't fire
        await Task.WhenAny(eventTrigger.Task, Task.Delay(500));

        // Assert - Event should NOT have fired
        Assert.False(eventTrigger.Task.IsCompleted);

        var session = await _dbContext.ResponseSessions.FirstAsync();
        Assert.False(session.IsComplete);
    }

    #endregion

    #region Validation Tests

    [Fact]
    public async Task HandleResponse_NullPayload_ReturnsBadRequest()
    {
        // Arrange
        int port = GetAvailablePort();
        await _sut.StartServerAsync(port);

        using var client = new HttpClient();
        var content = new StringContent("null", Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync($"http://localhost:{port}/response", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var errorBody = await GetResponseBodyAsync<Dictionary<string, string>>(response);
        Assert.NotNull(errorBody);
        Assert.Contains("error", errorBody.Keys);
        Assert.Contains("Invalid payload", errorBody["error"]);
    }


    [Fact]
    public async Task HandleResponse_MissingQuestionnaireId_ReturnsBadRequest()
    {
        // Arrange
        int port = GetAvailablePort();
        await _sut.StartServerAsync(port);

        var payload = new ResponseTransferDto
        {
            QuestionnaireId = "",  // Empty
            Question = "Question text",
            Answer = "Answer text",
            QuestionOrder = 1
        };

        using var client = new HttpClient();

        // Act
        var response = await client.PostAsJsonAsync($"http://localhost:{port}/response", payload);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var errorBody = await GetResponseBodyAsync<Dictionary<string, string>>(response);
        Assert.NotNull(errorBody);
        Assert.Contains("Missing required fields", errorBody["error"]);
    }

    [Fact]
    public async Task HandleResponse_MissingQuestion_ReturnsBadRequest()
    {
        // Arrange
        int port = GetAvailablePort();
        await _sut.StartServerAsync(port);

        var payload = new ResponseTransferDto
        {
            QuestionnaireId = "valid-id",
            Question = null!,  // Missing
            Answer = "Answer text",
            QuestionOrder = 1
        };

        using var client = new HttpClient();

        // Act
        var response = await client.PostAsJsonAsync($"http://localhost:{port}/response", payload);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task HandleResponse_MissingAnswer_ReturnsBadRequest()
    {
        // Arrange
        int port = GetAvailablePort();
        await _sut.StartServerAsync(port);

        var payload = new ResponseTransferDto
        {
            QuestionnaireId = "valid-id",
            Question = "Question text",
            Answer = "",  // Empty
            QuestionOrder = 1
        };

        using var client = new HttpClient();

        // Act
        var response = await client.PostAsJsonAsync($"http://localhost:{port}/response", payload);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task HandleResponse_QuestionNotFound_ReturnsNotFound()
    {
        // Arrange
        int port = GetAvailablePort();
        await _sut.StartServerAsync(port);

        var payload = new ResponseTransferDto
        {
            QuestionnaireId = "nonexistent-q",
            Question = "Question text",
            Answer = "Answer text",
            QuestionOrder = 999
        };

        using var client = new HttpClient();

        // Act
        var response = await client.PostAsJsonAsync($"http://localhost:{port}/response", payload);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var errorBody = await GetResponseBodyAsync<Dictionary<string, string>>(response);
        Assert.NotNull(errorBody);
        Assert.Contains("Question not found", errorBody["error"]);
    }

    #endregion

    #region HTTP Protocol Tests

    [Fact]
    public async Task HandleRequest_GetMethod_ReturnsNotFound()
    {
        // Arrange
        int port = GetAvailablePort();
        await _sut.StartServerAsync(port);

        using var client = new HttpClient();

        // Act
        var response = await client.GetAsync($"http://localhost:{port}/response");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task HandleRequest_WrongEndpoint_ReturnsNotFound()
    {
        // Arrange
        int port = GetAvailablePort();
        await _sut.StartServerAsync(port);

        var payload = new ResponseTransferDto
        {
            QuestionnaireId = "test",
            Question = "Q",
            Answer = "A",
            QuestionOrder = 1
        };

        using var client = new HttpClient();

        // Act
        var response = await client.PostAsJsonAsync($"http://localhost:{port}/wrong-endpoint", payload);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var errorBody = await GetResponseBodyAsync<Dictionary<string, string>>(response);
        Assert.NotNull(errorBody);
        Assert.Contains("Endpoint not found", errorBody["error"]);
    }

    [Fact]
    public async Task HandleResponse_ReturnsJsonContentType()
    {
        // Arrange
        int port = GetAvailablePort();
        await _sut.StartServerAsync(port);

        var questionnaireId = "content-type-test";
        await SeedQuestionAsync(questionnaireId, 1);

        var payload = new ResponseTransferDto
        {
            QuestionnaireId = questionnaireId,
            Question = "Question 1",
            Answer = "Answer 1",
            QuestionOrder = 1
        };

        using var client = new HttpClient();

        // Act
        var response = await client.PostAsJsonAsync($"http://localhost:{port}/response", payload);

        // Assert
        Assert.NotNull(response.Content.Headers.ContentType);
        Assert.Equal("application/json", response.Content.Headers.ContentType.MediaType);
    }

    #endregion

    #region Server Lifecycle Tests

    [Fact]
    public async Task StartServerAsync_SetsIsRunningToTrue()
    {
        int port = GetAvailablePort();
        await _sut.StartServerAsync(port);
        Assert.True(_sut.IsRunning);
    }

    [Fact]
    public async Task StartServerAsync_WhenAlreadyRunning_DoesNotThrow()
    {
        int port = GetAvailablePort();
        await _sut.StartServerAsync(port);
        var exception = await Record.ExceptionAsync(async () => await _sut.StartServerAsync(port));
        Assert.Null(exception);
        Assert.True(_sut.IsRunning);
    }

    [Fact]
    public async Task StopServerAsync_SetsIsRunningToFalse()
    {
        int port = GetAvailablePort();
        await _sut.StartServerAsync(port);
        await _sut.StopServerAsync();
        Assert.False(_sut.IsRunning);
    }

    [Fact]
    public async Task StopServerAsync_WhenNotRunning_DoesNotThrow()
    {
        var exception = await Record.ExceptionAsync(async () => await _sut.StopServerAsync());
        Assert.Null(exception);
    }

    [Fact]
    public async Task StopServerAsync_StopsAcceptingRequests()
    {
        int port = GetAvailablePort();
        await _sut.StartServerAsync(port);
        var questionnaireId = "stop-test";
        await SeedQuestionAsync(questionnaireId, 1);
        await _sut.StopServerAsync();

        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(2);
        var payload = new ResponseTransferDto { QuestionnaireId = questionnaireId, Question = "Q", Answer = "A", QuestionOrder = 1 };

        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await client.PostAsJsonAsync($"http://localhost:{port}/response", payload));
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task HandleResponse_VeryLongAnswer_SavesSuccessfully()
    {
        int port = GetAvailablePort();
        await _sut.StartServerAsync(port);
        var questionnaireId = "long-answer-test";
        await SeedQuestionAsync(questionnaireId, 1);
        var longAnswer = new string('A', 10000);

        var payload = new ResponseTransferDto { QuestionnaireId = questionnaireId, Question = "Question 1", Answer = longAnswer, QuestionOrder = 1 };
        using var client = new HttpClient();
        var response = await client.PostAsJsonAsync($"http://localhost:{port}/response", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var savedResponse = await _dbContext.Responses.FirstAsync();
        Assert.Equal(longAnswer, savedResponse.AnswerText);
    }

    [Fact]
    public async Task HandleResponse_SpecialCharacters_EncodesCorrectly()
    {
        int port = GetAvailablePort();
        await _sut.StartServerAsync(port);
        var questionnaireId = "special-chars-test";
        await SeedQuestionAsync(questionnaireId, 1);
        var specialAnswer = "Test with \"quotes\", <html>, & émojis 🎉";

        var payload = new ResponseTransferDto { QuestionnaireId = questionnaireId, Question = "Question 1", Answer = specialAnswer, QuestionOrder = 1 };
        using var client = new HttpClient();
        await client.PostAsJsonAsync($"http://localhost:{port}/response", payload);

        var savedResponse = await _dbContext.Responses.FirstAsync();
        Assert.Equal(specialAnswer, savedResponse.AnswerText);
    }

    #endregion

    public async ValueTask DisposeAsync()
    {
        if (_sut.IsRunning) await _sut.StopServerAsync();
        await _dbContext.DisposeAsync();
    }
}
