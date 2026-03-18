using Microsoft.UI.Xaml;
using AvatarFormsApp.Helpers;
using Xunit;

namespace AvatarFormsApp.Tests;

public class StringToVisibilityConverterTests
{
    private readonly StringToVisibilityConverter _sut = new();

    // ── Convert ───────────────────────────────────────────────────────────────

    [Fact]
    public void Convert_ReturnsVisible_ForNonEmptyString()
    {
        var result = _sut.Convert("hello", typeof(Visibility), null!, string.Empty);
        Assert.Equal(Visibility.Visible, result);
    }

    [Fact]
    public void Convert_ReturnsCollapsed_ForEmptyString()
    {
        var result = _sut.Convert(string.Empty, typeof(Visibility), null!, string.Empty);
        Assert.Equal(Visibility.Collapsed, result);
    }

    [Fact]
    public void Convert_ReturnsCollapsed_ForWhitespaceOnly()
    {
        var result = _sut.Convert("   ", typeof(Visibility), null!, string.Empty);
        Assert.Equal(Visibility.Collapsed, result);
    }

    [Fact]
    public void Convert_ReturnsCollapsed_ForNull()
    {
        var result = _sut.Convert(null!, typeof(Visibility), null!, string.Empty);
        Assert.Equal(Visibility.Collapsed, result);
    }

    [Fact]
    public void Convert_ReturnsCollapsed_ForNonStringValue()
    {
        var result = _sut.Convert(42, typeof(Visibility), null!, string.Empty);
        Assert.Equal(Visibility.Collapsed, result);
    }

    [Fact]
    public void Convert_ReturnsCollapsed_ForBoolValue()
    {
        var result = _sut.Convert(true, typeof(Visibility), null!, string.Empty);
        Assert.Equal(Visibility.Collapsed, result);
    }

    [Fact]
    public void Convert_ReturnsVisible_ForSingleCharString()
    {
        var result = _sut.Convert("a", typeof(Visibility), null!, string.Empty);
        Assert.Equal(Visibility.Visible, result);
    }

    [Fact]
    public void Convert_ReturnsVisible_ForStringWithSpacesAndContent()
    {
        var result = _sut.Convert("  hello  ", typeof(Visibility), null!, string.Empty);
        Assert.Equal(Visibility.Visible, result);
    }

    [Fact]
    public void Convert_ReturnsCollapsed_ForTabOnly()
    {
        var result = _sut.Convert("\t", typeof(Visibility), null!, string.Empty);
        Assert.Equal(Visibility.Collapsed, result);
    }

    [Fact]
    public void Convert_ReturnsCollapsed_ForNewlineOnly()
    {
        var result = _sut.Convert("\n", typeof(Visibility), null!, string.Empty);
        Assert.Equal(Visibility.Collapsed, result);
    }

    // ── ConvertBack ───────────────────────────────────────────────────────────

    [Fact]
    public void ConvertBack_ThrowsNotImplementedException()
    {
        Assert.Throws<NotImplementedException>(() =>
            _sut.ConvertBack(Visibility.Visible, typeof(string), null!, string.Empty));
    }
}
