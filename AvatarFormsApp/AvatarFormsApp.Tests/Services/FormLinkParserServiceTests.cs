using System.Collections.ObjectModel;
using System.Reflection;
using System.Text.Json.Nodes;
using AvatarFormsApp.Services;
using Xunit;

namespace AvatarFormsApp.Tests.Services;

public class FormLinkParserServiceTests
{
    private readonly FormLinkParserService _sut = new();

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
        => Assert.Equal("Hello World", InvokeStatic<string>("CleanHtml", "<b>Hello</b> World"));

    [Fact]
    public void CleanHtml_ReplacesNbsp()
        => Assert.Equal("Hello World", InvokeStatic<string>("CleanHtml", "Hello&nbsp;World"));

    [Fact]
    public void CleanHtml_ReturnsEmpty_WhenNull()
        => Assert.Equal(string.Empty, InvokeStatic<string>("CleanHtml", new object?[] { null }));

    [Fact]
    public void CleanHtml_ReturnsEmpty_WhenEmpty()
        => Assert.Equal(string.Empty, InvokeStatic<string>("CleanHtml", string.Empty));

    [Fact]
    public void CleanHtml_Trims_Whitespace()
        => Assert.Equal("Hello", InvokeStatic<string>("CleanHtml", "  Hello  "));

    [Fact]
    public void CleanHtml_RemovesNestedTags()
        => Assert.Equal("text", InvokeStatic<string>("CleanHtml", "<div><span>text</span></div>"));

    [Fact]
    public void CleanHtml_PlainText_Unchanged()
        => Assert.Equal("plain text", InvokeStatic<string>("CleanHtml", "plain text"));

    // ── SafeString ────────────────────────────────────────────────────────────

    [Fact]
    public void SafeString_ReturnsString_ForStringNode()
        => Assert.Equal("hello", InvokeStatic<string?>("SafeString", JsonNode.Parse("\"hello\"")));

    [Fact]
    public void SafeString_ReturnsNull_ForNull()
        => Assert.Null(InvokeStatic<string?>("SafeString", new object?[] { null }));

    [Fact]
    public void SafeString_ReturnsNull_ForObjectNode()
        => Assert.Null(InvokeStatic<string?>("SafeString", JsonNode.Parse("{\"key\":\"value\"}")));

    [Fact]
    public void SafeString_ReturnsStringified_ForNumber()
        => Assert.Equal("42", InvokeStatic<string?>("SafeString", JsonNode.Parse("42")));

    // ── SafeDouble ────────────────────────────────────────────────────────────

    [Fact]
    public void SafeDouble_ReturnsValue_ForNumberNode()
        => Assert.Equal(3.14, InvokeStatic<double?>("SafeDouble", JsonNode.Parse("3.14")));

    [Fact]
    public void SafeDouble_ReturnsNull_ForNull()
        => Assert.Null(InvokeStatic<double?>("SafeDouble", new object?[] { null }));

    [Fact]
    public void SafeDouble_ReturnsNull_ForStringNode()
        => Assert.Null(InvokeStatic<double?>("SafeDouble", JsonNode.Parse("\"hello\"")));

    [Fact]
    public void SafeDouble_ReturnsValue_ForIntegerNode()
        => Assert.Equal(5.0, InvokeStatic<double?>("SafeDouble", JsonNode.Parse("5")));

    // ── SafeBool ──────────────────────────────────────────────────────────────

    [Fact]
    public void SafeBool_ReturnsTrue_ForTrueNode()
        => Assert.True(InvokeStatic<bool?>("SafeBool", JsonNode.Parse("true")));

    [Fact]
    public void SafeBool_ReturnsFalse_ForFalseNode()
        => Assert.False(InvokeStatic<bool?>("SafeBool", JsonNode.Parse("false")));

    [Fact]
    public void SafeBool_ReturnsNull_ForNull()
        => Assert.Null(InvokeStatic<bool?>("SafeBool", new object?[] { null }));

    [Fact]
    public void SafeBool_ReturnsNull_ForStringNode()
        => Assert.Null(InvokeStatic<bool?>("SafeBool", JsonNode.Parse("\"true\"")));

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
        => Assert.Equal(expected, InvokeStatic<string>("MapGoogleType", typeNum));

    // ── ParsedQuestion computed properties ───────────────────────────────────

    [Fact]
    public void ParsedQuestion_HasOptions_False_WhenEmpty()
    {
        var q = new ParsedQuestion();
        Assert.False(q.HasOptions);
    }

    [Fact]
    public void ParsedQuestion_HasOptions_True_WhenOptionsAdded()
    {
        var q = new ParsedQuestion();
        q.Options.Add(new EditableOption { Text = "A" });
        Assert.True(q.HasOptions);
    }

    [Fact]
    public void ParsedQuestion_TypeLabel_IsMultipleChoice_WhenHasOptions()
    {
        var q = new ParsedQuestion();
        q.Options.Add(new EditableOption { Text = "A" });
        Assert.Equal("Multiple Choice", q.TypeLabel);
    }

    [Fact]
    public void ParsedQuestion_TypeLabel_IsOpenEnded_WhenNoOptions()
    {
        var q = new ParsedQuestion();
        Assert.Equal("Open Ended", q.TypeLabel);
    }

    [Fact]
    public void EditableOption_DefaultText_IsEmpty()
    {
        var o = new EditableOption();
        Assert.Equal(string.Empty, o.Text);
    }

    [Fact]
    public void EditableOption_Text_CanBeSet()
    {
        var o = new EditableOption { Text = "Option A" };
        Assert.Equal("Option A", o.Text);
    }

    // ── ExtractChoices ────────────────────────────────────────────────────────

    [Fact]
    public void ExtractChoices_ReturnsOptions_FromDisplayText()
    {
        var json = JsonNode.Parse("""
        {
            "choices": [
                { "displayText": "Alpha" },
                { "displayText": "Beta" }
            ]
        }
        """)!;

        var result = InvokeInstance<ObservableCollection<EditableOption>>("ExtractChoices", json);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, o => o.Text == "Alpha");
        Assert.Contains(result, o => o.Text == "Beta");
    }

    [Fact]
    public void ExtractChoices_ReturnsOptions_FromDisplayValue()
    {
        var json = JsonNode.Parse("""
        {
            "choices": [
                { "displayValue": "Option1" }
            ]
        }
        """)!;

        var result = InvokeInstance<ObservableCollection<EditableOption>>("ExtractChoices", json);

        Assert.Single(result);
        Assert.Equal("Option1", result[0].Text);
    }

    [Fact]
    public void ExtractChoices_ReturnsOptions_FromValueKey()
    {
        var json = JsonNode.Parse("""
        {
            "choices": [
                { "value": "Val1" }
            ]
        }
        """)!;

        var result = InvokeInstance<ObservableCollection<EditableOption>>("ExtractChoices", json);

        Assert.Single(result);
        Assert.Equal("Val1", result[0].Text);
    }

    [Fact]
    public void ExtractChoices_ReturnsOptions_FromDescription()
    {
        var json = JsonNode.Parse("""
        {
            "choices": [
                { "Description": "Desc1" }
            ]
        }
        """)!;

        var result = InvokeInstance<ObservableCollection<EditableOption>>("ExtractChoices", json);

        Assert.Single(result);
        Assert.Equal("Desc1", result[0].Text);
    }

    [Fact]
    public void ExtractChoices_ReturnsEmpty_WhenNoChoices()
    {
        var json = JsonNode.Parse("{\"title\": \"Q\"}")!;
        var result = InvokeInstance<ObservableCollection<EditableOption>>("ExtractChoices", json);
        Assert.Empty(result);
    }

    [Fact]
    public void ExtractChoices_ReturnsOptions_FromPropertiesChoices()
    {
        var json = JsonNode.Parse("""
        {
            "properties": {
                "choices": [
                    { "displayText": "PropOption" }
                ]
            }
        }
        """)!;

        var result = InvokeInstance<ObservableCollection<EditableOption>>("ExtractChoices", json);

        Assert.Single(result);
        Assert.Equal("PropOption", result[0].Text);
    }

    [Fact]
    public void ExtractChoices_ReturnsOptions_FromQuestionInfo()
    {
        var questionInfoJson = """{"Choices":[{"displayText":"InfoOpt"}]}""";
        var escapedJson = System.Text.Json.JsonSerializer.Serialize(questionInfoJson);
        var json = JsonNode.Parse($"{{\"questionInfo\": {escapedJson}}}")!;

        var result = InvokeInstance<ObservableCollection<EditableOption>>("ExtractChoices", json);

        Assert.Single(result);
        Assert.Equal("InfoOpt", result[0].Text);
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
                    { "id": "q1", "title": "What is your name?", "type": "Question.Text", "required": true, "order": 1 }
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
        { "form": { "title": "My Survey", "questions": [] } }
        """)!;

        InvokeInstance<List<ParsedQuestion>>("ParseModernMsJson", json);

        Assert.Equal("My Survey", _sut.FormTitle);
    }

    [Fact]
    public void ParseModernMsJson_SetsFormDescription()
    {
        var json = JsonNode.Parse("""
        { "form": { "title": "T", "description": "My Desc", "questions": [] } }
        """)!;

        InvokeInstance<List<ParsedQuestion>>("ParseModernMsJson", json);

        Assert.Equal("My Desc", _sut.FormDescription);
    }

    [Fact]
    public void ParseModernMsJson_ParsesFromDataFormKey()
    {
        var json = JsonNode.Parse("""
        {
            "data": {
                "form": {
                    "title": "Nested",
                    "questions": [
                        { "id": "q1", "title": "Q1", "type": "Question.Text", "order": 1 }
                    ]
                }
            }
        }
        """)!;

        var result = InvokeInstance<List<ParsedQuestion>>("ParseModernMsJson", json);

        Assert.Single(result);
        Assert.Equal("Q1", result[0].Title);
    }

    [Fact]
    public void ParseModernMsJson_ParsesFromRootQuestionsKey()
    {
        var json = JsonNode.Parse("""
        { "questions": [ { "id": "q1", "title": "Flat question", "type": "Question.Text", "order": 1 } ] }
        """)!;

        var result = InvokeInstance<List<ParsedQuestion>>("ParseModernMsJson", json);

        Assert.Single(result);
        Assert.Equal("Flat question", result[0].Title);
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
    public void ParseModernMsJson_SkipsMatrixChoiceGroup()
    {
        var json = JsonNode.Parse("""
        {
            "form": {
                "questions": [
                    { "id": "mg1", "type": "Question.MatrixChoiceGroup", "title": "Matrix Group", "order": 1 },
                    { "id": "q1", "title": "Real Q", "type": "Question.Text", "order": 2 }
                ]
            }
        }
        """)!;

        var result = InvokeInstance<List<ParsedQuestion>>("ParseModernMsJson", json);

        Assert.Single(result);
        Assert.Equal("Real Q", result[0].Title);
    }

    [Fact]
    public void ParseModernMsJson_SetsSubtitle_WhenPresent()
    {
        var json = JsonNode.Parse("""
        {
            "form": {
                "questions": [
                    { "id": "q1", "title": "Q1", "type": "Question.Text", "order": 1, "subtitle": "A hint" }
                ]
            }
        }
        """)!;

        var result = InvokeInstance<List<ParsedQuestion>>("ParseModernMsJson", json);

        Assert.Single(result);
        Assert.Equal("A hint", result[0].Subtitle);
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
                        "id": "q1", "title": "Pick one", "type": "Question.Choice", "order": 1,
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
        Assert.Contains(result[0].Options, o => o.Text == "Option A");
        Assert.Contains(result[0].Options, o => o.Text == "Option B");
    }

    [Fact]
    public void ParseModernMsJson_SetsRequiredFalse_WhenNotSet()
    {
        var json = JsonNode.Parse("""
        {
            "form": {
                "questions": [
                    { "id": "q1", "title": "Q1", "type": "Question.Text", "order": 1 }
                ]
            }
        }
        """)!;

        var result = InvokeInstance<List<ParsedQuestion>>("ParseModernMsJson", json);

        Assert.False(result[0].Required);
    }

    [Fact]
    public void ParseModernMsJson_OrdersQuestionsByOrder()
    {
        var json = JsonNode.Parse("""
        {
            "form": {
                "questions": [
                    { "id": "q2", "title": "Second", "type": "Question.Text", "order": 2 },
                    { "id": "q1", "title": "First",  "type": "Question.Text", "order": 1 }
                ]
            }
        }
        """)!;

        var result = InvokeInstance<List<ParsedQuestion>>("ParseModernMsJson", json);

        Assert.Equal("First", result[0].Title);
        Assert.Equal("Second", result[1].Title);
    }

    // ── ParseGoogleFormsData ──────────────────────────────────────────────────

    [Fact]
    public void ParseGoogleFormsData_ReturnsEmpty_ForEmptyArray()
    {
        var result = InvokeInstance<List<ParsedQuestion>>("ParseGoogleFormsData", JsonNode.Parse("[]")!);
        Assert.Empty(result);
    }

    [Fact]
    public void ParseGoogleFormsData_ParsesShortTextQuestion()
    {
        var json = JsonNode.Parse("""
        [ null, ["Form description"], null, "Form Title", null, [ [123456, "What is your name?", null, 0, [[789, []]]] ] ]
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
        [ null, [""], null, "Survey", null, [ [111, "Favourite color?", null, 2, [[999, [["Red"], ["Blue"], ["Green"]]]]] ] ]
        """)!;

        var result = InvokeInstance<List<ParsedQuestion>>("ParseGoogleFormsData", json);

        Assert.Single(result);
        Assert.Equal("MultipleChoice", result[0].Type);
        Assert.Equal(3, result[0].Options.Count);
        Assert.Contains(result[0].Options, o => o.Text == "Red");
    }

    [Fact]
    public void ParseGoogleFormsData_SetsFormTitle()
    {
        var json = JsonNode.Parse("[null, [\"\"], null, \"My Google Form\", null, []]")!;
        InvokeInstance<List<ParsedQuestion>>("ParseGoogleFormsData", json);
        Assert.Equal("My Google Form", _sut.FormTitle);
    }

    [Fact]
    public void ParseGoogleFormsData_SetsFormDescription()
    {
        var json = JsonNode.Parse("[null, [\"My description\"], null, \"Title\", null, []]")!;
        InvokeInstance<List<ParsedQuestion>>("ParseGoogleFormsData", json);
        Assert.Equal("My description", _sut.FormDescription);
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

    [Fact]
    public void ParseGoogleFormsData_IndexesQuestionsStartingAt1()
    {
        var json = JsonNode.Parse("""
        [
            null, [""], null, "Form", null,
            [
                [1, "Q1", null, 0, [[10, []]]],
                [2, "Q2", null, 0, [[11, []]]]
            ]
        ]
        """)!;

        var result = InvokeInstance<List<ParsedQuestion>>("ParseGoogleFormsData", json);

        Assert.Equal(1, result[0].Index);
        Assert.Equal(2, result[1].Index);
    }

    [Fact]
    public void ParseGoogleFormsData_ParsesParagraphType()
    {
        var json = JsonNode.Parse("""
        [ null, [""], null, "Form", null, [ [1, "Describe yourself", null, 1, [[2, []]]] ] ]
        """)!;

        var result = InvokeInstance<List<ParsedQuestion>>("ParseGoogleFormsData", json);

        Assert.Single(result);
        Assert.Equal("Paragraph", result[0].Type);
    }

    [Fact]
    public void ParseGoogleFormsData_ParsesDropdownType()
    {
        var json = JsonNode.Parse("""
        [ null, [""], null, "Form", null, [ [1, "Choose one", null, 4, [[2, [["A"], ["B"]]]]] ] ]
        """)!;

        var result = InvokeInstance<List<ParsedQuestion>>("ParseGoogleFormsData", json);

        Assert.Single(result);
        Assert.Equal("Dropdown", result[0].Type);
    }

    [Fact]
    public void ParseGoogleFormsData_ParsesDateType()
    {
        var json = JsonNode.Parse("""
        [ null, [""], null, "Form", null, [ [1, "When?", null, 9, [[2, []]]] ] ]
        """)!;

        var result = InvokeInstance<List<ParsedQuestion>>("ParseGoogleFormsData", json);

        Assert.Single(result);
        Assert.Equal("Date", result[0].Type);
    }

    [Fact]
    public void ParseGoogleFormsData_HandlesHtmlInTitle()
    {
        var json = JsonNode.Parse("""
        [ null, [""], null, "Form", null, [ [1, "<b>Bold Question</b>", null, 0, [[2, []]]] ] ]
        """)!;

        var result = InvokeInstance<List<ParsedQuestion>>("ParseGoogleFormsData", json);

        Assert.Single(result);
        Assert.Equal("Bold Question", result[0].Title);
    }
}
