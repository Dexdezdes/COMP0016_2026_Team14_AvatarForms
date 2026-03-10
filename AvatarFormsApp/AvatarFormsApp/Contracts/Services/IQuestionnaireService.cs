using AvatarFormsApp.Models;

namespace AvatarFormsApp.Contracts.Services;

public interface IQuestionnaireService
{
    // Basic CRUD operations
    Task<List<Questionnaire>> GetAllAsync();
    Task<Questionnaire?> GetByIdAsync(string id);
    Task<Questionnaire> AddAsync(Questionnaire questionnaire);
    Task<Questionnaire> UpdateAsync(Questionnaire questionnaire);
    Task<bool> DeleteAsync(string id);
    
    // Query operations
    Task<List<Questionnaire>> SearchAsync(string searchTerm);
    Task<List<Questionnaire>> GetByStatusAsync(string status);
    Task<List<Questionnaire>> GetByOwnerAsync(string ownerId);
    
    // Get questionnaire with all related data
    Task<Questionnaire?> GetWithQuestionsAsync(string id);
    Task<Questionnaire?> GetWithResponsesAsync(string id);
    
    // Response operations
    Task<int> GetResponseCountAsync(string questionnaireId);
    Task<List<ResponseSession>> GetResponseSessionsAsync(string questionnaireId);
    Task<ResponseSession?> GetResponseSessionByIdAsync(string sessionId);
}
