using AvatarFormsApp.Services;
using Xunit;

namespace AvatarFormsApp.Tests;

public class FileServiceTests : IDisposable
{
    private readonly FileService _sut = new();
    private readonly string _testDir = Path.Combine(Path.GetTempPath(), $"FileServiceTest_{Guid.NewGuid()}");

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, recursive: true);
    }

    // ── Read ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Read_ReturnsDeserializedObject_WhenFileExists()
    {
        Directory.CreateDirectory(_testDir);
        File.WriteAllText(Path.Combine(_testDir, "test.json"), "{\"Name\":\"Alice\",\"Age\":30}");

        var result = _sut.Read<TestPerson>(_testDir, "test.json");

        Assert.NotNull(result);
        Assert.Equal("Alice", result.Name);
        Assert.Equal(30, result.Age);
    }

    [Fact]
    public void Read_ReturnsDefault_WhenFileDoesNotExist()
    {
        var result = _sut.Read<TestPerson>(_testDir, "nonexistent.json");
        Assert.Null(result);
    }

    [Fact]
    public void Read_ReturnsDefault_WhenDirectoryDoesNotExist()
    {
        var result = _sut.Read<TestPerson>(Path.Combine(_testDir, "missing"), "file.json");
        Assert.Null(result);
    }

    [Fact]
    public void Read_ReturnsString_WhenFileContainsString()
    {
        Directory.CreateDirectory(_testDir);
        File.WriteAllText(Path.Combine(_testDir, "str.json"), "\"hello world\"");

        var result = _sut.Read<string>(_testDir, "str.json");
        Assert.Equal("hello world", result);
    }

    [Fact]
    public void Read_ReturnsInt_WhenFileContainsNumber()
    {
        Directory.CreateDirectory(_testDir);
        File.WriteAllText(Path.Combine(_testDir, "num.json"), "42");

        var result = _sut.Read<int>(_testDir, "num.json");
        Assert.Equal(42, result);
    }

    [Fact]
    public void Read_ReturnsList_WhenFileContainsArray()
    {
        Directory.CreateDirectory(_testDir);
        File.WriteAllText(Path.Combine(_testDir, "list.json"), "[1,2,3]");

        var result = _sut.Read<List<int>>(_testDir, "list.json");
        Assert.Equal(new List<int> { 1, 2, 3 }, result);
    }

    [Fact]
    public void Read_ReturnsDefault_WhenFileIsEmpty()
    {
        Directory.CreateDirectory(_testDir);
        File.WriteAllText(Path.Combine(_testDir, "empty.json"), "");

        var result = _sut.Read<TestPerson>(_testDir, "empty.json");
        Assert.Null(result);
    }

    // ── Save ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Save_CreatesFile_WhenDirectoryExists()
    {
        Directory.CreateDirectory(_testDir);
        _sut.Save(_testDir, "out.json", new TestPerson { Name = "Bob", Age = 25 });

        Assert.True(File.Exists(Path.Combine(_testDir, "out.json")));
    }

    [Fact]
    public void Save_CreatesDirectory_WhenItDoesNotExist()
    {
        var newDir = Path.Combine(_testDir, "newsubdir");
        _sut.Save(newDir, "out.json", "hello");

        Assert.True(Directory.Exists(newDir));
    }

    [Fact]
    public void Save_WritesCorrectJson_ForObject()
    {
        Directory.CreateDirectory(_testDir);
        _sut.Save(_testDir, "person.json", new TestPerson { Name = "Carol", Age = 40 });

        var json = File.ReadAllText(Path.Combine(_testDir, "person.json"));
        Assert.Contains("Carol", json);
        Assert.Contains("40", json);
    }

    [Fact]
    public void Save_WritesCorrectJson_ForString()
    {
        Directory.CreateDirectory(_testDir);
        _sut.Save(_testDir, "str.json", "hello");

        var json = File.ReadAllText(Path.Combine(_testDir, "str.json"));
        Assert.Contains("hello", json);
    }

    [Fact]
    public void Save_WritesCorrectJson_ForList()
    {
        Directory.CreateDirectory(_testDir);
        _sut.Save(_testDir, "list.json", new List<int> { 1, 2, 3 });

        var json = File.ReadAllText(Path.Combine(_testDir, "list.json"));
        Assert.Contains("1", json);
        Assert.Contains("2", json);
        Assert.Contains("3", json);
    }

    [Fact]
    public void Save_OverwritesExistingFile()
    {
        Directory.CreateDirectory(_testDir);
        _sut.Save(_testDir, "overwrite.json", new TestPerson { Name = "First", Age = 1 });
        _sut.Save(_testDir, "overwrite.json", new TestPerson { Name = "Second", Age = 2 });

        var json = File.ReadAllText(Path.Combine(_testDir, "overwrite.json"));
        Assert.Contains("Second", json);
        Assert.DoesNotContain("First", json);
    }

    [Fact]
    public void Save_ThenRead_RoundTripsCorrectly()
    {
        Directory.CreateDirectory(_testDir);
        var original = new TestPerson { Name = "Dave", Age = 35 };
        _sut.Save(_testDir, "roundtrip.json", original);

        var result = _sut.Read<TestPerson>(_testDir, "roundtrip.json");

        Assert.Equal(original.Name, result!.Name);
        Assert.Equal(original.Age, result.Age);
    }

    [Fact]
    public void Save_WritesNullJson_ForNullContent()
    {
        Directory.CreateDirectory(_testDir);
        _sut.Save<TestPerson?>(_testDir, "null.json", null);

        var json = File.ReadAllText(Path.Combine(_testDir, "null.json"));
        Assert.Equal("null", json);
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    [Fact]
    public void Delete_RemovesFile_WhenFileExists()
    {
        Directory.CreateDirectory(_testDir);
        var filePath = Path.Combine(_testDir, "todelete.json");
        File.WriteAllText(filePath, "{}");

        _sut.Delete(_testDir, "todelete.json");

        Assert.False(File.Exists(filePath));
    }

    [Fact]
    public void Delete_DoesNotThrow_WhenFileDoesNotExist()
    {
        Directory.CreateDirectory(_testDir);
        var ex = Record.Exception(() => _sut.Delete(_testDir, "nonexistent.json"));
        Assert.Null(ex);
    }

    [Fact]
    public void Delete_DoesNotThrow_WhenFileNameIsNull()
    {
        Directory.CreateDirectory(_testDir);
        var ex = Record.Exception(() => _sut.Delete(_testDir, null!));
        Assert.Null(ex);
    }

    [Fact]
    public void Delete_DoesNotThrow_WhenDirectoryDoesNotExist()
    {
        var ex = Record.Exception(() => _sut.Delete(Path.Combine(_testDir, "missing"), "file.json"));
        Assert.Null(ex);
    }

    [Fact]
    public void Delete_LeavesOtherFiles_Intact()
    {
        Directory.CreateDirectory(_testDir);
        File.WriteAllText(Path.Combine(_testDir, "keep.json"), "{}");
        File.WriteAllText(Path.Combine(_testDir, "delete.json"), "{}");

        _sut.Delete(_testDir, "delete.json");

        Assert.True(File.Exists(Path.Combine(_testDir, "keep.json")));
        Assert.False(File.Exists(Path.Combine(_testDir, "delete.json")));
    }
}

// ── Test model ────────────────────────────────────────────────────────────────

public class TestPerson
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
}
