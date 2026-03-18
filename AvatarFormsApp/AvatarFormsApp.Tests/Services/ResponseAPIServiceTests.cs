using System.Net;
using System.Net.Http.Json;
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
    private readonly int _testPort = 5005; // Using a unique port for this suite

    public ResponseAPIServiceTests()
    {
        // 1. Setup In-Memory Database
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AppDbContext(options);

        // 2. Setup Dependency Injection Mocks
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

    public async ValueTask DisposeAsync()
    {
        if (_sut.IsRunning)
        {
            await _sut.StopServerAsync();
        }
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task HandleResponse_ValidPayload_SavesToDbAndReturnsOk()
    {
        // Arrange
        int currentPort = _testPort + 1;
        await _sut.StartServerAsync(currentPort);

        var questionnaireId = "test-q-1";
        var question = new Question
        {
            Id = "question-1",
            QuestionnaireId = questionnaireId,
            Order = 1,
            Text = "How are you feeling?" // Fixed: satisfies required member
        };

        _dbContext.Questions.Add(question);
        await _dbContext.SaveChangesAsync();

        var payload = new ResponseTransferDto
        {
            QuestionnaireId = questionnaireId,
            Question = "How are you feeling?",
            Answer = "I am doing great",
            QuestionOrder = 1
        };

        using var client = new HttpClient();

        // Act
        var response = await client.PostAsJsonAsync($"http://localhost:{currentPort}/response", payload);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var savedResponse = await _dbContext.Responses.FirstOrDefaultAsync();
        Assert.NotNull(savedResponse);
        Assert.Equal("I am doing great", savedResponse.AnswerText);
        Assert.Equal("question-1", savedResponse.QuestionId);
    }

    [Fact]
    public async Task HandleResponse_TriggersAllResponsesReceived_WhenExpectedCountReached()
    {
        // Arrange
        int currentPort = _testPort + 2;
        await _sut.StartServerAsync(currentPort);
        _sut.SetExpectedQuestionCount(1);

        var questionnaireId = "test-q-2";
        _dbContext.Questions.Add(new Question
        {
            Id = "q2",
            QuestionnaireId = questionnaireId,
            Order = 1,
            Text = "Status check"
        });
        await _dbContext.SaveChangesAsync();

        bool eventFired = false;
        _sut.AllResponsesReceived += () => eventFired = true;

        var payload = new ResponseTransferDto
        {
            QuestionnaireId = questionnaireId,
            Question = "Status check",
            Answer = "Complete",
            QuestionOrder = 1
        };

        using var client = new HttpClient();

        // Act
        await client.PostAsJsonAsync($"http://localhost:{currentPort}/response", payload);

        // Give the background Task.Run a moment to trigger the event
        await Task.Delay(200);

        // Assert
        Assert.True(eventFired);
        var session = await _dbContext.ResponseSessions.FirstAsync();
        Assert.True(session.IsComplete);
    }

    [Fact]
    public async Task StartServerAsync_SetsIsRunningToTrue()
    {
        int currentPort = _testPort + 3;
        await _sut.StartServerAsync(currentPort);
        Assert.True(_sut.IsRunning);
    }
}
