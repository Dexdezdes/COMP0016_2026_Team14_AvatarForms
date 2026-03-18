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
    [Fact]
    public void SafeDouble_ReturnsValue_ForZero()
        => Assert.Equal(0.0, InvokeStatic<double?>("SafeDouble", JsonNode.Parse("0")));
    [Fact]
    public void SafeDouble_ReturnsNull_ForBoolNode()
        => Assert.Null(InvokeStatic<double?>("SafeDouble", JsonNode.Parse("true")));

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
    [Fact]
    public void SafeBool_ReturnsNull_ForNumberNode()
        => Assert.Null(InvokeStatic<bool?>("SafeBool", JsonNode.Parse("1")));

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
    [InlineData(6, "ShortText")]
    [InlineData(99, "ShortText")]
    [InlineData(-1, "ShortText")]
    public void MapGoogleType_ReturnsCorrectType(int typeNum, string expected)
        => Assert.Equal(expected, InvokeStatic<string>("MapGoogleType", typeNum));

    // ── ParsedQuestion computed properties ───────────────────────────────────

    [Fact]
    public void ParsedQuestion_HasOptions_False_WhenEmpty()
        => Assert.False(new ParsedQuestion().HasOptions);
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
        => Assert.Equal("Open Ended", new ParsedQuestion().TypeLabel);
    [Fact]
    public void EditableOption_DefaultText_IsEmpty()
        => Assert.Equal(string.Empty, new EditableOption().Text);
    [Fact]
    public void EditableOption_Text_CanBeSet()
        => Assert.Equal("Option A", new EditableOption { Text = "Option A" }.Text);

    // ── ExtractChoices ────────────────────────────────────────────────────────

    [Fact]
    public void ExtractChoices_ReturnsOptions_FromDisplayText()
    {
        var json = JsonNode.Parse("""{"choices":[{"displayText":"Alpha"},{"displayText":"Beta"}]}""")!;
        var result = InvokeInstance<ObservableCollection<EditableOption>>("ExtractChoices", json);
        Assert.Equal(2, result.Count);
        Assert.Contains(result, o => o.Text == "Alpha");
        Assert.Contains(result, o => o.Text == "Beta");
    }

    [Fact]
    public void ExtractChoices_ReturnsOptions_FromDisplayValue()
    {
        var json = JsonNode.Parse("""{"choices":[{"displayValue":"Option1"}]}""")!;
        var result = InvokeInstance<ObservableCollection<EditableOption>>("ExtractChoices", json);
        Assert.Single(result);
        Assert.Equal("Option1", result[0].Text);
    }

    [Fact]
    public void ExtractChoices_ReturnsOptions_FromValueKey()
    {
        var json = JsonNode.Parse("""{"choices":[{"value":"Val1"}]}""")!;
        var result = InvokeInstance<ObservableCollection<EditableOption>>("ExtractChoices", json);
        Assert.Single(result);
        Assert.Equal("Val1", result[0].Text);
    }

    [Fact]
    public void ExtractChoices_ReturnsOptions_FromDescription()
    {
        var json = JsonNode.Parse("""{"choices":[{"Description":"Desc1"}]}""")!;
        var result = InvokeInstance<ObservableCollection<EditableOption>>("ExtractChoices", json);
        Assert.Single(result);
        Assert.Equal("Desc1", result[0].Text);
    }

    [Fact]
    public void ExtractChoices_ReturnsOptions_FromFormsProDisplayRTText()
    {
        var json = JsonNode.Parse("""{"choices":[{"FormsProDisplayRTText":"RTOpt"}]}""")!;
        var result = InvokeInstance<ObservableCollection<EditableOption>>("ExtractChoices", json);
        Assert.Single(result);
        Assert.Equal("RTOpt", result[0].Text);
    }

    [Fact]
    public void ExtractChoices_ReturnsEmpty_WhenNoChoices()
    {
        var json = JsonNode.Parse("""{"title":"Q"}""")!;
        var result = InvokeInstance<ObservableCollection<EditableOption>>("ExtractChoices", json);
        Assert.Empty(result);
    }

    [Fact]
    public void ExtractChoices_ReturnsEmpty_WhenChoicesArrayIsEmpty()
    {
        var json = JsonNode.Parse("""{"choices":[]}""")!;
        var result = InvokeInstance<ObservableCollection<EditableOption>>("ExtractChoices", json);
        Assert.Empty(result);
    }

    [Fact]
    public void ExtractChoices_SkipsEmptyTextOptions()
    {
        var json = JsonNode.Parse("""{"choices":[{"displayText":""},{"displayText":"Valid"}]}""")!;
        var result = InvokeInstance<ObservableCollection<EditableOption>>("ExtractChoices", json);
        Assert.Single(result);
        Assert.Equal("Valid", result[0].Text);
    }

    [Fact]
    public void ExtractChoices_ReturnsOptions_FromPropertiesChoices()
    {
        var json = JsonNode.Parse("""{"properties":{"choices":[{"displayText":"PropOption"}]}}""")!;
        var result = InvokeInstance<ObservableCollection<EditableOption>>("ExtractChoices", json);
        Assert.Single(result);
        Assert.Equal("PropOption", result[0].Text);
    }

    [Fact]
    public void ExtractChoices_ReturnsOptions_FromQuestionInfo_CapitalChoices()
    {
        var questionInfoJson = """{"Choices":[{"displayText":"InfoOpt"}]}""";
        var escapedJson = System.Text.Json.JsonSerializer.Serialize(questionInfoJson);
        var json = JsonNode.Parse($"{{\"questionInfo\": {escapedJson}}}")!;
        var result = InvokeInstance<ObservableCollection<EditableOption>>("ExtractChoices", json);
        Assert.Single(result);
        Assert.Equal("InfoOpt", result[0].Text);
    }

    [Fact]
    public void ExtractChoices_ReturnsOptions_FromQuestionInfo_LowercaseChoices()
    {
        var questionInfoJson = """{"choices":[{"displayText":"LowOpt"}]}""";
        var escapedJson = System.Text.Json.JsonSerializer.Serialize(questionInfoJson);
        var json = JsonNode.Parse($"{{\"questionInfo\": {escapedJson}}}")!;
        var result = InvokeInstance<ObservableCollection<EditableOption>>("ExtractChoices", json);
        Assert.Single(result);
        Assert.Equal("LowOpt", result[0].Text);
    }

    [Fact]
    public void ExtractChoices_HandlesHtmlInOptionText()
    {
        var json = JsonNode.Parse("""{"choices":[{"displayText":"<b>Bold</b>"}]}""")!;
        var result = InvokeInstance<ObservableCollection<EditableOption>>("ExtractChoices", json);
        Assert.Single(result);
        Assert.Equal("Bold", result[0].Text);
    }

    // ── FindAllArrays — JsonObject branch (line 374-377, previously 0 hits) ──
    // Embed a JsonObject inside the Google Forms array structure to trigger the
    // else-if (node is JsonObject) branch in FindAllArrays.

    [Fact]
    public void ParseGoogleFormsData_FindAllArrays_TraversesJsonObjects()
    {
        // Wrapping the question block inside a JsonObject forces FindAllArrays
        // to recurse through the object branch (lines 374-377).
        // The outer structure still looks like a Google form array.
        var json = JsonNode.Parse("""
        [
            null,
            ["desc"],
            null,
            "Title",
            null,
            [
                [1, "Wrapped Q", null, 0, [[2, []]]]
            ],
            { "extra": [[3, "Nested in object", null, 0, [[4, []]]]] }
        ]
        """)!;
        // Should not throw and should still find the top-level question
        var result = InvokeInstance<List<ParsedQuestion>>("ParseGoogleFormsData", json);
        Assert.NotNull(result);
        Assert.True(result.Count >= 1);
        Assert.Equal("Wrapped Q", result[0].Title);
    }

    [Fact]
    public void FindAllArrays_DirectlyWithJsonObject_TraversesProperties()
    {
        // Directly call FindAllArrays with a JsonObject root to hit the
        // else-if (node is JsonObject obj) branch (lines 374-377).
        var found = new List<System.Text.Json.Nodes.JsonArray>();
        var method = typeof(FormLinkParserService)
            .GetMethod("FindAllArrays", BindingFlags.NonPublic | BindingFlags.Instance)!;

        var obj = JsonNode.Parse("""{"items":[[1,"Q",null,0]]}""")!;
        method.Invoke(_sut, new object?[] { obj, found });

        Assert.True(found.Count > 0);
    }

    [Fact]
    public void FindAllArrays_WithNull_DoesNotThrow()
    {
        var found = new List<System.Text.Json.Nodes.JsonArray>();
        var method = typeof(FormLinkParserService)
            .GetMethod("FindAllArrays", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var ex = Record.Exception(() => method.Invoke(_sut, new object?[] { null, found }));
        Assert.Null(ex);
        Assert.Empty(found);
    }

    // ── ParseModernMsJson ─────────────────────────────────────────────────────

    [Fact]
    public void ParseModernMsJson_ReturnsEmpty_WhenNoFormNode()
    {
        var result = InvokeInstance<List<ParsedQuestion>>("ParseModernMsJson", JsonNode.Parse("""{"something":"else"}""")!);
        Assert.Empty(result);
    }

    [Fact]
    public void ParseModernMsJson_ParsesQuestionsFromFormKey()
    {
        var json = JsonNode.Parse("""
        {"form":{"title":"Test Form","questions":[
            {"id":"q1","title":"What is your name?","type":"Question.Text","required":true,"order":1}
        ]}}
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
        var json = JsonNode.Parse("""{"form":{"title":"My Survey","questions":[]}}""")!;
        InvokeInstance<List<ParsedQuestion>>("ParseModernMsJson", json);
        Assert.Equal("My Survey", _sut.FormTitle);
    }

    [Fact]
    public void ParseModernMsJson_SetsFormTitle_FromNameFallback()
    {
        var json = JsonNode.Parse("""{"form":{"name":"Named Survey","questions":[]}}""")!;
        InvokeInstance<List<ParsedQuestion>>("ParseModernMsJson", json);
        Assert.Equal("Named Survey", _sut.FormTitle);
    }

    [Fact]
    public void ParseModernMsJson_SetsFormDescription()
    {
        var json = JsonNode.Parse("""{"form":{"title":"T","description":"My Desc","questions":[]}}""")!;
        InvokeInstance<List<ParsedQuestion>>("ParseModernMsJson", json);
        Assert.Equal("My Desc", _sut.FormDescription);
    }

    [Fact]
    public void ParseModernMsJson_SetsFormDescription_FromSubtitleFallback()
    {
        var json = JsonNode.Parse("""{"form":{"title":"T","subtitle":"Sub Desc","questions":[]}}""")!;
        InvokeInstance<List<ParsedQuestion>>("ParseModernMsJson", json);
        Assert.Equal("Sub Desc", _sut.FormDescription);
    }

    [Fact]
    public void ParseModernMsJson_ParsesFromDataFormKey()
    {
        var json = JsonNode.Parse("""
        {"data":{"form":{"title":"Nested","questions":[
            {"id":"q1","title":"Q1","type":"Question.Text","order":1}
        ]}}}
        """)!;
        var result = InvokeInstance<List<ParsedQuestion>>("ParseModernMsJson", json);
        Assert.Single(result);
        Assert.Equal("Q1", result[0].Title);
    }

    [Fact]
    public void ParseModernMsJson_ParsesFromRootQuestionsKey()
    {
        var json = JsonNode.Parse("""{"questions":[{"id":"q1","title":"Flat question","type":"Question.Text","order":1}]}""")!;
        var result = InvokeInstance<List<ParsedQuestion>>("ParseModernMsJson", json);
        Assert.Single(result);
        Assert.Equal("Flat question", result[0].Title);
    }

    [Fact]
    public void ParseModernMsJson_SkipsColumnGroupQuestions()
    {
        var json = JsonNode.Parse("""
        {"form":{"questions":[
            {"id":"s1","type":"Question.ColumnGroup","title":"Section 1","order":1},
            {"id":"q1","title":"Real question","type":"Question.Text","order":2}
        ]}}
        """)!;
        var result = InvokeInstance<List<ParsedQuestion>>("ParseModernMsJson", json);
        Assert.Single(result);
        Assert.Equal("Real question", result[0].Title);
    }

    [Fact]
    public void ParseModernMsJson_SetsSection_FromColumnGroup()
    {
        var json = JsonNode.Parse("""
        {"form":{"questions":[
            {"id":"s1","type":"Question.ColumnGroup","title":"Section A","order":1},
            {"id":"q1","title":"Q in section","type":"Question.Text","order":2}
        ]}}
        """)!;
        var result = InvokeInstance<List<ParsedQuestion>>("ParseModernMsJson", json);
        Assert.Equal("Section A", result[0].Section);
    }

    [Fact]
    public void ParseModernMsJson_SetsSection_DefaultGeneral_WhenNoColumnGroup()
    {
        var json = JsonNode.Parse("""{"form":{"questions":[{"id":"q1","title":"Q","type":"Question.Text","order":1}]}}""")!;
        var result = InvokeInstance<List<ParsedQuestion>>("ParseModernMsJson", json);
        Assert.Equal("General", result[0].Section);
    }

    [Fact]
    public void ParseModernMsJson_SkipsMatrixChoiceGroup()
    {
        var json = JsonNode.Parse("""
        {"form":{"questions":[
            {"id":"mg1","type":"Question.MatrixChoiceGroup","title":"Matrix Group","order":1},
            {"id":"q1","title":"Real Q","type":"Question.Text","order":2}
        ]}}
        """)!;
        var result = InvokeInstance<List<ParsedQuestion>>("ParseModernMsJson", json);
        Assert.Single(result);
        Assert.Equal("Real Q", result[0].Title);
    }

    // ── MatrixChoice block (lines 481-491, previously 0 hits) ─────────────────
    // Need both a MatrixChoiceGroup (to populate matrixGroups dict) AND a
    // MatrixChoice question that references it via groupId.

    [Fact]
    public void ParseModernMsJson_SetsMatrixFields_WhenMatrixChoiceMatchesGroup()
    {
        var json = JsonNode.Parse("""
        {"form":{"questions":[
            {
                "id":"mg1",
                "type":"Question.MatrixChoiceGroup",
                "title":"Matrix Group",
                "order":1,
                "choices":[{"displayText":"ColA"},{"displayText":"ColB"}]
            },
            {
                "id":"mq1",
                "title":"Row Question",
                "type":"Question.MatrixChoice",
                "groupId":"mg1",
                "order":2
            }
        ]}}
        """)!;
        var result = InvokeInstance<List<ParsedQuestion>>("ParseModernMsJson", json);
        Assert.Single(result);
        Assert.True(result[0].IsMatrix);
        Assert.Equal("Matrix Group", result[0].MatrixGroupTitle);
        Assert.Equal("[Matrix Group] Row Question", result[0].FullTitle);
        Assert.Equal(2, result[0].Options.Count);
        Assert.Contains(result[0].Options, o => o.Text == "ColA");
    }

    [Fact]
    public void ParseModernMsJson_MatrixChoice_NoMatchingGroup_NotIsMatrix()
    {
        // MatrixChoice with a groupId that doesn't match any MatrixChoiceGroup
        // hits the if(matrixGroups.TryGetValue) false branch
        var json = JsonNode.Parse("""
        {"form":{"questions":[
            {
                "id":"mq1",
                "title":"Row Q",
                "type":"Question.MatrixChoice",
                "groupId":"nonexistent",
                "order":1
            }
        ]}}
        """)!;
        var result = InvokeInstance<List<ParsedQuestion>>("ParseModernMsJson", json);
        Assert.Single(result);
        Assert.False(result[0].IsMatrix);
        Assert.Null(result[0].MatrixGroupTitle);
    }

    [Fact]
    public void ParseModernMsJson_SetsSubtitle_WhenPresent()
    {
        var json = JsonNode.Parse("""
        {"form":{"questions":[
            {"id":"q1","title":"Q1","type":"Question.Text","order":1,"subtitle":"A hint"}
        ]}}
        """)!;
        var result = InvokeInstance<List<ParsedQuestion>>("ParseModernMsJson", json);
        Assert.Equal("A hint", result[0].Subtitle);
    }

    [Fact]
    public void ParseModernMsJson_SetsSubtitle_FromDescriptionFallback()
    {
        var json = JsonNode.Parse("""
        {"form":{"questions":[
            {"id":"q1","title":"Q1","type":"Question.Text","order":1,"description":"A desc"}
        ]}}
        """)!;
        var result = InvokeInstance<List<ParsedQuestion>>("ParseModernMsJson", json);
        Assert.Equal("A desc", result[0].Subtitle);
    }

    [Fact]
    public void ParseModernMsJson_SkipsQuestion_WhenTitleEmptyAndNoOptions()
    {
        var json = JsonNode.Parse("""{"form":{"questions":[{"id":"q1","title":"","type":"Question.Text","order":1}]}}""")!;
        var result = InvokeInstance<List<ParsedQuestion>>("ParseModernMsJson", json);
        Assert.Empty(result);
    }

    [Fact]
    public void ParseModernMsJson_IncludesQuestion_WhenTitleEmptyButHasOptions()
    {
        var json = JsonNode.Parse("""
        {"form":{"questions":[
            {"id":"q1","title":"","type":"Question.Choice","order":1,"choices":[{"displayText":"Opt"}]}
        ]}}
        """)!;
        var result = InvokeInstance<List<ParsedQuestion>>("ParseModernMsJson", json);
        Assert.Single(result);
    }

    [Fact]
    public void ParseModernMsJson_SetsRequiredFalse_WhenNotSet()
    {
        var json = JsonNode.Parse("""{"form":{"questions":[{"id":"q1","title":"Q","type":"Question.Text","order":1}]}}""")!;
        var result = InvokeInstance<List<ParsedQuestion>>("ParseModernMsJson", json);
        Assert.False(result[0].Required);
    }

    [Fact]
    public void ParseModernMsJson_IndexesQuestionsStartingAt1()
    {
        var json = JsonNode.Parse("""
        {"form":{"questions":[
            {"id":"q1","title":"Q1","type":"Question.Text","order":1},
            {"id":"q2","title":"Q2","type":"Question.Text","order":2}
        ]}}
        """)!;
        var result = InvokeInstance<List<ParsedQuestion>>("ParseModernMsJson", json);
        Assert.Equal(1, result[0].Index);
        Assert.Equal(2, result[1].Index);
    }

    [Fact]
    public void ParseModernMsJson_OrdersQuestionsByOrder()
    {
        var json = JsonNode.Parse("""
        {"form":{"questions":[
            {"id":"q2","title":"Second","type":"Question.Text","order":2},
            {"id":"q1","title":"First","type":"Question.Text","order":1}
        ]}}
        """)!;
        var result = InvokeInstance<List<ParsedQuestion>>("ParseModernMsJson", json);
        Assert.Equal("First", result[0].Title);
        Assert.Equal("Second", result[1].Title);
    }

    [Fact]
    public void ParseModernMsJson_ParsesChoices()
    {
        var json = JsonNode.Parse("""
        {"form":{"questions":[
            {"id":"q1","title":"Pick one","type":"Question.Choice","order":1,
             "choices":[{"displayText":"Option A"},{"displayText":"Option B"}]}
        ]}}
        """)!;
        var result = InvokeInstance<List<ParsedQuestion>>("ParseModernMsJson", json);
        Assert.Single(result);
        Assert.Equal(2, result[0].Options.Count);
        Assert.Contains(result[0].Options, o => o.Text == "Option A");
        Assert.Contains(result[0].Options, o => o.Text == "Option B");
    }

    [Fact]
    public void ParseModernMsJson_ParsesDescriptiveQuestions()
    {
        var json = JsonNode.Parse("""
        {"form":{
            "questions":[],
            "descriptiveQuestions":[
                {"id":"d1","title":"Descriptive Q","type":"Question.Text","order":1}
            ]
        }}
        """)!;
        var result = InvokeInstance<List<ParsedQuestion>>("ParseModernMsJson", json);
        Assert.Single(result);
        Assert.Equal("Descriptive Q", result[0].Title);
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
        [ null, [""], null, "Form", null,
          [ [1, "Section Header", null, 8, []], [2, "Real Question", null, 0, [[3, []]]] ]
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
        [ null, [""], null, "Form", null,
          [ [1, "Q1", null, 0, [[10, []]]], [2, "Q2", null, 0, [[11, []]]] ]
        ]
        """)!;
        var result = InvokeInstance<List<ParsedQuestion>>("ParseGoogleFormsData", json);
        Assert.Equal(1, result[0].Index);
        Assert.Equal(2, result[1].Index);
    }

    [Fact]
    public void ParseGoogleFormsData_ParsesParagraphType()
    {
        var json = JsonNode.Parse("""[ null, [""], null, "Form", null, [ [1, "Describe yourself", null, 1, [[2, []]]] ] ]""")!;
        var result = InvokeInstance<List<ParsedQuestion>>("ParseGoogleFormsData", json);
        Assert.Equal("Paragraph", result[0].Type);
    }

    [Fact]
    public void ParseGoogleFormsData_ParsesDropdownType()
    {
        var json = JsonNode.Parse("""[ null, [""], null, "Form", null, [ [1, "Choose one", null, 4, [[2, [["A"], ["B"]]]]] ] ]""")!;
        var result = InvokeInstance<List<ParsedQuestion>>("ParseGoogleFormsData", json);
        Assert.Equal("Dropdown", result[0].Type);
    }

    [Fact]
    public void ParseGoogleFormsData_ParsesDateType()
    {
        var json = JsonNode.Parse("""[ null, [""], null, "Form", null, [ [1, "When?", null, 9, [[2, []]]] ] ]""")!;
        var result = InvokeInstance<List<ParsedQuestion>>("ParseGoogleFormsData", json);
        Assert.Equal("Date", result[0].Type);
    }

    [Fact]
    public void ParseGoogleFormsData_ParsesTimeType()
    {
        var json = JsonNode.Parse("""[ null, [""], null, "Form", null, [ [1, "What time?", null, 10, [[2, []]]] ] ]""")!;
        var result = InvokeInstance<List<ParsedQuestion>>("ParseGoogleFormsData", json);
        Assert.Equal("Time", result[0].Type);
    }

    [Fact]
    public void ParseGoogleFormsData_ParsesCheckboxesType()
    {
        var json = JsonNode.Parse("""[ null, [""], null, "Form", null, [ [1, "Pick all", null, 3, [[2, [["X"],["Y"]]]]] ] ]""")!;
        var result = InvokeInstance<List<ParsedQuestion>>("ParseGoogleFormsData", json);
        Assert.Equal("Checkboxes", result[0].Type);
    }

    [Fact]
    public void ParseGoogleFormsData_HandlesHtmlInTitle()
    {
        var json = JsonNode.Parse("""[ null, [""], null, "Form", null, [ [1, "<b>Bold Question</b>", null, 0, [[2, []]]] ] ]""")!;
        var result = InvokeInstance<List<ParsedQuestion>>("ParseGoogleFormsData", json);
        Assert.Equal("Bold Question", result[0].Title);
    }

    // ── ParseGoogleFormsData filter branches (lines 304-309, previously 0 hits) ──
    // Line 304: q[1] is not a string → continue
    // Line 308: q[3] is not a number → continue

    [Fact]
    public void ParseGoogleFormsData_SkipsArray_WhenIndex1IsNotString()
    {
        // Array where index 0 is a number (valid) but index 1 is also a number (not a string)
        // This hits the line 304-305 branch that filters out numeric config arrays like [0,0,0,2,...]
        var json = JsonNode.Parse("""
        [ null, [""], null, "Form", null,
          [ [1, 999, null, 0, [[2, []]]], [2, "Real Q", null, 0, [[3, []]]] ]
        ]
        """)!;
        var result = InvokeInstance<List<ParsedQuestion>>("ParseGoogleFormsData", json);
        // Only "Real Q" should be parsed; the numeric-title array is skipped
        Assert.Single(result);
        Assert.Equal("Real Q", result[0].Title);
    }

    [Fact]
    public void ParseGoogleFormsData_SkipsArray_WhenIndex3IsNotNumber()
    {
        // Array where index 0 is a number, index 1 is a string, but index 3 is a string (not a number)
        // This hits the line 308-309 branch
        var json = JsonNode.Parse("""
        [ null, [""], null, "Form", null,
          [ [1, "Bad Q", null, "not_a_type", [[2, []]]], [3, "Good Q", null, 0, [[4, []]]] ]
        ]
        """)!;
        var result = InvokeInstance<List<ParsedQuestion>>("ParseGoogleFormsData", json);
        Assert.Single(result);
        Assert.Equal("Good Q", result[0].Title);
    }
}
