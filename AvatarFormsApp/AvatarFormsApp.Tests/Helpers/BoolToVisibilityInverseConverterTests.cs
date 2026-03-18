using Microsoft.UI.Xaml;
using AvatarFormsApp.Helpers;
using Xunit;

namespace AvatarFormsApp.Tests;

public class BoolToVisibilityInverseConverterTest
{
    private readonly BoolToVisibilityInverseConverter _sut = new();

    // ── Convert ───────────────────────────────────────────────────────────────

    [Fact]
    public void Convert_ReturnsCollapsed_ForTrue()
    {
        var result = _sut.Convert(true, typeof(Visibility), null!, string.Empty);
        Assert.Equal(Visibility.Collapsed, result);
    }

    [Fact]
    public void Convert_ReturnsVisible_ForFalse()
    {
        var result = _sut.Convert(false, typeof(Visibility), null!, string.Empty);
        Assert.Equal(Visibility.Visible, result);
    }

    [Fact]
    public void Convert_ReturnsVisible_ForNonBoolValue()
    {
        var result = _sut.Convert("not a bool", typeof(Visibility), null!, string.Empty);
        Assert.Equal(Visibility.Visible, result);
    }

    [Fact]
    public void Convert_ReturnsVisible_ForNull()
    {
        var result = _sut.Convert(null!, typeof(Visibility), null!, string.Empty);
        Assert.Equal(Visibility.Visible, result);
    }

    [Fact]
    public void Convert_ReturnsVisible_ForInteger()
    {
        var result = _sut.Convert(1, typeof(Visibility), null!, string.Empty);
        Assert.Equal(Visibility.Visible, result);
    }

    // ── ConvertBack ───────────────────────────────────────────────────────────

    [Fact]
    public void ConvertBack_ReturnsTrue_ForCollapsed()
    {
        var result = _sut.ConvertBack(Visibility.Collapsed, typeof(bool), null!, string.Empty);
        Assert.Equal(true, result);
    }

    [Fact]
    public void ConvertBack_ReturnsFalse_ForVisible()
    {
        var result = _sut.ConvertBack(Visibility.Visible, typeof(bool), null!, string.Empty);
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

    // ── Inverse relationship with BoolToVisibilityConverter ──────────────────

    [Fact]
    public void Convert_IsOppositeOf_BoolToVisibilityConverter_ForTrue()
    {
        var normal = new BoolToVisibilityConverter();
        var normalResult = (Visibility)normal.Convert(true, typeof(Visibility), null!, string.Empty);
        var inverseResult = (Visibility)_sut.Convert(true, typeof(Visibility), null!, string.Empty);
        Assert.NotEqual(normalResult, inverseResult);
    }

    [Fact]
    public void Convert_IsOppositeOf_BoolToVisibilityConverter_ForFalse()
    {
        var normal = new BoolToVisibilityConverter();
        var normalResult = (Visibility)normal.Convert(false, typeof(Visibility), null!, string.Empty);
        var inverseResult = (Visibility)_sut.Convert(false, typeof(Visibility), null!, string.Empty);
        Assert.NotEqual(normalResult, inverseResult);
    }
}
