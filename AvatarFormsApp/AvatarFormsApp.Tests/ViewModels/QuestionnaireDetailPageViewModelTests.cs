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

    // ── Loading Tests ─────────────────────────────────────────────────────────

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

        await vm.LoadQuestionnaireAsync("any");

        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task LoadQuestionnaireAsync_SetsIsLoading_FalseAfterCompletion()
    {
        var vm = GetViewModel();
        _mockQService.Setup(s => s.GetByIdAsync(It.IsAny<string>())).ReturnsAsync((Questionnaire?)null);

        await vm.LoadQuestionnaireAsync("any");

        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task LoadQuestionnaireAsync_NullQuestionnaire_DoesNotSetPageTitle()
    {
        var vm = GetViewModel();
        _mockQService.Setup(s => s.GetByIdAsync(It.IsAny<string>())).ReturnsAsync((Questionnaire?)null);

        await vm.LoadQuestionnaireAsync("any");

        Assert.Equal("Questionnaire", vm.PageTitle); // stays default
    }

    // ── NavigateToAvatarAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task NavigateToAvatarAsync_FullSuccess_Navigates()
    {
        var vm = GetViewModel();
        vm.Questionnaire = CreateValidQuestionnaire("q1");

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
        _mockLlamaService.SetupGet(s => s.IsRunning).Returns(true);
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

    [Fact]
    public async Task NavigateToAvatarAsync_SetsIsStartingBackend_FalseAfterCompletion()
    {
        var vm = GetViewModel();
        _mockLlamaService.Setup(s => s.StartAsync()).ReturnsAsync(false);

        await vm.NavigateToAvatarCommand.ExecuteAsync(null);

        Assert.False(vm.IsStartingBackend);
    }

    [Fact]
    public async Task NavigateToAvatarAsync_OnException_SetsErrorMessage()
    {
        var vm = GetViewModel();
        _mockLlamaService.Setup(s => s.StartAsync()).ThrowsAsync(new Exception("crash"));

        await vm.NavigateToAvatarCommand.ExecuteAsync(null);

        Assert.Contains("Error", vm.StatusMessage);
    }

    [Fact]
    public async Task NavigateToAvatarAsync_OnException_IsStartingBackend_IsFalse()
    {
        var vm = GetViewModel();
        _mockLlamaService.Setup(s => s.StartAsync()).ThrowsAsync(new Exception("crash"));

        await vm.NavigateToAvatarCommand.ExecuteAsync(null);

        Assert.False(vm.IsStartingBackend);
    }

    [Fact]
    public async Task NavigateToAvatarAsync_SkipsLlamaStart_WhenAlreadyRunning()
    {
        var vm = GetViewModel();
        _mockLlamaService.SetupGet(s => s.IsRunning).Returns(true);
        _mockResponseApiService.SetupGet(s => s.IsRunning).Returns(true);
        _mockPythonService.Setup(s => s.StartAsync(It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                         .ReturnsAsync(false);

        await vm.NavigateToAvatarCommand.ExecuteAsync(null);

        _mockLlamaService.Verify(s => s.StartAsync(), Times.Never);
    }

    [Fact]
    public async Task NavigateToAvatarAsync_SkipsPythonStart_WhenAlreadyRunning()
    {
        var vm = GetViewModel();
        _mockLlamaService.SetupGet(s => s.IsRunning).Returns(true);
        _mockResponseApiService.SetupGet(s => s.IsRunning).Returns(true);
        _mockPythonService.SetupGet(s => s.IsRunning).Returns(true);
        vm.Questionnaire = CreateValidQuestionnaire("q1");
        _mockQApiService.Setup(s => s.SendQuestionnaireAsync(It.IsAny<string>(), It.IsAny<int>()))
                        .ReturnsAsync(true);

        await vm.NavigateToAvatarCommand.ExecuteAsync(null);

        _mockPythonService.Verify(s => s.StartAsync(It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task NavigateToAvatarAsync_Fails_WhenQApiReturnsFalse()
    {
        var vm = GetViewModel();
        _mockLlamaService.SetupGet(s => s.IsRunning).Returns(true);
        _mockResponseApiService.SetupGet(s => s.IsRunning).Returns(true);
        _mockPythonService.SetupGet(s => s.IsRunning).Returns(true);
        vm.Questionnaire = CreateValidQuestionnaire("q1");
        _mockQApiService.Setup(s => s.SendQuestionnaireAsync(It.IsAny<string>(), It.IsAny<int>()))
                        .ReturnsAsync(false);

        await vm.NavigateToAvatarCommand.ExecuteAsync(null);

        Assert.Contains("Failed to upload", vm.StatusMessage);
        _mockNavService.Verify(n => n.NavigateTo(It.IsAny<string>()), Times.Never);
    }

    // ── Event Handling ────────────────────────────────────────────────────────

    [Fact]
    public void OnAllResponsesReceived_StopsServer()
    {
        var vm = GetViewModel();

        _mockResponseApiService.Raise(m => m.AllResponsesReceived += null);

        Thread.Sleep(800);

        _mockResponseApiService.Verify(s => s.StopServerAsync(), Times.AtLeastOnce);
    }

    [Fact]
    public void OnNavigatedFrom_UnsubscribesFromEvent()
    {
        var vm = GetViewModel();

        vm.OnNavigatedFrom();

        _mockResponseApiService.Raise(m => m.AllResponsesReceived += null);

        Thread.Sleep(100);
        _mockResponseApiService.Verify(s => s.StopServerAsync(), Times.Never);
    }

    // ── CanExecute ────────────────────────────────────────────────────────────

    [Fact]
    public void CanNavigateToAvatar_ReturnsFalse_WhileStarting()
    {
        var vm = GetViewModel();
        vm.IsStartingBackend = true;

        Assert.False(vm.NavigateToAvatarCommand.CanExecute(null));
    }

    [Fact]
    public void CanNavigateToAvatar_ReturnsTrue_WhenNotStarting()
    {
        var vm = GetViewModel();
        vm.IsStartingBackend = false;

        Assert.True(vm.NavigateToAvatarCommand.CanExecute(null));
    }

    // ── Initial state ─────────────────────────────────────────────────────────

    [Fact]
    public void InitialState_PageTitle_IsDefault()
    {
        var vm = GetViewModel();
        Assert.Equal("Questionnaire", vm.PageTitle);
    }

    [Fact]
    public void InitialState_IsLoading_IsFalse()
    {
        var vm = GetViewModel();
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public void InitialState_StatusMessage_IsEmpty()
    {
        var vm = GetViewModel();
        Assert.Equal(string.Empty, vm.StatusMessage);
    }

    [Fact]
    public void InitialState_IsStartingBackend_IsFalse()
    {
        var vm = GetViewModel();
        Assert.False(vm.IsStartingBackend);
    }

    [Fact]
    public void OnNavigatedTo_DoesNotThrow()
    {
        var vm = GetViewModel();
        var ex = Record.Exception(() => vm.OnNavigatedTo(null!));
        Assert.Null(ex);
    }
}
