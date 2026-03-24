using AvatarFormsApp.Services;
using AvatarFormsApp.ViewModels;
using AvatarFormsApp.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Controls;
using Xunit;

namespace AvatarFormsApp.Tests.Services;

public class PageServiceTests
{
    private class TestVM1 : ObservableObject { }
    private class TestPage1 : Page { }

    private class TestVM2 : ObservableObject { }
    private class TestPage2 : Page { }

    [Fact]
    public void GetPageType_ReturnsType_WhenPreconfigured()
    {
        // Arrange
        var sut = new PageService();

        // Act
        var type = sut.GetPageType(typeof(DashboardPageViewModel).Name);

        // Assert
        Assert.Equal(typeof(DashboardPage), type);
    }

    [Fact]
    public void GetPageType_ThrowsArgumentException_WhenNotFound()
    {
        // Arrange
        var sut = new PageService();

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => sut.GetPageType("NonExistentVM"));
        Assert.Contains("Page not found", ex.Message);
    }

    [Fact]
    public void Configure_RegistersNewPage_Successfully()
    {
        // Arrange
        var sut = new PageService();

        // Act
        sut.Configure<TestVM1, TestPage1>();
        var type = sut.GetPageType(typeof(TestVM1).Name);

        // Assert
        Assert.Equal(typeof(TestPage1), type);
    }

    [Fact]
    public void Configure_ThrowsArgumentException_WhenKeyAlreadyConfigured()
    {
        // Arrange
        var sut = new PageService();
        sut.Configure<TestVM1, TestPage1>();

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => sut.Configure<TestVM1, TestPage2>());
        Assert.Contains("already configured in PageService", ex.Message);
    }

    [Fact]
    public void Configure_ThrowsArgumentException_WhenTypeAlreadyConfigured()
    {
        // Arrange
        var sut = new PageService();
        sut.Configure<TestVM1, TestPage1>();

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => sut.Configure<TestVM2, TestPage1>());
        Assert.Contains("This type is already configured with key", ex.Message);
    }
}
