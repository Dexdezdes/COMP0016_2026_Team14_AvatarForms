using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace AvatarFormsApp.Services;

public class ParsedQuestion
{
    public int Index { get; set; }
    public string Id { get; set; } = string.Empty;
    public string Section { get; set; } = "General";
    public string Title { get; set; } = string.Empty;
    public string? FullTitle { get; set; }
    public string Type { get; set; } = string.Empty;
    public bool Required { get; set; }
    public double? OrderValue { get; set; }
    public List<string> Options { get; set; } = new();
    public bool IsMatrix { get; set; }
    public string? MatrixGroupTitle { get; set; }
    public string? Subtitle { get; set; }
}

public class FormLinkParserService
{
    private static void Log(string msg) =>
        System.Diagnostics.Debug.WriteLine($"[FormLinkParser] {msg}");

    public async Task<List<ParsedQuestion>> ParseAsync(string url)
    {
        Log($"ParseAsync START — url={url}");
        try
        {
            Log("Calling CaptureFormPayloadAsync...");
            var rawJson = await CaptureFormPayloadAsync(url);
            Log($"CaptureFormPayloadAsync returned — rawJson is {(rawJson is null ? "NULL" : "NOT NULL")}");

            if (rawJson is null)
            {
                Log("No payload captured, returning empty list.");
                return new();
            }

            Log("Calling ParseModernMsJson...");
            var result = ParseModernMsJson(rawJson);
            Log($"ParseModernMsJson returned {result.Count} questions.");
            return result;
        }
        catch (Exception ex)
        {
            Log($"ParseAsync EXCEPTION: {ex.GetType().FullName}: {ex.Message}");
            Log($"StackTrace: {ex.StackTrace}");
            if (ex.InnerException is not null)
                Log($"InnerException: {ex.InnerException.GetType().FullName}: {ex.InnerException.Message}");
            return new();
        }
    }

    // ── Playwright ────────────────────────────────────────────────────────────

    private async Task<JsonNode?> CaptureFormPayloadAsync(string url)
    {
        JsonNode? captured = null;

        Log("Creating Playwright...");
        using var playwright = await Playwright.CreateAsync();
        Log("Launching Chromium...");
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        Log("Browser launched. Creating context...");
        var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();
        Log("Page created. Attaching response handler...");

        page.Response += async (_, response) =>
        {
            if (captured is not null) return;
            if (response.Request.ResourceType is not ("fetch" or "xhr")) return;
            if (response.Status != 200) return;
            try
            {
                var text = await response.TextAsync();
                if (!text.Contains("\"questions\":[")) return;
                Log($"Found form payload in response from: {response.Url}");
                captured = JsonNode.Parse(text);
                Log("Payload parsed into JsonNode OK.");
            }
            catch (Exception ex)
            {
                Log($"Response handler EXCEPTION: {ex.GetType().Name}: {ex.Message}");
            }
        };

        Log($"Navigating to {url}...");
        await page.GotoAsync(url, new() { WaitUntil = WaitUntilState.NetworkIdle });
        Log("Navigation complete.");

        var startBtn = await page.QuerySelectorAsync("button:has-text('Start'), button:has-text('Start now')");
        if (startBtn is not null)
        {
            Log("Found Start button, clicking...");
            await startBtn.EvaluateAsync("el => el.click()");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await page.WaitForTimeoutAsync(3000);
            Log("Post-start wait complete.");
        }
        else
        {
            Log("No Start button found.");
        }

        Log("Closing browser...");
        await browser.CloseAsync();
        Log($"Browser closed. captured is {(captured is null ? "NULL" : "NOT NULL")}");
        return captured;
    }

    // ── Parsing ───────────────────────────────────────────────────────────────

    private List<ParsedQuestion> ParseModernMsJson(JsonNode raw)
    {
        try
        {
            Log("ParseModernMsJson: reading form node...");
            var formNode = raw["form"];
            if (formNode is null) { Log("formNode is NULL, returning empty."); return new(); }

            Log($"formNode type: {formNode.GetType().Name}");

            var questions = (formNode["questions"] as JsonArray) ?? new JsonArray();
            var descriptive = (formNode["descriptiveQuestions"] as JsonArray) ?? new JsonArray();
            Log($"questions count={questions.Count}, descriptive count={descriptive.Count}");

            Log("Sorting elements...");
            var allElements = questions.Concat(descriptive)
                .Where(n => n is not null).Cast<JsonNode>()
                .OrderBy(n => SafeDouble(n["order"]) ?? 0)
                .ToList();
            Log($"allElements count={allElements.Count}");

            Log("Building matrix groups...");
            var matrixGroups = new Dictionary<string, (string Title, List<string> Columns)>();
            foreach (var q in questions.Where(n => n is not null).Cast<JsonNode>())
            {
                if (SafeString(q["type"]) == "Question.MatrixChoiceGroup")
                {
                    var gid = SafeString(q["id"]) ?? string.Empty;
                    Log($"  Matrix group: id={gid}");
                    matrixGroups[gid] = (CleanHtml(SafeString(q["title"])), ExtractChoices(q));
                }
            }
            Log($"Matrix groups count={matrixGroups.Count}");

            var result = new List<ParsedQuestion>();
            var currentSection = "General";

            for (int idx = 0; idx < allElements.Count; idx++)
            {
                var q = allElements[idx];
                try
                {
                    var qType = SafeString(q["type"]) ?? string.Empty;
                    var qId = SafeString(q["id"]) ?? "(no id)";
                    Log($"  Processing element [{idx}] id={qId} type={qType}");

                    if (qType == "Question.ColumnGroup")
                    {
                        currentSection = CleanHtml(SafeString(q["title"]));
                        Log($"  Section changed to: {currentSection}");
                        continue;
                    }
                    if (qType == "Question.MatrixChoiceGroup") { Log("  Skipping MatrixChoiceGroup"); continue; }

                    Log($"  Extracting choices for [{idx}]...");
                    var choices = ExtractChoices(q);
                    Log($"  Choices count={choices.Count}");

                    var item = new ParsedQuestion
                    {
                        Id = qId,
                        Section = currentSection,
                        Title = CleanHtml(SafeString(q["title"])),
                        Type = qType,
                        Required = SafeBool(q["required"]) ?? false,
                        OrderValue = SafeDouble(q["order"]),
                        Options = choices,
                    };

                    if (qType == "Question.MatrixChoice")
                    {
                        var gid = SafeString(q["groupId"]) ?? string.Empty;
                        if (matrixGroups.TryGetValue(gid, out var group))
                        {
                            item.IsMatrix = true;
                            item.MatrixGroupTitle = group.Title;
                            item.Options = group.Columns;
                            item.FullTitle = $"[{group.Title}] {item.Title}";
                        }
                    }

                    var subtitle = CleanHtml(SafeString(q["subtitle"]) ?? SafeString(q["description"]));
                    if (!string.IsNullOrEmpty(subtitle)) item.Subtitle = subtitle;

                    if (!string.IsNullOrEmpty(item.Title) || item.Options.Count > 0)
                    {
                        result.Add(item);
                        Log($"  Added question: \"{item.Title}\"");
                    }
                }
                catch (Exception ex)
                {
                    Log($"  Element [{idx}] EXCEPTION: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                }
            }

            for (int i = 0; i < result.Count; i++) result[i].Index = i + 1;
            Log($"ParseModernMsJson DONE — {result.Count} questions.");
            return result;
        }
        catch (Exception ex)
        {
            Log($"ParseModernMsJson EXCEPTION: {ex.GetType().FullName}: {ex.Message}");
            Log($"StackTrace: {ex.StackTrace}");
            return new();
        }
    }

    private List<string> ExtractChoices(JsonNode q)
    {
        try
        {
            JsonArray? choicesArr = null;

            if ((q["choices"] as JsonArray) is { Count: > 0 } a1) choicesArr = a1;
            else if ((q["properties"]?["choices"] as JsonArray) is { Count: > 0 } a2) choicesArr = a2;
            else if (SafeString(q["questionInfo"]) is string qi)
            {
                try
                {
                    var info = JsonNode.Parse(qi);
                    choicesArr = (info?["Choices"] ?? info?["choices"]) as JsonArray;
                }
                catch (Exception ex) { Log($"  questionInfo parse EXCEPTION: {ex.Message}"); }
            }

            if (choicesArr is null) return new();

            var options = new List<string>();
            foreach (var c in choicesArr.Where(n => n is not null).Cast<JsonNode>())
            {
                try
                {
                    var val = SafeString(c["Description"])
                           ?? SafeString(c["displayText"])
                           ?? SafeString(c["displayValue"])
                           ?? SafeString(c["FormsProDisplayRTText"])
                           ?? SafeString(c["value"]);

                    if (!string.IsNullOrEmpty(val))
                        options.Add(CleanHtml(val));
                }
                catch (Exception ex) { Log($"  Choice item EXCEPTION: {ex.Message}"); }
            }
            return options;
        }
        catch (Exception ex)
        {
            Log($"ExtractChoices EXCEPTION: {ex.GetType().Name}: {ex.Message}");
            return new();
        }
    }

    // ── Safe helpers ──────────────────────────────────────────────────────────

    private static string? SafeString(JsonNode? node)
    {
        if (node is not JsonValue jv) return null;
        try
        {
            var el = jv.GetValue<JsonElement>();
            return el.ValueKind == JsonValueKind.String ? el.GetString() : el.ToString();
        }
        catch { return null; }
    }

    private static double? SafeDouble(JsonNode? node)
    {
        if (node is not JsonValue jv) return null;
        try
        {
            var el = jv.GetValue<JsonElement>();
            if (el.TryGetDouble(out var d)) return d;
            if (el.TryGetInt64(out var l)) return (double)l;
            if (el.TryGetInt32(out var i)) return (double)i;
        }
        catch { }
        return null;
    }

    private static bool? SafeBool(JsonNode? node)
    {
        if (node is not JsonValue jv) return null;
        try
        {
            var el = jv.GetValue<JsonElement>();
            if (el.ValueKind == JsonValueKind.True) return true;
            if (el.ValueKind == JsonValueKind.False) return false;
        }
        catch { }
        return null;
    }

    private static string CleanHtml(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return Regex.Replace(text, "<[^>]+>", string.Empty)
                    .Replace("&nbsp;", " ").Trim();
    }
}
