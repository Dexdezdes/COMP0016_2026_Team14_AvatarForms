using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AvatarFormsApp.Contracts.Services;
using AvatarFormsApp.Models;
using AvatarFormsApp.ViewModels;
using Moq;
using Xunit;

namespace AvatarFormsApp.Tests.ViewModels;

public class ResponseDetailPageViewModelTests
{
    private readonly Mock<IQuestionnaireService> _mockQService = new();

    private ResponseDetailPageViewModel GetViewModel() => new(_mockQService.Object);

    private ResponseSession CreateMockSession(string id)
    {
        var qId = "q-123";
        return new ResponseSession
        {
            Id = id,
            QuestionnaireId = qId,
            SubmittedDate = new DateTime(2023, 10, 10, 12, 0, 0, DateTimeKind.Utc),
            IsComplete = true,
            Questionnaire = new Questionnaire
            {
                Id = qId,
                Name = "Test Form",
                Description = "Test Desc",
                Color = "#4CB3B3",
                OwnerId = "owner-1"
            },
            Responses = new List<Response>() // Never null
        };
    }

    [Fact]
    public async Task LoadSessionAsync_Success_MapsPropertiesCorrectly()
    {
        var vm = GetViewModel();
        var sessionId = "1234567890";
        var session = CreateMockSession(sessionId);

        var mcqQuestion = new Question
        {
            Id = "q1",
            Text = "Pick one",
            Order = 2,
            QuestionnaireId = "q-123",
            Options = new List<QuestionOption>
            {
                new() { Text = "Option A", Order = 1, QuestionId = "q1" },
                new() { Text = "Option B", Order = 2, QuestionId = "q1" }
            }
        };

        session.Responses.Add(new Response
        {
            Id = "r1",
            QuestionId = "q1",
            ResponseSessionId = sessionId,
            AnswerText = "Option A",
            Question = mcqQuestion
        });

        _mockQService.Setup(s => s.GetResponseSessionByIdAsync(sessionId))
                     .ReturnsAsync(session);

        await vm.LoadSessionAsync(sessionId);

        Assert.Equal("Test Form", vm.QuestionnaireName);
        Assert.Equal(1, vm.ResponseCount);
        Assert.Equal("Session — 12345678…", vm.PageTitle);

        Assert.Single(vm.ResponseItems);
        var item = vm.ResponseItems[0];
        Assert.Equal("Option A", item.AnswerText);
        Assert.Equal(mcqQuestion.IsMCQ, item.IsMCQ);
    }
    [Fact]
    public async Task LoadSessionAsync_ReturnsEarly_WhenSessionIsNull()
    {
        var vm = GetViewModel();
        _mockQService.Setup(s => s.GetResponseSessionByIdAsync(It.IsAny<string>()))
                     .ReturnsAsync((ResponseSession?)null);

        await vm.LoadSessionAsync("invalid");

        Assert.False(vm.IsLoading);
        Assert.Equal(string.Empty, vm.QuestionnaireName);
        Assert.Empty(vm.ResponseItems);
    }

    [Fact]
    public async Task LoadSessionAsync_CatchBlock_HandlesException()
    {
        var vm = GetViewModel();
        _mockQService.Setup(s => s.GetResponseSessionByIdAsync(It.IsAny<string>()))
                     .ThrowsAsync(new Exception("Database failure"));

        await vm.LoadSessionAsync("any-id");

        Assert.False(vm.IsLoading);
    }

    [Theory]
    [InlineData("Normal Text", "Normal Text")]
    [InlineData("Text with \"quotes\"", "Text with \"\"quotes\"\"")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Escapecsv_FormatsCorrectly(string? input, string expected)
    {
        var vm = GetViewModel();
        var method = typeof(ResponseDetailPageViewModel)
            .GetMethod("Escapecsv", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var result = method?.Invoke(vm, new object[] { input! });

        Assert.Equal(expected, result);
    }
}
