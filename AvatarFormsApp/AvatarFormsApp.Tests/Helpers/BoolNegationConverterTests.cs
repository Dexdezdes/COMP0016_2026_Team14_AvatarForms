using AvatarFormsApp.Helpers;
using Xunit;

namespace AvatarFormsApp.Tests;

public class BoolNegationConverterTests
{
    private readonly BoolNegationConverter _sut = new();

    // ── Convert ───────────────────────────────────────────────────────────────

    [Fact]
    public void Convert_ReturnsFalse_ForTrue()
    {
        var result = _sut.Convert(true, typeof(bool), null!, string.Empty);
        Assert.Equal(false, result);
    }

    [Fact]
    public void Convert_ReturnsTrue_ForFalse()
    {
        var result = _sut.Convert(false, typeof(bool), null!, string.Empty);
        Assert.Equal(true, result);
    }

    [Fact]
    public void Convert_ReturnsFalse_ForNull()
    {
        var result = _sut.Convert(null!, typeof(bool), null!, string.Empty);
        Assert.Equal(false, result);
    }

    [Fact]
    public void Convert_ReturnsFalse_ForString()
    {
        var result = _sut.Convert("true", typeof(bool), null!, string.Empty);
        Assert.Equal(false, result);
    }

    [Fact]
    public void Convert_ReturnsFalse_ForInteger()
    {
        var result = _sut.Convert(1, typeof(bool), null!, string.Empty);
        Assert.Equal(false, result);
    }

    // ── ConvertBack ───────────────────────────────────────────────────────────

    [Fact]
    public void ConvertBack_ReturnsFalse_ForTrue()
    {
        var result = _sut.ConvertBack(true, typeof(bool), null!, string.Empty);
        Assert.Equal(false, result);
    }

    [Fact]
    public void ConvertBack_ReturnsTrue_ForFalse()
    {
        var result = _sut.ConvertBack(false, typeof(bool), null!, string.Empty);
        Assert.Equal(true, result);
    }

    [Fact]
    public void ConvertBack_ReturnsFalse_ForNull()
    {
        var result = _sut.ConvertBack(null!, typeof(bool), null!, string.Empty);
        Assert.Equal(false, result);
    }

    [Fact]
    public void ConvertBack_ReturnsFalse_ForString()
    {
        var result = _sut.ConvertBack("false", typeof(bool), null!, string.Empty);
        Assert.Equal(false, result);
    }

    // ── Round-trip ────────────────────────────────────────────────────────────

    [Fact]
    public void RoundTrip_True_StaysTrue()
    {
        var negated = _sut.Convert(true, typeof(bool), null!, string.Empty);
        var result = _sut.ConvertBack(negated, typeof(bool), null!, string.Empty);
        Assert.Equal(true, result);
    }

    [Fact]
    public void RoundTrip_False_StaysFalse()
    {
        var negated = _sut.Convert(false, typeof(bool), null!, string.Empty);
        var result = _sut.ConvertBack(negated, typeof(bool), null!, string.Empty);
        Assert.Equal(false, result);
    }

    // ── Convert and ConvertBack are symmetric ─────────────────────────────────

    [Fact]
    public void Convert_And_ConvertBack_AreSymmetric_ForTrue()
    {
        Assert.Equal(_sut.Convert(true, typeof(bool), null!, string.Empty),
                     _sut.ConvertBack(true, typeof(bool), null!, string.Empty));
    }

    [Fact]
    public void Convert_And_ConvertBack_AreSymmetric_ForFalse()
    {
        Assert.Equal(_sut.Convert(false, typeof(bool), null!, string.Empty),
                     _sut.ConvertBack(false, typeof(bool), null!, string.Empty));
    }
}
