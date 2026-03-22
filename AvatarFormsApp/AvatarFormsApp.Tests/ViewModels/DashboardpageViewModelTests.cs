using AvatarFormsApp.Contracts.Services;
using AvatarFormsApp.Models;
using AvatarFormsApp.ViewModels;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace AvatarFormsApp.Tests.ViewModels;

// ── Fake service ──────────────────────────────────────────────────────────────

internal class FakeQuestionnaireServiceForDashboard : IQuestionnaireService
{
    public List<Questionnaire> QuestionnairesToReturn { get; set; } = new();
    public bool ThrowOnGetAll { get; set; } = false;
    public List<Questionnaire> Added { get; } = new();
    public List<string> Deleted { get; } = new();
    public Dictionary<string, int> ResponseCounts { get; set; } = new();

    public Task<List<Questionnaire>> GetAllAsync()
    {
        if (ThrowOnGetAll) throw new Exception("Simulated failure");
        return Task.FromResult(QuestionnairesToReturn);
    }

    public Task<Questionnaire> AddAsync(Questionnaire q)
    {
        Added.Add(q);
        QuestionnairesToReturn.Add(q);
        return Task.FromResult(q);
    }

    public Task<bool> DeleteAsync(string id)
    {
        Deleted.Add(id);
        QuestionnairesToReturn.RemoveAll(q => q.Id == id);
        return Task.FromResult(true);
    }

    public Task<int> GetResponseCountAsync(string questionnaireId)
    {
        ResponseCounts.TryGetValue(questionnaireId, out var count);
        return Task.FromResult(count);
    }

    public Task<Questionnaire?> GetByIdAsync(string id) => Task.FromResult<Questionnaire?>(null);
    public Task<Questionnaire> UpdateAsync(Questionnaire q) => Task.FromResult(q);
    public Task<List<Questionnaire>> SearchAsync(string s) => Task.FromResult(new List<Questionnaire>());
    public Task<List<Questionnaire>> GetByStatusAsync(string s) => Task.FromResult(new List<Questionnaire>());
    public Task<List<Questionnaire>> GetByOwnerAsync(string s) => Task.FromResult(new List<Questionnaire>());
    public Task<Questionnaire?> GetWithQuestionsAsync(string id) => Task.FromResult<Questionnaire?>(null);
    public Task<Questionnaire?> GetWithResponsesAsync(string id) => Task.FromResult<Questionnaire?>(null);
    public Task<List<ResponseSession>> GetResponseSessionsAsync(string id) => Task.FromResult(new List<ResponseSession>());
    public Task<ResponseSession?> GetResponseSessionByIdAsync(string id) => Task.FromResult<ResponseSession?>(null);
}

// ── Tests ─────────────────────────────────────────────────────────────────────

public class DashboardpageViewModelTests
{
    private FakeQuestionnaireServiceForDashboard _fakeService;
    private DashboardPageViewModel _sut;

    public DashboardpageViewModelTests()
    {
        _fakeService = new FakeQuestionnaireServiceForDashboard();
        _sut = new DashboardPageViewModel(_fakeService);
    }

    private async Task WaitForLoadAsync()
    {
        await Task.Delay(100);
    }

    // ── Initial state ─────────────────────────────────────────────────────────

    [Fact]
    public void InitialState_SearchText_IsEmpty()
    {
        Assert.Equal(string.Empty, _sut.SearchText);
    }

    [Fact]
    public void InitialState_SelectedFilter_IsAll()
    {
        Assert.Equal("All Questionnaires", _sut.SelectedFilter);
    }

    [Fact]
    public void InitialState_SelectedSort_IsNameAZ()
    {
        Assert.Equal("Sort by name (A-Z)", _sut.SelectedSort);
    }

    [Fact]
    public async Task InitialState_HasQuestionnaires_IsTrue()
    {
        _fakeService.QuestionnairesToReturn = new List<Questionnaire>
        {
            new Questionnaire { Id = "1", Name = "Test", OwnerId = "test", CreatedDate = DateTime.Now, Status = "Pending" }
        };

        _sut = new DashboardPageViewModel(_fakeService);
        await WaitForLoadAsync();

        Assert.True(_sut.HasQuestionnaires);
    }

    // ── LoadQuestionnairesAsync ───────────────────────────────────────────────

    [Fact]
    public async Task Load_PopulatesFilteredQuestionnaires()
    {
        _fakeService.QuestionnairesToReturn = new List<Questionnaire>
        {
            new Questionnaire { Id = "1", Name = "Alpha", Status = "Pending", CreatedDate = DateTime.Now, OwnerId = "test" },
            new Questionnaire { Id = "2", Name = "Beta", Status = "Pending", CreatedDate = DateTime.Now, OwnerId = "test" }
        };
        _sut = new DashboardPageViewModel(_fakeService);
        await WaitForLoadAsync();

        Assert.Equal(2, _sut.FilteredQuestionnaires.Count);
    }

    [Fact]
    public async Task Load_SetsHasQuestionnaires_True_WhenDataExists()
    {
        _fakeService.QuestionnairesToReturn = new List<Questionnaire>
        {
            new Questionnaire { Id = "1", Name = "Alpha", Status = "Pending", CreatedDate = DateTime.Now, OwnerId = "test" }
        };
        _sut = new DashboardPageViewModel(_fakeService);
        await WaitForLoadAsync();

        Assert.True(_sut.HasQuestionnaires);
    }

    [Fact]
    public async Task Load_SetsHasQuestionnaires_False_WhenNoData()
    {
        _fakeService.QuestionnairesToReturn = new List<Questionnaire>();
        _sut = new DashboardPageViewModel(_fakeService);
        await WaitForLoadAsync();

        Assert.False(_sut.HasQuestionnaires);
    }

    [Fact]
    public async Task Load_IsLoading_IsFalse_AfterCompletion()
    {
        _sut = new DashboardPageViewModel(_fakeService);
        await WaitForLoadAsync();

        Assert.False(_sut.IsLoading);
    }

    [Fact]
    public async Task Load_OnException_SetsHasQuestionnaires_False()
    {
        _fakeService.ThrowOnGetAll = true;
        _sut = new DashboardPageViewModel(_fakeService);
        await WaitForLoadAsync();

        Assert.False(_sut.HasQuestionnaires);
    }

    [Fact]
    public async Task Load_OnException_FilteredQuestionnaires_IsEmpty()
    {
        _fakeService.ThrowOnGetAll = true;
        _sut = new DashboardPageViewModel(_fakeService);
        await WaitForLoadAsync();

        Assert.Empty(_sut.FilteredQuestionnaires);
    }

    [Fact]
    public async Task Load_OnException_IsLoading_IsFalse()
    {
        _fakeService.ThrowOnGetAll = true;
        _sut = new DashboardPageViewModel(_fakeService);
        await WaitForLoadAsync();

        Assert.False(_sut.IsLoading);
    }

    // ── Refresh ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task RefreshQuestionnairesAsync_ReloadsData()
    {
        _fakeService.QuestionnairesToReturn = new List<Questionnaire>();
        _sut = new DashboardPageViewModel(_fakeService);
        await WaitForLoadAsync();

        _fakeService.QuestionnairesToReturn = new List<Questionnaire>
        {
            new Questionnaire { Id = "1", Name = "New Item", Status = "Pending", CreatedDate = DateTime.Now, OwnerId = "test" }
        };

        await _sut.RefreshQuestionnairesAsync();

        Assert.Single(_sut.FilteredQuestionnaires);
    }

    // ── Filter ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Filter_Pending_ShowsOnlyPendingQuestionnaires()
    {
        _fakeService.QuestionnairesToReturn = new List<Questionnaire>
        {
            new Questionnaire { Id = "1", Name = "Alpha", Status = "Pending", CreatedDate = DateTime.Now, OwnerId = "test" },
            new Questionnaire { Id = "2", Name = "Beta", Status = "Closed", CreatedDate = DateTime.Now, OwnerId = "test" }
        };
        _sut = new DashboardPageViewModel(_fakeService);
        await WaitForLoadAsync();

        _sut.SelectedFilter = "Pending";

        Assert.Single(_sut.FilteredQuestionnaires);
        Assert.Equal("Alpha", _sut.FilteredQuestionnaires[0].Name);
    }

    [Fact]
    public async Task Filter_Closed_ShowsOnlyClosedQuestionnaires()
    {
        _fakeService.QuestionnairesToReturn = new List<Questionnaire>
        {
            new Questionnaire { Id = "1", Name = "Alpha", Status = "Pending", CreatedDate = DateTime.Now, OwnerId = "test" },
            new Questionnaire { Id = "2", Name = "Beta", Status = "Closed", CreatedDate = DateTime.Now, OwnerId = "test" }
        };
        _sut = new DashboardPageViewModel(_fakeService);
        await WaitForLoadAsync();

        _sut.SelectedFilter = "Closed";

        Assert.Single(_sut.FilteredQuestionnaires);
        Assert.Equal("Beta", _sut.FilteredQuestionnaires[0].Name);
    }

    [Fact]
    public async Task Filter_All_ShowsAllQuestionnaires()
    {
        _fakeService.QuestionnairesToReturn = new List<Questionnaire>
        {
            new Questionnaire { Id = "1", Name = "Alpha", Status = "Pending", CreatedDate = DateTime.Now, OwnerId = "test" },
            new Questionnaire { Id = "2", Name = "Beta", Status = "Closed", CreatedDate = DateTime.Now, OwnerId = "test" }
        };
        _sut = new DashboardPageViewModel(_fakeService);
        await WaitForLoadAsync();

        _sut.SelectedFilter = "All Questionnaires";

        Assert.Equal(2, _sut.FilteredQuestionnaires.Count);
    }

    // ── Search ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Search_FiltersBy_Name()
    {
        _fakeService.QuestionnairesToReturn = new List<Questionnaire>
        {
            new Questionnaire { Id = "1", Name = "Health Survey", Status = "Pending", CreatedDate = DateTime.Now, OwnerId = "test" },
            new Questionnaire { Id = "2", Name = "Learning Review", Status = "Pending", CreatedDate = DateTime.Now, OwnerId = "test" }
        };
        _sut = new DashboardPageViewModel(_fakeService);
        await WaitForLoadAsync();

        _sut.SearchText = "health";

        Assert.Single(_sut.FilteredQuestionnaires);
        Assert.Equal("Health Survey", _sut.FilteredQuestionnaires[0].Name);
    }

    [Fact]
    public async Task Search_IsCaseInsensitive()
    {
        _fakeService.QuestionnairesToReturn = new List<Questionnaire>
        {
            new Questionnaire { Id = "1", Name = "Health Survey", Status = "Pending", CreatedDate = DateTime.Now, OwnerId = "test" }
        };
        _sut = new DashboardPageViewModel(_fakeService);
        await WaitForLoadAsync();

        _sut.SearchText = "HEALTH";

        Assert.Single(_sut.FilteredQuestionnaires);
    }

    [Fact]
    public async Task Search_EmptyText_ShowsAll()
    {
        _fakeService.QuestionnairesToReturn = new List<Questionnaire>
        {
            new Questionnaire { Id = "1", Name = "Alpha", Status = "Pending", CreatedDate = DateTime.Now, OwnerId = "test" },
            new Questionnaire { Id = "2", Name = "Beta", Status = "Pending", CreatedDate = DateTime.Now, OwnerId = "test" }
        };
        _sut = new DashboardPageViewModel(_fakeService);
        await WaitForLoadAsync();

        _sut.SearchText = string.Empty;

        Assert.Equal(2, _sut.FilteredQuestionnaires.Count);
    }

    [Fact]
    public async Task Search_WhitespaceOnly_ShowsAll()
    {
        _fakeService.QuestionnairesToReturn = new List<Questionnaire>
        {
            new Questionnaire { Id = "1", Name = "Alpha", Status = "Pending", CreatedDate = DateTime.Now, OwnerId = "test" },
            new Questionnaire { Id = "2", Name = "Beta", Status = "Pending", CreatedDate = DateTime.Now, OwnerId = "test" }
        };
        _sut = new DashboardPageViewModel(_fakeService);
        await WaitForLoadAsync();

        _sut.SearchText = "   ";

        Assert.Equal(2, _sut.FilteredQuestionnaires.Count);
    }

    [Fact]
    public async Task Search_NoMatch_ReturnsEmpty()
    {
        _fakeService.QuestionnairesToReturn = new List<Questionnaire>
        {
            new Questionnaire { Id = "1", Name = "Alpha", Status = "Pending", CreatedDate = DateTime.Now, OwnerId = "test" }
        };
        _sut = new DashboardPageViewModel(_fakeService);
        await WaitForLoadAsync();

        _sut.SearchText = "zzznomatch";

        Assert.Empty(_sut.FilteredQuestionnaires);
    }

    // ── Sort ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Sort_NameAZ_SortsAscending()
    {
        _fakeService.QuestionnairesToReturn = new List<Questionnaire>
        {
            new Questionnaire { Id = "1", Name = "Zebra", Status = "Pending", CreatedDate = DateTime.Now, OwnerId = "test" },
            new Questionnaire { Id = "2", Name = "Alpha", Status = "Pending", CreatedDate = DateTime.Now, OwnerId = "test" }
        };
        _sut = new DashboardPageViewModel(_fakeService);
        await WaitForLoadAsync();

        _sut.SelectedSort = "Sort by name (A-Z)";

        Assert.Equal("Alpha", _sut.FilteredQuestionnaires[0].Name);
        Assert.Equal("Zebra", _sut.FilteredQuestionnaires[1].Name);
    }

    [Fact]
    public async Task Sort_NameZA_SortsDescending()
    {
        _fakeService.QuestionnairesToReturn = new List<Questionnaire>
        {
            new Questionnaire { Id = "1", Name = "Alpha", Status = "Pending", CreatedDate = DateTime.Now, OwnerId = "test" },
            new Questionnaire { Id = "2", Name = "Zebra", Status = "Pending", CreatedDate = DateTime.Now, OwnerId = "test" }
        };
        _sut = new DashboardPageViewModel(_fakeService);
        await WaitForLoadAsync();

        _sut.SelectedSort = "Sort by name (Z-A)";

        Assert.Equal("Zebra", _sut.FilteredQuestionnaires[0].Name);
        Assert.Equal("Alpha", _sut.FilteredQuestionnaires[1].Name);
    }

    [Fact]
    public async Task Sort_ByDate_SortsMostRecentFirst()
    {
        var older = DateTime.Now.AddDays(-10);
        var newer = DateTime.Now;

        _fakeService.QuestionnairesToReturn = new List<Questionnaire>
        {
            new Questionnaire { Id = "1", Name = "Old", Status = "Pending", CreatedDate = older, OwnerId = "test" },
            new Questionnaire { Id = "2", Name = "New", Status = "Pending", CreatedDate = newer, OwnerId = "test" }
        };
        _sut = new DashboardPageViewModel(_fakeService);
        await WaitForLoadAsync();

        _sut.SelectedSort = "Sort by date";

        Assert.Equal("New", _sut.FilteredQuestionnaires[0].Name);
        Assert.Equal("Old", _sut.FilteredQuestionnaires[1].Name);
    }

    [Fact]
    public async Task Sort_ByResponses_SortsByResponseCountDescending()
    {
        _fakeService.QuestionnairesToReturn = new List<Questionnaire>
        {
            new Questionnaire { Id = "low", Name = "LowResponses", Status = "Pending", CreatedDate = DateTime.Now, OwnerId = "test" },
            new Questionnaire { Id = "high", Name = "HighResponses", Status = "Pending", CreatedDate = DateTime.Now, OwnerId = "test" }
        };
        _fakeService.ResponseCounts["low"] = 1;
        _fakeService.ResponseCounts["high"] = 99;

        _sut = new DashboardPageViewModel(_fakeService);
        await WaitForLoadAsync();

        _sut.SelectedSort = "Sort by responses";

        Assert.Equal("HighResponses", _sut.FilteredQuestionnaires[0].Name);
        Assert.Equal("LowResponses", _sut.FilteredQuestionnaires[1].Name);
    }

    // ── AddQuestionnaireAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task AddQuestionnaireAsync_AddsToService()
    {
        _sut = new DashboardPageViewModel(_fakeService);
        await WaitForLoadAsync();

        var q = new Questionnaire { Id = "new1", Name = "New Q", Status = "Pending", CreatedDate = DateTime.Now, OwnerId = "test" };
        await _sut.AddQuestionnaireAsync(q);

        Assert.Contains(_fakeService.Added, x => x.Id == "new1");
    }

    [Fact]
    public async Task AddQuestionnaireAsync_ReloadsAfterAdd()
    {
        _sut = new DashboardPageViewModel(_fakeService);
        await WaitForLoadAsync();

        var q = new Questionnaire { Id = "new1", Name = "New Q", Status = "Pending", CreatedDate = DateTime.Now, OwnerId = "test" };
        await _sut.AddQuestionnaireAsync(q);

        Assert.Single(_sut.FilteredQuestionnaires);
    }

    [Fact]
    public async Task AddQuestionnaireAsync_HandlesException_Gracefully()
    {
        // Arrange
        _fakeService.ThrowOnGetAll = false;
        _sut = new DashboardPageViewModel(_fakeService);
        await WaitForLoadAsync();

        var q = new Questionnaire { Id = "new1", Name = "New Q", Status = "Pending", CreatedDate = DateTime.Now, OwnerId = "test" };

        // Act: Set the service to throw during the "Reload" phase of AddQuestionnaireAsync
        _fakeService.ThrowOnGetAll = true;

        // We call the method. Since the VM catches the error internally (to show a popup), 
        // we just verify that it doesn't crash the test and leaves the VM in a safe state.
        await _sut.AddQuestionnaireAsync(q);

        // Assert: Ensure IsLoading is reset to false even if it failed
        Assert.False(_sut.IsLoading);
    }

    // ── DeleteQuestionnaireAsync ──────────────────────────────────────────────

    [Fact]
    public async Task DeleteQuestionnaireAsync_RemovesFromService()
    {
        _fakeService.QuestionnairesToReturn = new List<Questionnaire>
        {
            new Questionnaire { Id = "del1", Name = "To Delete", Status = "Pending", CreatedDate = DateTime.Now, OwnerId = "test" }
        };
        _sut = new DashboardPageViewModel(_fakeService);
        await WaitForLoadAsync();

        await _sut.DeleteQuestionnaireAsync("del1");

        Assert.Contains("del1", _fakeService.Deleted);
    }

    [Fact]
    public async Task DeleteQuestionnaireAsync_ReloadsAfterDelete()
    {
        _fakeService.QuestionnairesToReturn = new List<Questionnaire>
        {
            new Questionnaire { Id = "del1", Name = "To Delete", Status = "Pending", CreatedDate = DateTime.Now, OwnerId = "test" }
        };
        _sut = new DashboardPageViewModel(_fakeService);
        await WaitForLoadAsync();

        await _sut.DeleteQuestionnaireAsync("del1");

        Assert.Empty(_sut.FilteredQuestionnaires);
    }

    // ── SeedSampleDataAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task SeedSampleDataAsync_AddsQuestionnaires_WhenNoneExist()
    {
        _fakeService.QuestionnairesToReturn = new List<Questionnaire>();
        _sut = new DashboardPageViewModel(_fakeService);
        await WaitForLoadAsync();

        await _sut.SeedSampleDataAsync();

        Assert.Equal(5, _fakeService.Added.Count);
    }

    [Fact]
    public async Task SeedSampleDataAsync_DoesNotSeed_WhenDataAlreadyExists()
    {
        _fakeService.QuestionnairesToReturn = new List<Questionnaire>
        {
            new Questionnaire { Id = "existing", Name = "Existing", Status = "Pending", CreatedDate = DateTime.Now, OwnerId = "test" }
        };
        _sut = new DashboardPageViewModel(_fakeService);
        await WaitForLoadAsync();

        await _sut.SeedSampleDataAsync();

        Assert.Empty(_fakeService.Added);
    }

    [Fact]
    public async Task SeedSampleDataAsync_SeedsCorrectNames()
    {
        _fakeService.QuestionnairesToReturn = new List<Questionnaire>();
        _sut = new DashboardPageViewModel(_fakeService);
        await WaitForLoadAsync();

        await _sut.SeedSampleDataAsync();

        var names = _fakeService.Added.Select(q => q.Name).ToList();
        Assert.Contains("DASS Questionnaire", names);
        Assert.Contains("Learning Experience Survey", names);
        Assert.Contains("Examination Feedback Survey", names);
        Assert.Contains("Student Health Questionnaire", names);
        Assert.Contains("Year End Review", names);
    }
}
