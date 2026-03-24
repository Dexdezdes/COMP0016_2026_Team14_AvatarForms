using System.Net;
using System.Text.Json;
using AvatarFormsApp.Contracts.Services;
using AvatarFormsApp.DTOs;
using AvatarFormsApp.Models;
using AvatarFormsApp.Services;
using Moq;
using Xunit;

namespace AvatarFormsApp.Tests.Services;

public class QuestionnaireAPIServiceTests : IDisposable
{
    private readonly Mock<IQuestionnaireService> _mockInternalService;
    private readonly QuestionnaireAPIService _sut;
    private readonly HttpClient _httpClient;
    private HttpListener? _listener;

    public QuestionnaireAPIServiceTests()
    {
        // We still mock the internal DB service, because we only want to test the API outbound call
        _mockInternalService = new Mock<IQuestionnaireService>();

        // Using a REAL HttpClient now!
        _httpClient = new HttpClient();
        _sut = new QuestionnaireAPIService(_mockInternalService.Object, _httpClient);
    }

    private Questionnaire CreateValidQuestionnaire(string id)
    {
        return new Questionnaire
        {
            Id = id,
            Name = "Test Questionnaire",
            OwnerId = "test-user",
            Questions = new List<Question>()
        };
    }

    [Fact]
    public async Task SendQuestionnaireAsync_ReturnsFalse_WhenQuestionnaireNotFound()
    {
        // Arrange
        _mockInternalService.Setup(s => s.GetWithQuestionsAsync(It.IsAny<string>()))
            .ReturnsAsync((Questionnaire?)null);

        // Act
        var result = await _sut.SendQuestionnaireAsync("invalid-id");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task SendQuestionnaireAsync_ActuallySendsOverNetwork_ReturnsTrueOnSuccess()
    {
        // Arrange
        var qId = "real-network-test";
        _mockInternalService.Setup(s => s.GetWithQuestionsAsync(qId))
            .ReturnsAsync(CreateValidQuestionnaire(qId));

        int testPort = 8890;
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{testPort}/");
        _listener.Start();

        string receivedJson = string.Empty;

        // Start listening in the background
        var listenTask = Task.Run(async () =>
        {
            var context = await _listener.GetContextAsync();
            var request = context.Request;

            // Read the ACTUAL payload that crossed the network
            if (request.HasEntityBody)
            {
                using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                receivedJson = await reader.ReadToEndAsync();
            }

            // Verify the endpoint
            if (request.Url!.AbsolutePath == "/questionnaire" && request.HttpMethod == "POST")
            {
                context.Response.StatusCode = (int)HttpStatusCode.OK;
            }
            else
            {
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            }
            context.Response.Close();
        });

        // Act
        var result = await _sut.SendQuestionnaireAsync(qId, testPort);

        // Wait for the background listener to finish
        await listenTask;

        // Assert
        Assert.True(result);

        // VERIFY: Did the JSON correctly serialize and make it across the wire?
        Assert.NotEmpty(receivedJson);
        Assert.Contains(qId, receivedJson);
    }

    [Fact]
    public async Task SendQuestionnaireAsync_HandlesRealServerErrors()
    {
        // Arrange
        var qId = "error-test";
        _mockInternalService.Setup(s => s.GetWithQuestionsAsync(qId))
            .ReturnsAsync(CreateValidQuestionnaire(qId));

        int testPort = 8891;
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{testPort}/");
        _listener.Start();

        // Background listener that forces a 500 Internal Server Error
        var listenTask = Task.Run(async () =>
        {
            var context = await _listener.GetContextAsync();
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.Close();
        });

        // Act
        var result = await _sut.SendQuestionnaireAsync(qId, testPort);
        await listenTask;

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task SendQuestionnaireAsync_CatchesRealNetworkConnectionExceptions()
    {
        // Arrange
        var qId = "exception-test";
        _mockInternalService.Setup(s => s.GetWithQuestionsAsync(qId))
            .ReturnsAsync(CreateValidQuestionnaire(qId));

        // Note: We deliberately DO NOT start the HttpListener here.
        // This means there is no server listening on this port.
        int deadPort = 8899;

        // Act
        // This will throw a real HttpRequestException (Connection Refused) under the hood
        var result = await _sut.SendQuestionnaireAsync(qId, deadPort);

        // Assert
        // The try/catch in your service should catch the real network error and return false
        Assert.False(result);
    }

    public void Dispose()
    {
        // Clean up unmanaged resources to prevent memory leaks and locked ports
        _httpClient.Dispose();
        if (_listener != null && _listener.IsListening)
        {
            _listener.Stop();
            _listener.Close();
        }
    }
}
