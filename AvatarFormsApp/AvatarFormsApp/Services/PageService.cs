using AvatarFormsApp.Contracts.Services;
using AvatarFormsApp.ViewModels;
using AvatarFormsApp.Views;

using CommunityToolkit.Mvvm.ComponentModel;

using Microsoft.UI.Xaml.Controls;

namespace AvatarFormsApp.Services;

public class PageService : IPageService
{
    private readonly Dictionary<string, Type> _pages = new();

    public PageService()
    {
        Configure<DashboardPageViewModel, DashboardPage>();
        Configure<ConversationPageViewModel, ConversationPage>();
        Configure<ShellPageViewModel, ShellPage>();
        Configure<CreateQuestionnairePageViewModel, CreateQuestionnairePage>();
        Configure<QuestionnaireDetailPageViewModel, QuestionnaireDetailPage>();
        Configure<AvatarPageViewModel, AvatarPage>();
        Configure<ResponsesPageViewModel, ResponsesPage>();
        Configure<ResponseDetailPageViewModel, ResponseDetailPage>();
    }

    public Type GetPageType(string key)
    {
        Type? pageType;
        lock (_pages)
        {
            if (!_pages.TryGetValue(key, out pageType))
            {
                throw new ArgumentException($"Page not found: {key}. Did you forget to call PageService.Configure?");
            }
        }

        return pageType;
    }

    public void Configure<VM, V>()
        where VM : ObservableObject
        where V : Page
    {
        lock (_pages)
        {
            var key = typeof(VM).Name;
            if (_pages.ContainsKey(key))
            {
                throw new ArgumentException($"The key {key} is already configured in PageService");
            }

            var type = typeof(V);
            if (_pages.ContainsValue(type))
            {
                throw new ArgumentException($"This type is already configured with key {_pages.First(p => p.Value == type).Key}");
            }

            _pages.Add(key, type);
        }
    }
}
