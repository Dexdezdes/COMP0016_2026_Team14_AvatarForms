using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

namespace AvatarFormsApp.Services;
/*
Microsoft forms for testing:
Mental disease form
https://forms.cloud.microsoft/Pages/ResponsePage.aspx?id=_oivH5ipW0yTySEKEdmlwsk8NGIVcC1GluibkejnM-dURFVYSUtQTjRaUVRXMk9BQ05ZTFVMUlU4VC4u
University form (have start and next button)
https://forms.cloud.microsoft/Pages/ResponsePage.aspx?id=_oivH5ipW0yTySEKEdmlwsk8NGIVcC1GluibkejnM-dURU1GSFdDWkFENlVBWTA2U0dLQUZVOEwxQi4u
Market research form (have start and next button)
https://forms.cloud.microsoft/Pages/ResponsePage.aspx?id=_oivH5ipW0yTySEKEdmlwsk8NGIVcC1GluibkejnM-dUNlBBTU5FN01aWkpRVkdERUlBNVIxOVBFNS4u
Google forms for testing:
Start of the year
https://docs.google.com/forms/d/e/1FAIpQLSfKRWBhXmhPt5jVY7JO1Iqi7UWqgHbpiuXRcF8u7kSMWWi6Hg/viewform?usp=dialog
Business form
https://docs.google.com/forms/d/e/1FAIpQLSc0BYHF_Xb0FtHe_r7DPNY5K-Ne_UJ1Vd-IcN4PNjAAPuX-xg/viewform?usp=publish-editor
*/
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
        
    public string FormTitle { get; private set; } = string.Empty;
    public string FormDescription { get; private set; } = string.Empty;

    // ── Public entry point ────────────────────────────────────────────────────
    // webView must already be in the visual tree (initialized by the page).
    // Call this only from the UI thread.

    public async Task<List<ParsedQuestion>> ParseAsync(string url, WebView2 webView)
    {
        Log($"ParseAsync START — url={url}");
        try
        {
            await webView.EnsureCoreWebView2Async();
            Log("CoreWebView2 ready.");

            // Route to the correct platform parser
            if (url.Contains("docs.google.com/forms"))
            {
                Log("Detected Google Forms URL.");
                return await ParseGoogleFormAsync(url, webView);
            }
            else
            {
                Log("Detected Microsoft Forms URL.");
                var rawJson = await CaptureMsFormPayloadAsync(url, webView);
                Log($"Capture done — rawJson is {(rawJson is null ? "NULL" : "NOT NULL")}");
                if (rawJson is null) { Log("No payload captured."); return new(); }
                var result = ParseModernMsJson(rawJson);
                Log($"Parsed {result.Count} questions.");
                return result;
            }
        }
        catch (Exception ex)
        {
            Log($"ParseAsync EXCEPTION: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            return new();
        }
    }

    // ── WebView2 interception ─────────────────────────────────────────────────

    private async Task<JsonNode?> CaptureMsFormPayloadAsync(string url, WebView2 webView)
    {
        var tcs = new TaskCompletionSource<JsonNode?>();
        var core = webView.CoreWebView2;

        // Mirror the Playwright approach: only accept the specific API endpoint that
        // returns the structured form JSON with a top-level "form" key.
        // ResponsePageStartup.ashx has a different structure and must be skipped.
        async void OnResponseReceived(object? sender, CoreWebView2WebResourceResponseReceivedEventArgs e)
        {
            if (tcs.Task.IsCompleted) return;

            var uri = e.Request.Uri;

            // Accept the known MS Forms API endpoints.
            // ResponsePageStartup.ashx is also included — WebView2 (unlike Playwright's
            // fresh headless browser) reuses Edge's cache/session, so after clicking Start
            // the questions are already in memory and no new runtimeFormsWithResponses
            // request fires. The data comes from the initial ResponsePageStartup.ashx instead.
            bool isFormApi = uri.Contains("runtimeFormsWithResponses") ||
                             uri.Contains("light/forms") ||
                             uri.Contains("formapi/api") ||
                             uri.Contains("ResponsePageStartup.ashx");

            if (!isFormApi) return;

            Log($"Checking API response from: {uri}");

            try
            {
                var irasStream = await e.Response.GetContentAsync();
                using var reader = new System.IO.StreamReader(irasStream.AsStreamForRead());
                var text = await reader.ReadToEndAsync();

                if (!text.Contains("\"questions\":["))
                {
                    Log("Response missing questions array, skipping.");
                    return;
                }

                Log($"Found form payload ({text.Length} chars) from: {uri}");
                var node = JsonNode.Parse(text);
                Log("JsonNode parsed OK.");

                // ── Full JSON dump ──────────────────────────────────────────
                Log("=== RAW JSON START ===");
                Log(node!.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                Log("=== RAW JSON END ===");

                tcs.TrySetResult(node);
            }
            catch (Exception ex)
            {
                Log($"Response handler EXCEPTION: {ex.GetType().Name}: {ex.Message}");
                // Don't fail the TCS — keep waiting for another response
            }
        }

        core.WebResourceResponseReceived += OnResponseReceived;

        // After navigation completes, inject JS to click Start / Start now button.
        // Works for both forms that show questions immediately AND forms behind a Start button.
        async void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (!e.IsSuccess || tcs.Task.IsCompleted) return;
            try
            {
                Log("Navigation completed — injecting Start button click script...");
                var result = await core.ExecuteScriptAsync(@"
                    (function() {
                        var buttons = document.querySelectorAll('button');
                        for (var i = 0; i < buttons.length; i++) {
                            var text = buttons[i].innerText.trim().toLowerCase();
                            if (text === 'start' || text === 'start now') {
                                buttons[i].click();
                                return 'clicked';
                            }
                        }
                        return 'not_found';
                    })()");
                Log($"Start button script result: {result}");
            }
            catch (Exception ex) { Log($"Start button script EXCEPTION: {ex.Message}"); }
        }

        core.NavigationCompleted += OnNavigationCompleted;

        try
        {
            Log($"Navigating WebView2 to {url}...");
            core.Navigate(url);

            // 40s timeout — forms with Start button need extra time:
            // page load + button click + second network request for questions
            var timeout = Task.Delay(TimeSpan.FromSeconds(40));
            var winner = await Task.WhenAny(tcs.Task, timeout);

            if (winner == timeout)
            {
                Log("Timed out waiting for form payload.");
                return null;
            }

            return await tcs.Task;
        }
        finally
        {
            // Always unsubscribe both handlers to avoid leaks
            core.WebResourceResponseReceived -= OnResponseReceived;
            core.NavigationCompleted -= OnNavigationCompleted;
            Log("Handlers unsubscribed.");
        }
    }


    // ── Google Forms parser ───────────────────────────────────────────────────
    // Google Forms embeds all data in the HTML as a JS variable: FB_PUBLIC_LOAD_DATA_
    // We navigate, wait for the page to load, then extract it via ExecuteScriptAsync.

    private async Task<List<ParsedQuestion>> ParseGoogleFormAsync(string url, WebView2 webView)
    {
        var core = webView.CoreWebView2;
        var navTcs = new TaskCompletionSource<bool>();

        void OnNavCompleted(object? s, CoreWebView2NavigationCompletedEventArgs e)
        {
            navTcs.TrySetResult(e.IsSuccess);
        }

        core.NavigationCompleted += OnNavCompleted;
        try
        {
            core.Navigate(url);
            var timeout = Task.Delay(TimeSpan.FromSeconds(20));
            var winner = await Task.WhenAny(navTcs.Task, timeout);
            if (winner == timeout) { Log("Google Forms navigation timed out."); return new(); }
            if (!await navTcs.Task) { Log("Google Forms navigation failed."); return new(); }
        }
        finally { core.NavigationCompleted -= OnNavCompleted; }

        Log("Google Forms page loaded. Extracting FB_PUBLIC_LOAD_DATA_...");

        // Extract the raw JS array as a JSON string
        var json = await core.ExecuteScriptAsync(
            "JSON.stringify(window.FB_PUBLIC_LOAD_DATA_)");

        if (string.IsNullOrEmpty(json) || json == "null")
        {
            Log("FB_PUBLIC_LOAD_DATA_ not found on page.");
            return new();
        }

        // Strip surrounding quotes added by ExecuteScriptAsync JSON encoding
        // The result is a JSON string containing an escaped JSON array
        Log($"Raw data length: {json.Length}");
        Log("=== GOOGLE RAW JSON START ===");
        Log(json);
        Log("=== GOOGLE RAW JSON END ===");

        JsonNode? root;
        try
        {
            // ExecuteScriptAsync wraps the result in a JSON string — parse twice
            var unescaped = System.Text.Json.JsonSerializer.Deserialize<string>(json);

            Log("=== GOOGLE UNESCAPED JSON START ===");
            Log(unescaped ?? "(null)");
            Log("=== GOOGLE UNESCAPED JSON END ===");

            root = JsonNode.Parse(unescaped!);
        }
        catch (Exception ex)
        {
            Log($"FB_PUBLIC_LOAD_DATA_ parse failed: {ex.Message}");
            return new();
        }

        return ParseGoogleFormsData(root!);
    }

    private List<ParsedQuestion> ParseGoogleFormsData(JsonNode root)
    {
        var result = new List<ParsedQuestion>();
        try
        {
            // 1. Grab the Title (usually at root[3])
            FormTitle = CleanHtml(SafeString(root[3])) ?? "Untitled Form";
            FormDescription = CleanHtml(SafeString(root[1]?[0])) ?? string.Empty;
            Log($"Google form title: \"{FormTitle}\"");
            Log($"Google form description: \"{FormDescription}\"");

            // 2. Find ALL question blocks regardless of nesting
            var allArrays = new List<JsonArray>();
            FindAllArrays(root, allArrays);

            Log($"Inspecting {allArrays.Count} potential data blocks...");

            foreach (var q in allArrays)
            {
                // VALIDATION: Standard Google questions have [ID, Title, null, Type, [Inputs]]
                // We check for Count >= 4 and that index 0 is a number (the ID)
                if (q.Count < 4 || q[0] is not JsonValue jvId || jvId.GetValueKind() != JsonValueKind.Number)
                    continue;

                // Index 1 MUST be a string (the question title) — filters out numeric config arrays like [0,0,0,2,...]
                if (q[1] is not JsonValue jvTitle || jvTitle.GetValueKind() != JsonValueKind.String)
                    continue;

                // Index 3 MUST be the Type (Number)
                if (q[3] is not JsonValue jvType || jvType.GetValueKind() != JsonValueKind.Number)
                    continue;

                try
                {
                    var title = CleanHtml(SafeString(q[1])) ?? string.Empty;
                    var typeNum = (int)(SafeDouble(q[3]) ?? -1);

                    // Skip section headers (8) or unknown types (-1) for this list
                    if (typeNum == 8 || typeNum == -1) continue;

                    var qType = MapGoogleType(typeNum);
                    var options = new List<string>();
                    string entryId = string.Empty;

                    // Index 4 contains the input mapping
                    if (q.Count > 4 && q[4] is JsonArray inputGroup && inputGroup.Count > 0)
                    {
                        if (inputGroup[0] is JsonArray firstInput)
                        {
                            entryId = SafeString(firstInput[0]) ?? string.Empty;

                            // Check for MCQ/Dropdown options
                            if (firstInput.Count > 1 && firstInput[1] is JsonArray choices)
                            {
                                foreach (var c in choices)
                                {
                                    if (c is JsonArray cArr && cArr.Count > 0)
                                    {
                                        var txt = SafeString(cArr[0]);
                                        if (!string.IsNullOrEmpty(txt)) options.Add(CleanHtml(txt));
                                    }
                                }
                            }
                        }
                    }

                    result.Add(new ParsedQuestion
                    {
                        Id = entryId,
                        Title = title,
                        Type = qType,
                        Options = options,
                        Index = result.Count + 1
                    });
                }
                catch { /* Not a question block, skip silently */ }
            }

            Log($"Google: successfully extracted {result.Count} questions.");
        }
        catch (Exception ex)
        {
            Log($"ParseGoogleFormsData CRITICAL ERROR: {ex.Message}");
        }
        return result;
    }

    // Helper to crawl the JSON tree and find all arrays
    private void FindAllArrays(JsonNode? node, List<JsonArray> found)
    {
        if (node is JsonArray arr)
        {
            found.Add(arr);
            foreach (var item in arr) FindAllArrays(item, found);
        }
        else if (node is JsonObject obj)
        {
            foreach (var prop in obj) FindAllArrays(prop.Value, found);
        }
    }



    private static string MapGoogleType(int typeNum) => typeNum switch
    {
        0 => "ShortText",
        1 => "Paragraph",
        2 => "MultipleChoice",
        3 => "Checkboxes",
        4 => "Dropdown",
        5 => "LinearScale",
        7 => "Grid",
        9 => "Date",
        10 => "Time",
        _ => "ShortText",
    };

    // ── JSON parsing (unchanged) ──────────────────────────────────────────────

    private List<ParsedQuestion> ParseModernMsJson(JsonNode raw)
    {
        try
        {
            // Try multiple known root structures:
            // 1. {"form": {"questions": [...]}}  — runtimeFormsWithResponses
            // 2. {"questions": [...]}             — ResponsePageStartup.ashx (flat)
            // 3. {"data": {"form": {...}}}        — some variant responses
            JsonNode? formNode = raw["form"]
                              ?? raw["data"]?["form"]
                              ?? (raw["questions"] is not null ? raw : null);

            if (formNode is null)
            {
                Log($"formNode is NULL — tried keys: form, data.form, root questions. Keys present: {string.Join(", ", (raw as System.Text.Json.Nodes.JsonObject)?.Select(k => k.Key) ?? Enumerable.Empty<string>())}");
                return new();
            }
            Log($"formNode found via key: {(raw["form"] is not null ? "form" : raw["data"]?["form"] is not null ? "data.form" : "root")}");

            // ── Grab form title ─────────────────────────────────────────────
            // MS Forms stores the title in several possible locations
            var formTitle = SafeString(formNode["title"])
                         ?? SafeString(formNode["name"])
                         ?? SafeString(raw["form"]?["title"])
                         ?? SafeString(raw["title"])
                         ?? string.Empty;
            Log($"Form title: { formTitle}");
            FormTitle = CleanHtml(formTitle);

            var formDesc = SafeString(formNode["description"])
                        ?? SafeString(formNode["subtitle"])
                        ?? string.Empty;
            FormDescription = CleanHtml(formDesc);
            Log($"Form description: \"{FormDescription}\"");

            var questions = (formNode["questions"] as JsonArray) ?? new JsonArray();
            var descriptive = (formNode["descriptiveQuestions"] as JsonArray) ?? new JsonArray();
            Log($"questions={questions.Count} descriptive={descriptive.Count}");

            var allElements = questions.Concat(descriptive)
                .Where(n => n is not null).Cast<JsonNode>()
                .OrderBy(n => SafeDouble(n["order"]) ?? 0)
                .ToList();

            var matrixGroups = new Dictionary<string, (string Title, List<string> Columns)>();
            foreach (var q in questions.Where(n => n is not null).Cast<JsonNode>())
            {
                if (SafeString(q["type"]) == "Question.MatrixChoiceGroup")
                {
                    var gid = SafeString(q["id"]) ?? string.Empty;
                    matrixGroups[gid] = (CleanHtml(SafeString(q["title"])), ExtractChoices(q));
                }
            }

            var result = new List<ParsedQuestion>();
            var currentSection = "General";

            for (int idx = 0; idx < allElements.Count; idx++)
            {
                var q = allElements[idx];
                try
                {
                    var qType = SafeString(q["type"]) ?? string.Empty;
                    var qId = SafeString(q["id"]) ?? "(no id)";

                    if (qType == "Question.ColumnGroup")
                    {
                        currentSection = CleanHtml(SafeString(q["title"]));
                        continue;
                    }
                    if (qType == "Question.MatrixChoiceGroup") continue;

                    var item = new ParsedQuestion
                    {
                        Id = qId,
                        Section = currentSection,
                        Title = CleanHtml(SafeString(q["title"])),
                        Type = qType,
                        Required = SafeBool(q["required"]) ?? false,
                        OrderValue = SafeDouble(q["order"]),
                        Options = ExtractChoices(q),
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
                        result.Add(item);
                }
                catch (Exception ex)
                {
                    Log($"Element [{idx}] EXCEPTION: {ex.GetType().Name}: {ex.Message}");
                }
            }

            for (int i = 0; i < result.Count; i++) result[i].Index = i + 1;
            return result;
        }
        catch (Exception ex)
        {
            Log($"ParseModernMsJson EXCEPTION: {ex.GetType().Name}: {ex.Message}");
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
                try { var info = JsonNode.Parse(qi); choicesArr = (info?["Choices"] ?? info?["choices"]) as JsonArray; }
                catch { }
            }

            if (choicesArr is null) return new();

            var options = new List<string>();
            foreach (var c in choicesArr.Where(n => n is not null).Cast<JsonNode>())
            {
                var val = SafeString(c["Description"])
                       ?? SafeString(c["displayText"])
                       ?? SafeString(c["displayValue"])
                       ?? SafeString(c["FormsProDisplayRTText"])
                       ?? SafeString(c["value"]);
                if (!string.IsNullOrEmpty(val)) options.Add(CleanHtml(val));
            }
            return options;
        }
        catch { return new(); }
    }

    // ── Safe JSON helpers ─────────────────────────────────────────────────────

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
