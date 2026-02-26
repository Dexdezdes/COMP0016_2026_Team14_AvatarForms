using System.Net;
using System.Text;
using System.Text.Json;
using AvatarFormsApp.Contracts.Services;
using AvatarFormsApp.Data;
using AvatarFormsApp.DTOs;
using AvatarFormsApp.Models;
using Microsoft.EntityFrameworkCore;

namespace AvatarFormsApp.Services;

public class ResponseAPIService : IResponseAPIService
{
    private readonly IServiceProvider _serviceProvider;
    private HttpListener? _httpListener;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _listenerTask;
    private string? _currentSessionId;
    private int _expectedQuestionCount;
    private int _receivedResponseCount;

    public bool IsRunning { get; private set; }
    public event Action? AllResponsesReceived;

    public ResponseAPIService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void SetExpectedQuestionCount(int count)
    {
        _expectedQuestionCount = count;
        _receivedResponseCount = 0;
        System.Diagnostics.Debug.WriteLine($"Expected question count set to: {count}");
    }

    public async Task StartServerAsync(int port = 5000)
    {
        if (IsRunning)
        {
            System.Diagnostics.Debug.WriteLine("Response API server is already running");
            return;
        }

        try
        {
            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add($"http://localhost:{port}/");
            _httpListener.Start();
            
            _cancellationTokenSource = new CancellationTokenSource();
            IsRunning = true;

            System.Diagnostics.Debug.WriteLine($"Response API server started on port {port}");

            _listenerTask = Task.Run(() => ListenForRequestsAsync(_cancellationTokenSource.Token));

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to start Response API server: {ex.Message}");
            IsRunning = false;
            throw;
        }
    }

    public async Task StopServerAsync()
    {
        if (!IsRunning)
        {
            return;
        }

        try
        {
            _cancellationTokenSource?.Cancel();
            
            if (_listenerTask != null)
            {
                await _listenerTask;
            }

            _httpListener?.Stop();
            _httpListener?.Close();
            _httpListener = null;
            
            IsRunning = false;
            System.Diagnostics.Debug.WriteLine("Response API server stopped");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error stopping Response API server: {ex.Message}");
            throw;
        }
        finally
        {
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            _listenerTask = null;
        }
    }

    private async Task ListenForRequestsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _httpListener != null)
        {
            try
            {
                var context = await _httpListener.GetContextAsync();
                _ = Task.Run(() => HandleRequestAsync(context, cancellationToken), cancellationToken);
            }
            catch (HttpListenerException)
            {
                // Listener was stopped
                break;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in listener loop: {ex.Message}");
            }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        var request = context.Request;
        var response = context.Response;

        try
        {
            if (request.HttpMethod == "POST" && request.Url?.AbsolutePath == "/response")
            {
                await HandleResponseAsync(request, response, cancellationToken);
            }
            else
            {
                response.StatusCode = (int)HttpStatusCode.NotFound;
                await WriteResponseAsync(response, new { error = "Endpoint not found" });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error handling request: {ex.Message}");
            response.StatusCode = (int)HttpStatusCode.InternalServerError;
            await WriteResponseAsync(response, new { error = ex.Message });
        }
    }

    private async Task HandleResponseAsync(HttpListenerRequest request, HttpListenerResponse response, CancellationToken cancellationToken)
    {
        try
        {
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
            var body = await reader.ReadToEndAsync();

            System.Diagnostics.Debug.WriteLine($"Received response payload: {body}");

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var payload = JsonSerializer.Deserialize<ResponseTransferDto>(body, options);

            if (payload == null)
            {
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                await WriteResponseAsync(response, new { error = "Invalid payload" });
                return;
            }

            // Validate payload
            if (string.IsNullOrWhiteSpace(payload.QuestionnaireId) ||
                string.IsNullOrWhiteSpace(payload.Question) ||
                string.IsNullOrWhiteSpace(payload.Answer))
            {
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                await WriteResponseAsync(response, new { error = "Missing required fields" });
                return;
            }

            // Save the response to the database
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Get or create response session
            var session = await GetOrCreateSessionAsync(dbContext, payload.QuestionnaireId, cancellationToken);

            // Find the question by order and questionnaire
            var question = await dbContext.Questions
                .FirstOrDefaultAsync(q => q.QuestionnaireId == payload.QuestionnaireId && 
                                        q.Order == payload.QuestionOrder,
                                    cancellationToken);

            if (question == null)
            {
                System.Diagnostics.Debug.WriteLine($"Question not found for order {payload.QuestionOrder} in questionnaire {payload.QuestionnaireId}");
                response.StatusCode = (int)HttpStatusCode.NotFound;
                await WriteResponseAsync(response, new { error = "Question not found" });
                return;
            }

            // Create and save the response
            var responseEntity = new Models.Response
            {
                ResponseSessionId = session.Id,
                QuestionId = question.Id,
                AnswerText = payload.Answer,
                AnsweredDate = DateTime.UtcNow
            };

            dbContext.Responses.Add(responseEntity);
            await dbContext.SaveChangesAsync(cancellationToken);

            _receivedResponseCount++;
            System.Diagnostics.Debug.WriteLine($"Successfully saved response for question order {payload.QuestionOrder} ({_receivedResponseCount}/{_expectedQuestionCount})");

            // Check if all responses have been received
            if (_expectedQuestionCount > 0 && _receivedResponseCount >= _expectedQuestionCount)
            {
                // Mark session as complete
                session.IsComplete = true;
                await dbContext.SaveChangesAsync(cancellationToken);

                System.Diagnostics.Debug.WriteLine($"All responses received! Session {session.Id} marked as complete.");

                // Trigger event on a background thread to avoid blocking the HTTP response
                _ = Task.Run(() => AllResponsesReceived?.Invoke());
            }

            response.StatusCode = (int)HttpStatusCode.OK;
            await WriteResponseAsync(response, new 
            { 
                success = true,
                session_id = session.Id,
                response_id = responseEntity.Id
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error handling response: {ex.Message}");
            response.StatusCode = (int)HttpStatusCode.InternalServerError;
            await WriteResponseAsync(response, new { error = ex.Message });
        }
    }

    private async Task<ResponseSession> GetOrCreateSessionAsync(AppDbContext dbContext, string questionnaireId, CancellationToken cancellationToken)
    {
        // If we have a current session for this questionnaire, use it
        if (!string.IsNullOrEmpty(_currentSessionId))
        {
            var existingSession = await dbContext.ResponseSessions
                .FirstOrDefaultAsync(s => s.Id == _currentSessionId && s.QuestionnaireId == questionnaireId, cancellationToken);
            
            if (existingSession != null)
            {
                return existingSession;
            }
        }

        // Create a new session
        var session = new ResponseSession
        {
            QuestionnaireId = questionnaireId,
            SubmittedDate = DateTime.UtcNow,
            IsComplete = false
        };

        dbContext.ResponseSessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);

        _currentSessionId = session.Id;

        System.Diagnostics.Debug.WriteLine($"Created new response session: {session.Id}");

        return session;
    }

    private async Task WriteResponseAsync(HttpListenerResponse response, object data)
    {
        var json = JsonSerializer.Serialize(data);
        var buffer = Encoding.UTF8.GetBytes(json);
        
        response.ContentType = "application/json";
        response.ContentLength64 = buffer.Length;

        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        response.OutputStream.Close();
    }
}
