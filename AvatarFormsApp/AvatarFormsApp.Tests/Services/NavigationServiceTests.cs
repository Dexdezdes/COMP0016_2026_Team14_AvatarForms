using AvatarFormsApp.Contracts.Services;
using AvatarFormsApp.Services;
using Moq;
using Xunit;

namespace AvatarFormsApp.Tests.Services;

public class NavigationServiceTests
{
    private readonly Mock<IPageService> _mockPageService = new();

    [Fact]
    public void GoBack_ReturnsFalse_WhenFrameIsNull()
    {
        // Arrange
        var sut = new NavigationService(_mockPageService.Object);
        // By default Frame is null

        // Act
        var result = sut.GoBack();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void NavigateTo_ReturnsFalse_WhenFrameIsNull()
    {
        // Arrange
        var sut = new NavigationService(_mockPageService.Object);
        _mockPageService.Setup(p => p.GetPageType(It.IsAny<string>())).Returns(typeof(object));

        // Act
        var result = sut.NavigateTo("TestPage");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void CanGoBack_ReturnsFalse_WhenFrameIsNull()
    {
        // Arrange
        var sut = new NavigationService(_mockPageService.Object);

        // Act
        var result = sut.CanGoBack;

        // Assert
        Assert.False(result);
    }
}
