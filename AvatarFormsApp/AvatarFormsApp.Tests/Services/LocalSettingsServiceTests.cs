using AvatarFormsApp.Contracts.Services;
using AvatarFormsApp.Helpers;
using AvatarFormsApp.Models;
using AvatarFormsApp.Services;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;

namespace AvatarFormsApp.Tests.Services;

public class LocalSettingsServiceTests : IDisposable
{
    private readonly Mock<IFileService> _mockFileService = new();
    private readonly IOptions<LocalSettingsOptions> _options;

    public LocalSettingsServiceTests()
    {
        var opt = new LocalSettingsOptions
        {
            ApplicationDataFolder = "TestFolder",
            LocalSettingsFile = "TestConfig.json"
        };
        _options = Options.Create(opt);
    }

    public void Dispose()
    {
        RuntimeHelper.IsMSIX = false;
    }

    [Fact]
    public async Task ReadSettingAsync_NonMSIX_ReturnsValueFromSettings()
    {
        // Arrange
        RuntimeHelper.IsMSIX = false;
        var dict = new Dictionary<string, object>
        {
            { "TestKey", "{\"Value\":\"TestResult\"}" }
        };
        _mockFileService.Setup(f => f.Read<IDictionary<string, object>>(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(dict);

        var sut = new LocalSettingsService(_mockFileService.Object, _options);

        // Act
        var result = await sut.ReadSettingAsync<TestModel>("TestKey");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("TestResult", result.Value);
    }

    [Fact]
    public async Task ReadSettingAsync_NonMSIX_ReturnsDefaultIfKeyNotFound()
    {
        // Arrange
        RuntimeHelper.IsMSIX = false;
        var dict = new Dictionary<string, object>();
        _mockFileService.Setup(f => f.Read<IDictionary<string, object>>(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(dict);

        var sut = new LocalSettingsService(_mockFileService.Object, _options);

        // Act
        var result = await sut.ReadSettingAsync<string>("NonExistentKey");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task SaveSettingAsync_NonMSIX_SavesToSettings()
    {
        // Arrange
        RuntimeHelper.IsMSIX = false;
        _mockFileService.Setup(f => f.Read<IDictionary<string, object>>(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new Dictionary<string, object>());

        var sut = new LocalSettingsService(_mockFileService.Object, _options);

        // Act
        await sut.SaveSettingAsync("NewKey", new TestModel { Value = "NewValue" });

        // Assert
        _mockFileService.Verify(f => f.Save(
            It.IsAny<string>(), 
            It.IsAny<string>(), 
            It.Is<IDictionary<string, object>>(d => d.ContainsKey("NewKey"))), Times.Once);
    }

    private class TestModel
    {
        public string? Value { get; set; }
    }

    [Fact]
    public async Task ReadSettingAsync_MSIX_ThrowsExceptionDueToPackagedEnvironment()
    {
        // Arrange
        RuntimeHelper.IsMSIX = true;
        var sut = new LocalSettingsService(_mockFileService.Object, _options);

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(() => sut.ReadSettingAsync<TestModel>("TestKey"));
    }

    [Fact]
    public async Task SaveSettingAsync_MSIX_ThrowsExceptionDueToPackagedEnvironment()
    {
        // Arrange
        RuntimeHelper.IsMSIX = true;
        var sut = new LocalSettingsService(_mockFileService.Object, _options);

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(() => sut.SaveSettingAsync("TestKey", new TestModel()));
    }
}
