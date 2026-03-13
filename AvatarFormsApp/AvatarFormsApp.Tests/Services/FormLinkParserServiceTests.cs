using System.Reflection;
using System.Text.Json.Nodes;
using AvatarFormsApp.Services;
using Xunit;

namespace AvatarFormsApp.Tests.Services;

public class FormLinkParserServiceTests
{
    private readonly FormLinkParserService _sut = new();

    // Reflection helpers
    private T InvokeStatic<T>(string methodName, params object?[] args)
    {
        var method = typeof(FormLinkParserService)
            .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)!;
        return (T)method.Invoke(null, args)!;
    }

    private T InvokeInstance<T>(string methodName, params object?[] args)
    {
        var method = typeof(FormLinkParserService)
            .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (T)method.Invoke(_sut, args)!;
    }

    // ── CleanHtml ─────────────────────────────────────────────────────────────

    [Fact]
    public void CleanHtml_RemovesHtmlTags()
    {
        Assert.Equal("Hello World", InvokeStatic<string>("CleanHtml", "<b>Hello</b> World"));
    }

    [Fact]
    public void CleanHtml_ReplacesNbsp()
    {
        Assert.Equal("Hello World", InvokeStatic<string>("CleanHtml", "Hello&nbsp;World"));
    }

    [Fact]
    public void CleanHtml_ReturnsEmpty_WhenNull()
    {
        Assert.Equal(string.Empty, InvokeStatic<string>("CleanHtml", new object?[] { null }));
    }

    [Fact]
    public void CleanHtml_ReturnsEmpty_WhenEmpty()
    {
        Assert.Equal(string.Empty, InvokeStatic<string>("CleanHtml", string.Empty));
    }

    [Fact]
    public void CleanHtml_Trims_Whitespace()
    {
        Assert.Equal("Hello", InvokeStatic<string>("CleanHtml", "  Hello  "));
    }

    [Fact]
    public void CleanHtml_RemovesNestedTags()
    {
        Assert.Equal("text", InvokeStatic<string>("CleanHtml", "<div><span>text</span></div>"));
    }

    // ── SafeString ────────────────────────────────────────────────────────────

    [Fact]
    public void SafeString_ReturnsString_ForStringNode()
    {
        var node = JsonNode.Parse("\"hello\"");
        Assert.Equal("hello", InvokeStatic<string?>("SafeString", node));
    }

    [Fact]
    public void SafeString_ReturnsNull_ForNull()
    {
        Assert.Null(InvokeStatic<string?>("SafeString", new object?[] { null }));
    }

    [Fact]
    public void SafeString_ReturnsNull_ForObjectNode()
    {
        var node = JsonNode.Parse("{\"key\":\"value\"}");
        Assert.Null(InvokeStatic<string?>("SafeString", node));
    }

    [Fact]
    public void SafeString_ReturnsStringified_ForNumber()
    {
        var node = JsonNode.Parse("42");
        Assert.Equal("42", InvokeStatic<string?>("SafeString", node));
    }

    // ── SafeDouble ────────────────────────────────────────────────────────────

    [Fact]
    public void SafeDouble_ReturnsValue_ForNumberNode()
    {
        var node = JsonNode.Parse("3.14");
        Assert.Equal(3.14, InvokeStatic<double?>("SafeDouble", node));
    }

    [Fact]
    public void SafeDouble_ReturnsNull_ForNull()
    {
        Assert.Null(InvokeStatic<double?>("SafeDouble", new object?[] { null }));
    }

    [Fact]
    public void SafeDouble_ReturnsNull_ForStringNode()
    {
        var node = JsonNode.Parse("\"hello\"");
        Assert.Null(InvokeStatic<double?>("SafeDouble", node));
    }

    [Fact]
    public void SafeDouble_ReturnsValue_ForIntegerNode()
    {
        var node = JsonNode.Parse("5");
        Assert.Equal(5.0, InvokeStatic<double?>("SafeDouble", node));
    }

    // ── SafeBool ──────────────────────────────────────────────────────────────

    [Fact]
    public void SafeBool_ReturnsTrue_ForTrueNode()
    {
        var node = JsonNode.Parse("true");
        Assert.True(InvokeStatic<bool?>("SafeBool", node));
    }

    [Fact]
    public void SafeBool_ReturnsFalse_ForFalseNode()
    {
        var node = JsonNode.Parse("false");
        Assert.False(InvokeStatic<bool?>("SafeBool", node));
    }

    [Fact]
    public void SafeBool_ReturnsNull_ForNull()
    {
        Assert.Null(InvokeStatic<bool?>("SafeBool", new object?[] { null }));
    }

    [Fact]
    public void SafeBool_ReturnsNull_ForStringNode()
    {
        var node = JsonNode.Parse("\"true\"");
        Assert.Null(InvokeStatic<bool?>("SafeBool", node));
    }

    // ── MapGoogleType ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0, "ShortText")]
    [InlineData(1, "Paragraph")]
    [InlineData(2, "MultipleChoice")]
    [InlineData(3, "Checkboxes")]
    [InlineData(4, "Dropdown")]
    [InlineData(5, "LinearScale")]
    [InlineData(7, "Grid")]
    [InlineData(9, "Date")]
    [InlineData(10, "Time")]
    [InlineData(99, "ShortText")]
    public void MapGoogleType_ReturnsCorrectType(int typeNum, string expected)
    {
        Assert.Equal(expected, InvokeStatic<string>("MapGoogleType", typeNum));
    }

    // ── ParseModernMsJson ─────────────────────────────────────────────────────

    [Fact]
    public void ParseModernMsJson_ReturnsEmpty_WhenNoFormNode()
    {
        var json = JsonNode.Parse("{\"something\":\"else\"}")!;
        var result = InvokeInstance<List<ParsedQuestion>>("ParseModernMsJson", json);
        Assert.Empty(result);
    }

    [Fact]
    public void ParseModernMsJson_ParsesQuestionsFromFormKey()
    {
        var json = JsonNode.Parse("""
        {
            "form": {
                "title": "Test Form",
                "questions": [
                    {
                        "id": "q1",
                        "title": "What is your name?",
                        "type": "Question.Text",
                        "required": true,
                        "order": 1
                    }
                ]
            }
        }
        """)!;

        var result = InvokeInstance<List<ParsedQuestion>>("ParseModernMsJson", json);

        Assert.Single(result);
        Assert.Equal("What is your name?", result[0].Title);
        Assert.Equal("Question.Text", result[0].Type);
        Assert.True(result[0].Required);
    }

    [Fact]
    public void ParseModernMsJson_SetsFormTitle()
    {
        var json = JsonNode.Parse("""
        {
            "form": {
                "title": "My Survey",
                "questions": []
            }
        }
        """)!;

        InvokeInstance<List<ParsedQuestion>>("ParseModernMsJson", json);

        Assert.Equal("My Survey", _sut.FormTitle);
    }

    [Fact]
    public void ParseModernMsJson_SkipsColumnGroupQuestions()
    {
        var json = JsonNode.Parse("""
        {
            "form": {
                "questions": [
                    { "id": "s1", "type": "Question.ColumnGroup", "title": "Section 1", "order": 1 },
                    { "id": "q1", "title": "Real question", "type": "Question.Text", "order": 2 }
                ]
            }
        }
        """)!;

        var result = InvokeInstance<List<ParsedQuestion>>("ParseModernMsJson", json);

        Assert.Single(result);
        Assert.Equal("Real question", result[0].Title);
    }

    [Fact]
    public void ParseModernMsJson_SetsSection_FromColumnGroup()
    {
        var json = JsonNode.Parse("""
        {
            "form": {
                "questions": [
                    { "id": "s1", "type": "Question.ColumnGroup", "title": "Section A", "order": 1 },
                    { "id": "q1", "title": "Q in section", "type": "Question.Text", "order": 2 }
                ]
            }
        }
        """)!;

        var result = InvokeInstance<List<ParsedQuestion>>("ParseModernMsJson", json);

        Assert.Equal("Section A", result[0].Section);
    }

    [Fact]
    public void ParseModernMsJson_IndexesQuestionsStartingAt1()
    {
        var json = JsonNode.Parse("""
        {
            "form": {
                "questions": [
                    { "id": "q1", "title": "Q1", "type": "Question.Text", "order": 1 },
                    { "id": "q2", "title": "Q2", "type": "Question.Text", "order": 2 }
                ]
            }
        }
        """)!;

        var result = InvokeInstance<List<ParsedQuestion>>("ParseModernMsJson", json);

        Assert.Equal(1, result[0].Index);
        Assert.Equal(2, result[1].Index);
    }

    [Fact]
    public void ParseModernMsJson_ParsesChoices()
    {
        var json = JsonNode.Parse("""
        {
            "form": {
                "questions": [
                    {
                        "id": "q1",
                        "title": "Pick one",
                        "type": "Question.Choice",
                        "order": 1,
                        "choices": [
                            { "displayText": "Option A" },
                            { "displayText": "Option B" }
                        ]
                    }
                ]
            }
        }
        """)!;

        var result = InvokeInstance<List<ParsedQuestion>>("ParseModernMsJson", json);

        Assert.Single(result);
        Assert.Equal(2, result[0].Options.Count);

        // Extract the strings from the EditableOption objects for comparison
        var optionTexts = result[0].Options.Select(o => o.Text).ToList();
        Assert.Contains("Option A", optionTexts);
        Assert.Contains("Option B", optionTexts);
    }

    [Fact]
    public void ParseModernMsJson_ParsesFromRootQuestionsKey()
    {
        var json = JsonNode.Parse("""
        {
            "questions": [
                { "id": "q1", "title": "Flat question", "type": "Question.Text", "order": 1 }
            ]
        }
        """)!;

        var result = InvokeInstance<List<ParsedQuestion>>("ParseModernMsJson", json);

        Assert.Single(result);
        Assert.Equal("Flat question", result[0].Title);
    }

    // ── ParseGoogleFormsData ──────────────────────────────────────────────────

    [Fact]
    public void ParseGoogleFormsData_ReturnsEmpty_ForEmptyArray()
    {
        var json = JsonNode.Parse("[]")!;
        var result = InvokeInstance<List<ParsedQuestion>>("ParseGoogleFormsData", json);
        Assert.Empty(result);
    }

    [Fact]
    public void ParseGoogleFormsData_ParsesShortTextQuestion()
    {
        var json = JsonNode.Parse("""
        [
            null, ["Form description"], null, "Form Title", null,
            [
                [123456, "What is your name?", null, 0, [[789, []]]]
            ]
        ]
        """)!;

        var result = InvokeInstance<List<ParsedQuestion>>("ParseGoogleFormsData", json);

        Assert.Single(result);
        Assert.Equal("What is your name?", result[0].Title);
        Assert.Equal("ShortText", result[0].Type);
    }

    [Fact]
    public void ParseGoogleFormsData_ParsesMultipleChoiceWithOptions()
    {
        var json = JsonNode.Parse("""
        [
            null, [""], null, "Survey", null,
            [
                [111, "Favourite color?", null, 2, [[999, [["Red"], ["Blue"], ["Green"]]]]]
            ]
        ]
        """)!;

        var result = InvokeInstance<List<ParsedQuestion>>("ParseGoogleFormsData", json);

        Assert.Single(result);
        Assert.Equal("MultipleChoice", result[0].Type);
        Assert.Equal(3, result[0].Options.Count);

        // Project the 'Text' property from EditableOption
        var optionTexts = result[0].Options.Select(o => o.Text).ToList();
        Assert.Contains("Red", optionTexts);
    }

    [Fact]
    public void ParseGoogleFormsData_SetsFormTitle()
    {
        var json = JsonNode.Parse("""
        [null, [""], null, "My Google Form", null, []]
        """)!;

        InvokeInstance<List<ParsedQuestion>>("ParseGoogleFormsData", json);

        Assert.Equal("My Google Form", _sut.FormTitle);
    }

    [Fact]
    public void ParseGoogleFormsData_SkipsSectionHeaders_Type8()
    {
        var json = JsonNode.Parse("""
        [
            null, [""], null, "Form", null,
            [
                [1, "Section Header", null, 8, []],
                [2, "Real Question", null, 0, [[3, []]]]
            ]
        ]
        """)!;

        var result = InvokeInstance<List<ParsedQuestion>>("ParseGoogleFormsData", json);

        Assert.Single(result);
        Assert.Equal("Real Question", result[0].Title);
    }
}
