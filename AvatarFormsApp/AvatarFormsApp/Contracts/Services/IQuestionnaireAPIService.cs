namespace AvatarFormsApp.Contracts.Services;

public interface IQuestionnaireAPIService
{
    Task<bool> SendQuestionnaireAsync(string questionnaireId, int port = 8882);
}
