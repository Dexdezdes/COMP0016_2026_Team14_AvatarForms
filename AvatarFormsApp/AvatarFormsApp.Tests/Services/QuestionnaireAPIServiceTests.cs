using System.Net;
using AvatarFormsApp.Contracts.Services;
using AvatarFormsApp.Models;
using AvatarFormsApp.Services;
using Moq;
using Moq.Protected;

using Xunit;

namespace AvatarFormsApp.Tests.Services;

public class QuestionnaireAPIServiceTests
{
    private readonly Mock<IQuestionnaireService> _mockService = new();

    private HttpClient CreateMockClient(HttpStatusCode statusCode, bool throwException = false)
    {
        var handler = new Mock<HttpMessageHandler>();
        if (throwException)
        {
            handler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("Test exception"));
        }
        else
        {
            handler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(statusCode));
        }

        return new HttpClient(handler.Object) { Timeout = TimeSpan.FromSeconds(3) };
    }

    [Fact]
    public async Task SendQuestionnaireAsync_ReturnsFalse_WhenQuestionnaireNotFound()
    {
        // Arrange
        _mockService.Setup(s => s.GetWithQuestionsAsync(It.IsAny<string>()))
            .ReturnsAsync((Questionnaire)null!);
        var sut = new QuestionnaireAPIService(_mockService.Object);

        // Act
        var result = await sut.SendQuestionnaireAsync("id", 8882);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task SendQuestionnaireAsync_ReturnsTrue_WhenPostSucceeds()
    {
        // Arrange
        var q = new Questionnaire { Name = "Test Title", OwnerId = "owner-1", Id = "id-123", Questions = new() };
        _mockService.Setup(s => s.GetWithQuestionsAsync("id-123"))
            .ReturnsAsync(q);
        var client = CreateMockClient(HttpStatusCode.OK);
        var sut = new QuestionnaireAPIService(_mockService.Object, client);

        // Act
        var result = await sut.SendQuestionnaireAsync("id-123", 8882);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task SendQuestionnaireAsync_ReturnsFalse_WhenPostStatusNotSuccess()
    {
        // Arrange
        var q = new Questionnaire { Name = "Test Title", OwnerId = "owner-1", Id = "id-123", Questions = new() };
        _mockService.Setup(s => s.GetWithQuestionsAsync("id-123"))
            .ReturnsAsync(q);
        var client = CreateMockClient(HttpStatusCode.BadRequest);
        var sut = new QuestionnaireAPIService(_mockService.Object, client);

        // Act
        var result = await sut.SendQuestionnaireAsync("id-123", 8882);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task SendQuestionnaireAsync_ReturnsFalse_WhenExceptionThrown()
    {
        // Arrange
        var q = new Questionnaire { Name = "Test Title", OwnerId = "owner-1", Id = "id-123", Questions = new() };
        _mockService.Setup(s => s.GetWithQuestionsAsync("id-123"))
            .ReturnsAsync(q);
        var client = CreateMockClient(HttpStatusCode.OK, throwException: true);
        var sut = new QuestionnaireAPIService(_mockService.Object, client);

        // Act
        var result = await sut.SendQuestionnaireAsync("id-123", 8882);

        // Assert
        Assert.False(result);
    }
}
