using AvatarFormsApp.Data;
using AvatarFormsApp.Models;
using AvatarFormsApp.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AvatarFormsApp.Tests.Services;

public class QuestionnaireServiceTests
{
    private AppDbContext GetDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    // Helper to create a valid questionnaire with required fields
    private Questionnaire CreateValidQuestionnaire(string id, string name = "Default Name")
    {
        return new Questionnaire
        {
            Id = id,
            Name = name,
            OwnerId = "test-owner-123", // Required field
            Status = "Pending"
        };
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllOrderedByDate()
    {
        using var context = GetDbContext();
        var service = new QuestionnaireService(context);

        var q1 = CreateValidQuestionnaire("1", "Old");
        q1.CreatedDate = DateTime.UtcNow.AddDays(-1);

        var q2 = CreateValidQuestionnaire("2", "New");
        q2.CreatedDate = DateTime.UtcNow;

        context.Questionnaires.AddRange(q1, q2);
        await context.SaveChangesAsync();

        var result = await service.GetAllAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("2", result[0].Id);
    }

    [Fact]
    public async Task AddAsync_SetsDatesAndPersists()
    {
        using var context = GetDbContext();
        var service = new QuestionnaireService(context);
        var q = CreateValidQuestionnaire("test-add", "Test Questionnaire");

        var result = await service.AddAsync(q);

        Assert.NotEqual(default, result.CreatedDate);
        Assert.NotNull(await context.Questionnaires.FindAsync("test-add"));
    }

    [Fact]
    public async Task UpdateAsync_UpdatesModifiedDate()
    {
        using var context = GetDbContext();
        var service = new QuestionnaireService(context);

        var q = CreateValidQuestionnaire("u1", "Original");
        q.CreatedDate = DateTime.UtcNow.AddHours(-1);

        context.Questionnaires.Add(q);
        await context.SaveChangesAsync();

        q.Name = "Updated Name";
        var result = await service.UpdateAsync(q);

        Assert.True(result.ModifiedDate > q.CreatedDate);
        Assert.Equal("Updated Name", context.Questionnaires.Find("u1")?.Name);
    }

    [Fact]
    public async Task DeleteAsync_RemovesQuestionnaireAndResponses()
    {
        using var context = GetDbContext();
        var service = new QuestionnaireService(context);

        var q = CreateValidQuestionnaire("del-me", "Delete Me");
        var session = new ResponseSession { Id = "s1", QuestionnaireId = "del-me" };

        // Response now includes required QuestionId
        var response = new Response
        {
            Id = "r1",
            ResponseSessionId = "s1",
            QuestionId = "q-123" // Required field
        };

        context.Questionnaires.Add(q);
        context.ResponseSessions.Add(session);
        context.Responses.Add(response);
        await context.SaveChangesAsync();

        var result = await service.DeleteAsync("del-me");

        Assert.True(result);
        Assert.Null(await context.Questionnaires.FindAsync("del-me"));
    }

    [Fact]
    public async Task SearchAsync_WithTerm_FiltersCorrectly()
    {
        using var context = GetDbContext();
        var service = new QuestionnaireService(context);
        context.Questionnaires.AddRange(
            CreateValidQuestionnaire("1", "Apple Survey"),
            CreateValidQuestionnaire("2", "Banana Survey")
        );
        await context.SaveChangesAsync();

        var result = await service.SearchAsync("Apple");

        Assert.Single(result);
        Assert.Equal("Apple Survey", result[0].Name);
    }

    [Fact]
    public async Task GetByStatusAsync_FiltersCorrectly()
    {
        using var context = GetDbContext();
        var service = new QuestionnaireService(context);

        var q1 = CreateValidQuestionnaire("1");
        q1.Status = "Pending";

        var q2 = CreateValidQuestionnaire("2");
        q2.Status = "Closed";

        context.Questionnaires.AddRange(q1, q2);
        await context.SaveChangesAsync();

        var result = await service.GetByStatusAsync("Closed");

        Assert.Single(result);
        Assert.Equal("2", result[0].Id);
    }

    [Fact]
    public async Task GetResponseCountAsync_OnlyCountsCompleted()
    {
        using var context = GetDbContext();
        var service = new QuestionnaireService(context);
        context.ResponseSessions.AddRange(
            new ResponseSession { Id = "s1", QuestionnaireId = "q1", IsComplete = true },
            new ResponseSession { Id = "s2", QuestionnaireId = "q1", IsComplete = false }
        );
        await context.SaveChangesAsync();

        var count = await service.GetResponseCountAsync("q1");

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task GetAllAsync_ThrowsCustomException_OnDatabaseError()
    {
        using var context = GetDbContext();
        var service = new QuestionnaireService(context);

        await context.DisposeAsync();

        var ex = await Assert.ThrowsAsync<Exception>(() => service.GetAllAsync());
        Assert.Contains("Error retrieving questionnaires", ex.Message);
    }
}
