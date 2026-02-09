using Microsoft.EntityFrameworkCore;
using AvatarFormsApp.Contracts.Services;
using AvatarFormsApp.Data;
using AvatarFormsApp.Models;

namespace AvatarFormsApp.Services;

public class QuestionnaireService : IQuestionnaireService
{
    private readonly AppDbContext _context;

    public QuestionnaireService(AppDbContext context)
    {
        _context = context;
    }

    // Get all questionnaires
    public async Task<List<Questionnaire>> GetAllAsync()
    {
        try
        {
            return await _context.Questionnaires
                .Include(q => q.Questions)
                    .ThenInclude(q => q.Options)
                .OrderByDescending(q => q.CreatedDate)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            throw new Exception("Error retrieving questionnaires", ex);
        }
    }

    // Get questionnaire by ID
    public async Task<Questionnaire?> GetByIdAsync(string id)
    {
        try
        {
            return await _context.Questionnaires
                .Include(q => q.Questions)
                    .ThenInclude(q => q.Options)
                .FirstOrDefaultAsync(q => q.Id == id);
        }
        catch (Exception ex)
        {
            throw new Exception($"Error retrieving questionnaire {id}", ex);
        }
    }

    // Add new questionnaire
    public async Task<Questionnaire> AddAsync(Questionnaire questionnaire)
    {
        try
        {
            questionnaire.CreatedDate = DateTime.UtcNow;
            questionnaire.ModifiedDate = DateTime.UtcNow;
            
            _context.Questionnaires.Add(questionnaire);
            await _context.SaveChangesAsync();
            
            return questionnaire;
        }
        catch (Exception ex)
        {
            throw new Exception("Error adding questionnaire", ex);
        }
    }

    // Update existing questionnaire
    public async Task<Questionnaire> UpdateAsync(Questionnaire questionnaire)
    {
        try
        {
            questionnaire.ModifiedDate = DateTime.UtcNow;
            
            _context.Questionnaires.Update(questionnaire);
            await _context.SaveChangesAsync();
            
            return questionnaire;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error updating questionnaire {questionnaire.Id}", ex);
        }
    }

    // Delete questionnaire
    public async Task<bool> DeleteAsync(string id)
    {
        try
        {
            var questionnaire = await _context.Questionnaires.FindAsync(id);
            if (questionnaire == null)
                return false;

            _context.Questionnaires.Remove(questionnaire);
            await _context.SaveChangesAsync();
            
            return true;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error deleting questionnaire {id}", ex);
        }
    }

    // Search questionnaires by name
    public async Task<List<Questionnaire>> SearchAsync(string searchTerm)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return await GetAllAsync();

            return await _context.Questionnaires
                .Where(q => q.Name.Contains(searchTerm))
                .OrderByDescending(q => q.CreatedDate)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            throw new Exception("Error searching questionnaires", ex);
        }
    }

    // Get questionnaires by status (Pending/Closed)
    public async Task<List<Questionnaire>> GetByStatusAsync(string status)
    {
        try
        {
            return await _context.Questionnaires
                .Where(q => q.Status == status)
                .OrderByDescending(q => q.CreatedDate)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            throw new Exception($"Error retrieving questionnaires with status {status}", ex);
        }
    }

    // Get questionnaires by owner
    public async Task<List<Questionnaire>> GetByOwnerAsync(string ownerId)
    {
        try
        {
            return await _context.Questionnaires
                .Where(q => q.OwnerId == ownerId)
                .OrderByDescending(q => q.CreatedDate)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            throw new Exception($"Error retrieving questionnaires for owner {ownerId}", ex);
        }
    }

    // Get questionnaire with all questions
    public async Task<Questionnaire?> GetWithQuestionsAsync(string id)
    {
        try
        {
            return await _context.Questionnaires
                .Include(q => q.Questions.OrderBy(q => q.Order))
                    .ThenInclude(q => q.Options.OrderBy(o => o.Order))
                .FirstOrDefaultAsync(q => q.Id == id);
        }
        catch (Exception ex)
        {
            throw new Exception($"Error retrieving questionnaire {id} with questions", ex);
        }
    }

    // Get questionnaire with all responses
    public async Task<Questionnaire?> GetWithResponsesAsync(string id)
    {
        try
        {
            return await _context.Questionnaires
                .Include(q => q.ResponseSessions)
                    .ThenInclude(rs => rs.Responses)
                        .ThenInclude(r => r.Question)
                .FirstOrDefaultAsync(q => q.Id == id);
        }
        catch (Exception ex)
        {
            throw new Exception($"Error retrieving questionnaire {id} with responses", ex);
        }
    }

    // Get response count for a questionnaire
    public async Task<int> GetResponseCountAsync(string questionnaireId)
    {
        try
        {
            return await _context.ResponseSessions
                .Where(rs => rs.QuestionnaireId == questionnaireId && rs.IsComplete)
                .CountAsync();
        }
        catch (Exception ex)
        {
            throw new Exception($"Error counting responses for questionnaire {questionnaireId}", ex);
        }
    }

    // Get all response sessions for a questionnaire
    public async Task<List<ResponseSession>> GetResponseSessionsAsync(string questionnaireId)
    {
        try
        {
            return await _context.ResponseSessions
                .Where(rs => rs.QuestionnaireId == questionnaireId)
                .Include(rs => rs.Responses)
                    .ThenInclude(r => r.Question)
                .OrderByDescending(rs => rs.SubmittedDate)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            throw new Exception($"Error retrieving response sessions for questionnaire {questionnaireId}", ex);
        }
    }
}
