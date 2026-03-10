namespace AvatarFormsApp.Contracts.Services;

public interface IResponseAPIService
{
    Task StartServerAsync(int port = 5000);
    Task StopServerAsync();
    bool IsRunning { get; }
    void SetExpectedQuestionCount(int count);
    event Action? AllResponsesReceived;
}
