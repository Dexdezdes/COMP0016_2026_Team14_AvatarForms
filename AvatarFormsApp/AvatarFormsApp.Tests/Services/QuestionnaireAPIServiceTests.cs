using System.Net;
using System.Net.Http.Json;
using AvatarFormsApp.Contracts.Services;
using AvatarFormsApp.DTOs;
using AvatarFormsApp.Models;
using AvatarFormsApp.Services;
using Moq;
using Moq.Protected;
using Xunit;

namespace AvatarFormsApp.Tests.Services;

public class QuestionnaireAPIServiceTests
{
    private readonly Mock<IQuestionnaireService> _mockInternalService;
    private readonly Mock<HttpMessageHandler> _mockHandler;
    private readonly QuestionnaireAPIService _sut;

    public QuestionnaireAPIServiceTests()
    {
        _mockInternalService = new Mock<IQuestionnaireService>();
        _mockHandler = new Mock<HttpMessageHandler>();

        // Wrap the mocked handler in a real HttpClient
        var httpClient = new HttpClient(_mockHandler.Object);

        // This requires InternalsVisibleTo in the main csproj
        _sut = new QuestionnaireAPIService(_mockInternalService.Object, httpClient);
    }

    /// <summary>
    /// Helper to create a valid Questionnaire model based on your actual schema
    /// </summary>
    private Questionnaire CreateValidQuestionnaire(string id, string name = "Test Questionnaire")
    {
        return new Questionnaire
        {
            Id = id,
            Name = name,          // Fixed: Use Name instead of Title
            OwnerId = "test-user", // Fixed: Required member
            Questions = new List<Question>()
        };
    }

    [Fact]
    public async Task SendQuestionnaireAsync_ReturnsFalse_WhenQuestionnaireNotFound()
    {
        // Arrange: Service returns null
        _mockInternalService.Setup(s => s.GetWithQuestionsAsync(It.IsAny<string>()))
            .ReturnsAsync((Questionnaire?)null);

        // Act
        var result = await _sut.SendQuestionnaireAsync("invalid-id");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task SendQuestionnaireAsync_ReturnsTrue_OnSuccessfulPost()
    {
        // Arrange
        var qId = "test-q";
        var questionnaire = CreateValidQuestionnaire(qId);

        _mockInternalService.Setup(s => s.GetWithQuestionsAsync(qId)).ReturnsAsync(questionnaire);

        // Mock the HTTP Response to be 200 OK
        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

        // Act
        var result = await _sut.SendQuestionnaireAsync(qId, 8882);

        // Assert
        Assert.True(result);

        // Verify the URL and Method were correct
        _mockHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Post &&
                req.RequestUri!.ToString() == "http://localhost:8882/questionnaire"),
            ItExpr.IsAny<CancellationToken>()
        );
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task SendQuestionnaireAsync_ReturnsFalse_OnServerError(HttpStatusCode statusCode)
    {
        // Arrange
        var qId = "err-test";
        _mockInternalService.Setup(s => s.GetWithQuestionsAsync(It.IsAny<string>()))
            .ReturnsAsync(CreateValidQuestionnaire(qId));

        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = statusCode });

        // Act
        var result = await _sut.SendQuestionnaireAsync(qId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task SendQuestionnaireAsync_ReturnsFalse_OnConnectionException()
    {
        // Arrange
        var qId = "exception-test";
        _mockInternalService.Setup(s => s.GetWithQuestionsAsync(It.IsAny<string>()))
            .ReturnsAsync(CreateValidQuestionnaire(qId));

        // Simulate a network crash/timeout
        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network down"));

        // Act
        var result = await _sut.SendQuestionnaireAsync(qId);

        // Assert
        Assert.False(result);
    }
}
