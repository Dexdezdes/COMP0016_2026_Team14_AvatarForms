using System.Net;
<<<<<<< api-tests
using System.Net.Sockets;
using System.Text.Json;
using AvatarFormsApp.Contracts.Services;
using AvatarFormsApp.DTOs;
using AvatarFormsApp.Models;
using AvatarFormsApp.Services;
using Moq;
=======
using AvatarFormsApp.Contracts.Services;
using AvatarFormsApp.Models;
using AvatarFormsApp.Services;
using Moq;
using Moq.Protected;
>>>>>>> main
using Xunit;

namespace AvatarFormsApp.Tests.Services;

<<<<<<< api-tests
public class QuestionnaireAPIServiceTests : IDisposable
{
    private readonly Mock<IQuestionnaireService> _mockInternalService;
    private readonly QuestionnaireAPIService _sut;
    private HttpListener? _listener;
    private readonly List<int> _usedPorts = new();

    public QuestionnaireAPIServiceTests()
    {
        _mockInternalService = new Mock<IQuestionnaireService>();
        _sut = new QuestionnaireAPIService(_mockInternalService.Object);
    }

    private Questionnaire CreateValidQuestionnaire(string id, int questionCount = 0)
    {
        var questions = new List<Question>();
        for (int i = 0; i < questionCount; i++)
        {
            questions.Add(new Question
            {
                Id = $"q-{i}",
                QuestionnaireId = id,
                Order = i + 1,
                Text = $"Question {i + 1}",
                Type = QuestionType.OpenEnded // Fixed: Maps to your enum
            });
        }

        return new Questionnaire
        {
            Id = id,
            Name = "Test Questionnaire",
            Description = "Test Description",
            OwnerId = "test-user",
            Questions = questions
        };
    }

    private int GetAvailablePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        _usedPorts.Add(port);
        return port;
    }

    private async Task<(string receivedJson, HttpStatusCode statusCode, string method, string path)> StartMockServer(
        int port,
        HttpStatusCode responseCode = HttpStatusCode.OK,
        int delayMs = 0)
    {
        string receivedJson = string.Empty;
        HttpStatusCode statusCode = HttpStatusCode.OK;
        string method = string.Empty;
        string path = string.Empty;

        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{port}/");
        _listener.Start();

        var listenTask = Task.Run(async () =>
        {
            var context = await _listener.GetContextAsync();
            var request = context.Request;

            method = request.HttpMethod;
            path = request.Url!.AbsolutePath;

            if (request.HasEntityBody)
            {
                using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                receivedJson = await reader.ReadToEndAsync();
            }

            if (delayMs > 0)
            {
                await Task.Delay(delayMs);
            }

            statusCode = responseCode;
            context.Response.StatusCode = (int)responseCode;
            context.Response.Close();
        });

        await listenTask;

        return (receivedJson, statusCode, method, path);
    }

    #region Happy Path Tests

    [Fact]
    public async Task SendQuestionnaireAsync_ValidQuestionnaire_SendsCorrectPayloadOverNetwork()
    {
        // Arrange
        var qId = "valid-questionnaire";
        var questionnaire = CreateValidQuestionnaire(qId, questionCount: 3);

        _mockInternalService.Setup(s => s.GetWithQuestionsAsync(qId))
            .ReturnsAsync(questionnaire);

        int port = GetAvailablePort();
        var serverTask = StartMockServer(port, HttpStatusCode.OK);

        // Act
        var result = await _sut.SendQuestionnaireAsync(qId, port);
        var serverResult = await serverTask;

        // Assert
        Assert.True(result);
        Assert.Equal(HttpStatusCode.OK, serverResult.statusCode);
        Assert.Equal("POST", serverResult.method);
        Assert.Equal("/questionnaire", serverResult.path);

        // Verify JSON payload
        Assert.NotEmpty(serverResult.receivedJson);
        var payload = JsonSerializer.Deserialize<QuestionnaireTransferDto>(
            serverResult.receivedJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(payload);
        Assert.Equal(qId, payload.QuestionnaireId); // Fixed: Check QuestionnaireId instead of Id
        Assert.Equal("Test Description", payload.Description); // Fixed: DTO uses Description, not Name
        Assert.Equal(3, payload.Questions.Count);
    }

    [Fact]
    public async Task SendQuestionnaireAsync_EmptyQuestionnaire_StillSendsSuccessfully()
    {
        // Arrange
        var qId = "empty-questionnaire";
        var questionnaire = CreateValidQuestionnaire(qId, questionCount: 0);

        _mockInternalService.Setup(s => s.GetWithQuestionsAsync(qId))
            .ReturnsAsync(questionnaire);

        int port = GetAvailablePort();
        var serverTask = StartMockServer(port);

        // Act
        var result = await _sut.SendQuestionnaireAsync(qId, port);
        await serverTask;

        // Assert
        Assert.True(result);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task SendQuestionnaireAsync_QuestionnaireNotFound_ReturnsFalseWithoutSending()
    {
        // Arrange
        _mockInternalService.Setup(s => s.GetWithQuestionsAsync(It.IsAny<string>()))
            .ReturnsAsync((Questionnaire?)null);

        // Act
        var result = await _sut.SendQuestionnaireAsync("nonexistent-id");

        // Assert
        Assert.False(result);
        _mockInternalService.Verify(s => s.GetWithQuestionsAsync("nonexistent-id"), Times.Once);
    }

    [Fact]
    public async Task SendQuestionnaireAsync_ServerReturns500_ReturnsFalse()
    {
        // Arrange
        var qId = "server-error-test";
        _mockInternalService.Setup(s => s.GetWithQuestionsAsync(qId))
            .ReturnsAsync(CreateValidQuestionnaire(qId));

        int port = GetAvailablePort();
        var serverTask = StartMockServer(port, HttpStatusCode.InternalServerError);

        // Act
        var result = await _sut.SendQuestionnaireAsync(qId, port);
        await serverTask;
=======
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
>>>>>>> main

        // Assert
        Assert.False(result);
    }

    [Fact]
<<<<<<< api-tests
    public async Task SendQuestionnaireAsync_ServerReturns400_ReturnsFalse()
    {
        // Arrange
        var qId = "bad-request-test";
        _mockInternalService.Setup(s => s.GetWithQuestionsAsync(qId))
            .ReturnsAsync(CreateValidQuestionnaire(qId));

        int port = GetAvailablePort();
        var serverTask = StartMockServer(port, HttpStatusCode.BadRequest);

        // Act
        var result = await _sut.SendQuestionnaireAsync(qId, port);
        await serverTask;

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task SendQuestionnaireAsync_ServerReturns404_ReturnsFalse()
    {
        // Arrange
        var qId = "not-found-test";
        _mockInternalService.Setup(s => s.GetWithQuestionsAsync(qId))
            .ReturnsAsync(CreateValidQuestionnaire(qId));

        int port = GetAvailablePort();
        var serverTask = StartMockServer(port, HttpStatusCode.NotFound);

        // Act
        var result = await _sut.SendQuestionnaireAsync(qId, port);
        await serverTask;
=======
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
>>>>>>> main

        // Assert
        Assert.False(result);
    }

    [Fact]
<<<<<<< api-tests
    public async Task SendQuestionnaireAsync_ConnectionRefused_ReturnsFalse()
    {
        // Arrange
        var qId = "connection-refused-test";
        _mockInternalService.Setup(s => s.GetWithQuestionsAsync(qId))
            .ReturnsAsync(CreateValidQuestionnaire(qId));

        // Use a port with no listener
        int deadPort = GetAvailablePort();

        // Act
        var result = await _sut.SendQuestionnaireAsync(qId, deadPort);
=======
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
>>>>>>> main

        // Assert
        Assert.False(result);
    }
<<<<<<< api-tests

    [Fact]
    public async Task SendQuestionnaireAsync_Timeout_ReturnsFalse()
    {
        // Arrange
        var qId = "timeout-test";
        _mockInternalService.Setup(s => s.GetWithQuestionsAsync(qId))
            .ReturnsAsync(CreateValidQuestionnaire(qId));

        int port = GetAvailablePort();

        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{port}/");
        _listener.Start();

        var hangingServerTask = Task.Run(async () =>
        {
            try
            {
                var context = await _listener.GetContextAsync();
                await Task.Delay(Timeout.Infinite);
            }
            catch
            {
                // Expected when listener is stopped
            }
        });

        // Act
        var result = await _sut.SendQuestionnaireAsync(qId, port);

        // Assert
        Assert.False(result);

        // Cleanup
        _listener.Stop();
    }

    #endregion

    #region Network and Protocol Tests

    [Fact]
    public async Task SendQuestionnaireAsync_UsesPostMethod()
    {
        // Arrange
        var qId = "method-test";
        _mockInternalService.Setup(s => s.GetWithQuestionsAsync(qId))
            .ReturnsAsync(CreateValidQuestionnaire(qId));

        int port = GetAvailablePort();
        var serverTask = StartMockServer(port);

        // Act
        await _sut.SendQuestionnaireAsync(qId, port);
        var result = await serverTask;

        // Assert
        Assert.Equal("POST", result.method);
    }

    [Fact]
    public async Task SendQuestionnaireAsync_UsesCorrectEndpoint()
    {
        // Arrange
        var qId = "endpoint-test";
        _mockInternalService.Setup(s => s.GetWithQuestionsAsync(qId))
            .ReturnsAsync(CreateValidQuestionnaire(qId));

        int port = GetAvailablePort();
        var serverTask = StartMockServer(port);

        // Act
        await _sut.SendQuestionnaireAsync(qId, port);
        var result = await serverTask;

        // Assert
        Assert.Equal("/questionnaire", result.path);
    }

    [Fact]
    public async Task SendQuestionnaireAsync_SendsValidJson()
    {
        // Arrange
        var qId = "json-validity-test";
        _mockInternalService.Setup(s => s.GetWithQuestionsAsync(qId))
            .ReturnsAsync(CreateValidQuestionnaire(qId, questionCount: 2));

        int port = GetAvailablePort();
        var serverTask = StartMockServer(port);

        // Act
        await _sut.SendQuestionnaireAsync(qId, port);
        var result = await serverTask;

        // Assert
        Assert.NotEmpty(result.receivedJson);

        var exception = Record.Exception(() =>
            JsonSerializer.Deserialize<QuestionnaireTransferDto>(result.receivedJson));
        Assert.Null(exception);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task SendQuestionnaireAsync_ComplexQuestionnaire_SerializesCorrectly()
    {
        // Arrange
        var questionnaire = new Questionnaire
        {
            Id = "complex-q",
            Name = "Complex Questionnaire",
            Description = "A complex survey",
            OwnerId = "owner-123",
            Questions = new List<Question>
            {
                new Question
                {
                    Id = "q1",
                    QuestionnaireId = "complex-q",
                    Order = 1,
                    Text = "What is your name?",
                    Type = QuestionType.OpenEnded // Fixed: Uses Enum
                },
                new Question
                {
                    Id = "q2",
                    QuestionnaireId = "complex-q",
                    Order = 2,
                    Text = "Choose your favorite",
                    Type = QuestionType.MCQ, // Fixed: Uses Enum
                    Options = new List<QuestionOption>
                    {
                        new QuestionOption { Id = "opt1", QuestionId = "q2", Text = "Option A", Order = 1 }, // Fixed: Text instead of OptionText
                        new QuestionOption { Id = "opt2", QuestionId = "q2", Text = "Option B", Order = 2 }  // Fixed: Text instead of OptionText
                    }
                }
            }
        };

        _mockInternalService.Setup(s => s.GetWithQuestionsAsync("complex-q"))
            .ReturnsAsync(questionnaire);

        int port = GetAvailablePort();
        var serverTask = StartMockServer(port);

        // Act
        var result = await _sut.SendQuestionnaireAsync("complex-q", port);
        var serverResult = await serverTask;

        // Assert
        Assert.True(result);

        var payload = JsonSerializer.Deserialize<QuestionnaireTransferDto>(
            serverResult.receivedJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(payload);
        Assert.Equal(2, payload.Questions.Count);

        var mcqQuestion = payload.Questions.FirstOrDefault(q => q.Type == "mcq"); // Fixed: q.Type instead of q.QuestionType
        Assert.NotNull(mcqQuestion);
        Assert.Equal(2, mcqQuestion.Options?.Count);
    }

    [Fact]
    public async Task SendQuestionnaireAsync_SpecialCharactersInText_EncodesCorrectly()
    {
        // Arrange
        var questionnaire = CreateValidQuestionnaire("special-chars");
        questionnaire.Questions.Add(new Question
        {
            Id = "special",
            QuestionnaireId = "special-chars",
            Order = 1,
            Text = "Test with \"quotes\", <html>, & ampersands, émojis 🎉",
            Type = QuestionType.OpenEnded // Fixed: Uses Enum
        });

        _mockInternalService.Setup(s => s.GetWithQuestionsAsync("special-chars"))
            .ReturnsAsync(questionnaire);

        int port = GetAvailablePort();
        var serverTask = StartMockServer(port);

        // Act
        var result = await _sut.SendQuestionnaireAsync("special-chars", port);
        var serverResult = await serverTask;

        // Assert
        Assert.True(result);

        var payload = JsonSerializer.Deserialize<QuestionnaireTransferDto>(
            serverResult.receivedJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(payload);
        Assert.Contains("quotes", payload.Questions[0].Text);
        Assert.Contains("🎉", payload.Questions[0].Text);
    }

    #endregion

    public void Dispose()
    {
        if (_listener != null && _listener.IsListening)
        {
            _listener.Stop();
            _listener.Close();
        }
    }
=======
>>>>>>> main
}
