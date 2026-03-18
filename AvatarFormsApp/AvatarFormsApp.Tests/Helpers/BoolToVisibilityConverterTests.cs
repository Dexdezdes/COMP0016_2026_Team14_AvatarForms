using Microsoft.UI.Xaml;
using AvatarFormsApp.Helpers;
using Xunit;

namespace AvatarFormsApp.Tests;

public class BoolToVisibilityConverterTests
{
    private readonly BoolToVisibilityConverter _sut = new();

    // ── Convert ───────────────────────────────────────────────────────────────

    [Fact]
    public void Convert_ReturnsVisible_ForTrue()
    {
        var result = _sut.Convert(true, typeof(Visibility), null!, string.Empty);
        Assert.Equal(Visibility.Visible, result);
    }

    [Fact]
    public void Convert_ReturnsCollapsed_ForFalse()
    {
        var result = _sut.Convert(false, typeof(Visibility), null!, string.Empty);
        Assert.Equal(Visibility.Collapsed, result);
    }

    [Fact]
    public void Convert_ReturnsCollapsed_ForNonBoolValue()
    {
        var result = _sut.Convert("not a bool", typeof(Visibility), null!, string.Empty);
        Assert.Equal(Visibility.Collapsed, result);
    }

    [Fact]
    public void Convert_ReturnsCollapsed_ForNull()
    {
        var result = _sut.Convert(null!, typeof(Visibility), null!, string.Empty);
        Assert.Equal(Visibility.Collapsed, result);
    }

    [Fact]
    public void Convert_ReturnsCollapsed_ForInteger()
    {
        var result = _sut.Convert(1, typeof(Visibility), null!, string.Empty);
        Assert.Equal(Visibility.Collapsed, result);
    }

    // ── ConvertBack ───────────────────────────────────────────────────────────

    [Fact]
    public void ConvertBack_ReturnsTrue_ForVisible()
    {
        var result = _sut.ConvertBack(Visibility.Visible, typeof(bool), null!, string.Empty);
        Assert.Equal(true, result);
    }

    [Fact]
    public void ConvertBack_ReturnsFalse_ForCollapsed()
    {
        var result = _sut.ConvertBack(Visibility.Collapsed, typeof(bool), null!, string.Empty);
        Assert.Equal(false, result);
    }

    [Fact]
    public void ConvertBack_ReturnsFalse_ForNonVisibilityValue()
    {
        var result = _sut.ConvertBack("not visibility", typeof(bool), null!, string.Empty);
        Assert.Equal(false, result);
    }

    [Fact]
    public void ConvertBack_ReturnsFalse_ForNull()
    {
        var result = _sut.ConvertBack(null!, typeof(bool), null!, string.Empty);
        Assert.Equal(false, result);
    }

    [Fact]
    public void ConvertBack_ReturnsFalse_ForInteger()
    {
        var result = _sut.ConvertBack(1, typeof(bool), null!, string.Empty);
        Assert.Equal(false, result);
    }

    // ── Round-trip ────────────────────────────────────────────────────────────

    [Fact]
    public void RoundTrip_True_StaysTrue()
    {
        var visibility = _sut.Convert(true, typeof(Visibility), null!, string.Empty);
        var result = _sut.ConvertBack(visibility, typeof(bool), null!, string.Empty);
        Assert.Equal(true, result);
    }

    [Fact]
    public void RoundTrip_False_StaysFalse()
    {
        var visibility = _sut.Convert(false, typeof(Visibility), null!, string.Empty);
        var result = _sut.ConvertBack(visibility, typeof(bool), null!, string.Empty);
        Assert.Equal(false, result);
    }
}
