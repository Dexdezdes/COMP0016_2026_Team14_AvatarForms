using System.Collections.ObjectModel;
using AvatarFormsApp.Contracts.Services;
using AvatarFormsApp.Contracts.ViewModels;
using AvatarFormsApp.Models;
using AvatarFormsApp.ViewModels;
using Moq;
using Xunit;

namespace AvatarFormsApp.Tests.ViewModels;

public class QuestionnaireDetailPageViewModelTests
{
    private readonly Mock<IQuestionnaireService> _mockQService = new();
    private readonly Mock<INavigationService> _mockNavService = new();
    private readonly Mock<ILlamafileProcessService> _mockLlamaService = new();
    private readonly Mock<IPythonProcessService> _mockPythonService = new();
    private readonly Mock<IQuestionnaireAPIService> _mockQApiService = new();
    private readonly Mock<IResponseAPIService> _mockResponseApiService = new();

    private QuestionnaireDetailPageViewModel GetViewModel()
    {
        return new QuestionnaireDetailPageViewModel(
            _mockQService.Object,
            _mockNavService.Object,
            _mockLlamaService.Object,
            _mockPythonService.Object,
            _mockQApiService.Object,
            _mockResponseApiService.Object);
    }

    private Questionnaire CreateValidQuestionnaire(string id)
    {
        return new Questionnaire
        {
            Id = id,
            Name = "Test Questionnaire",
            OwnerId = "user-1",
            Questions = new List<Question>
            {
                new Question { Id = "q1", Text = "Q1", Order = 1, QuestionnaireId = id }
            }
        };
    }

    // ── Loading Tests ────────────────────────────────────────────────────────

    [Fact]
    public async Task LoadQuestionnaireAsync_Success_SetsProperties()
    {
        var vm = GetViewModel();
        var questionnaire = CreateValidQuestionnaire("test-id");
        _mockQService.Setup(s => s.GetByIdAsync("test-id")).ReturnsAsync(questionnaire);

        await vm.LoadQuestionnaireAsync("test-id");

        Assert.Equal("Test Questionnaire", vm.PageTitle);
        Assert.Equal(questionnaire, vm.Questionnaire);
        Assert.Single(vm.Questions);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task LoadQuestionnaireAsync_HandlesException()
    {
        var vm = GetViewModel();
        _mockQService.Setup(s => s.GetByIdAsync(It.IsAny<string>())).ThrowsAsync(new Exception("DB Error"));

        // Should not crash, just set IsLoading to false
        await vm.LoadQuestionnaireAsync("any");

        Assert.False(vm.IsLoading);
    }

    // ── Navigation & Backend Flow Tests ──────────────────────────────────────

    [Fact]
    public async Task NavigateToAvatarAsync_FullSuccess_Navigates()
    {
        var vm = GetViewModel();
        vm.Questionnaire = CreateValidQuestionnaire("q1");

        // Setup success for all steps
        _mockLlamaService.Setup(s => s.StartAsync()).ReturnsAsync(true);
        _mockPythonService.Setup(s => s.StartAsync(It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                         .ReturnsAsync(true);
        _mockQApiService.Setup(s => s.SendQuestionnaireAsync(It.IsAny<string>(), It.IsAny<int>()))
                        .ReturnsAsync(true);

        await vm.NavigateToAvatarCommand.ExecuteAsync(null);

        _mockNavService.Verify(n => n.NavigateTo(nameof(AvatarPageViewModel)), Times.Once);
        Assert.Equal(string.Empty, vm.StatusMessage);
    }

    [Fact]
    public async Task NavigateToAvatarAsync_Fails_IfLlamafileFails()
    {
        var vm = GetViewModel();
        _mockLlamaService.Setup(s => s.StartAsync()).ReturnsAsync(false);

        await vm.NavigateToAvatarCommand.ExecuteAsync(null);

        Assert.Contains("Failed to start llamafile", vm.StatusMessage);
        _mockNavService.Verify(n => n.NavigateTo(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task NavigateToAvatarAsync_Fails_IfPythonFails()
    {
        var vm = GetViewModel();
        _mockLlamaService.SetupGet(s => s.IsRunning).Returns(true); // Already running
        _mockPythonService.Setup(s => s.StartAsync(It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                         .ReturnsAsync(false);

        await vm.NavigateToAvatarCommand.ExecuteAsync(null);

        Assert.Contains("Failed to start avatar", vm.StatusMessage);
    }

    [Fact]
    public async Task NavigateToAvatarAsync_Fails_IfNoQuestionnaireLoaded()
    {
        var vm = GetViewModel();
        _mockLlamaService.SetupGet(s => s.IsRunning).Returns(true);
        _mockResponseApiService.SetupGet(s => s.IsRunning).Returns(true);
        _mockPythonService.SetupGet(s => s.IsRunning).Returns(true);
        vm.Questionnaire = null;

        await vm.NavigateToAvatarCommand.ExecuteAsync(null);

        Assert.Equal("No questionnaire uploaded.", vm.StatusMessage);
    }

    // ── Event Handling Tests ────────────────────────────────────────────────

    [Fact]
    public void OnAllResponsesReceived_StopsServer()
    {
        var vm = GetViewModel();

        // Trigger the event via the mock
        _mockResponseApiService.Raise(m => m.AllResponsesReceived += null);

        // We use Task.Delay(500) in the code, so we need a tiny wait in the test 
        // to verify the async call inside the void event handler.
        Thread.Sleep(600);

        _mockResponseApiService.Verify(s => s.StopServerAsync(), Times.AtLeastOnce);
    }

    [Fact]
    public void OnNavigatedFrom_UnsubscribesFromEvent()
    {
        var vm = GetViewModel();

        vm.OnNavigatedFrom();

        // After unsubscription, raising the event should not trigger another StopServerAsync call
        _mockResponseApiService.Raise(m => m.AllResponsesReceived += null);

        Thread.Sleep(100);
        _mockResponseApiService.Verify(s => s.StopServerAsync(), Times.Never);
    }

    [Fact]
    public void CanNavigateToAvatar_ReturnsFalse_WhileStarting()
    {
        var vm = GetViewModel();
        vm.IsStartingBackend = true;

        var canExecute = vm.NavigateToAvatarCommand.CanExecute(null);

        Assert.False(canExecute);
    }
}
