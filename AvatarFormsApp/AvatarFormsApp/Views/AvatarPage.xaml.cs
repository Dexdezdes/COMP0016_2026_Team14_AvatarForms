using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Media.SpeechRecognition;
using WinUIEx.Messaging;

namespace AvatarFormsApp.Views;

public sealed partial class AvatarPage : Page
{
    private Process? _pythonProcess;
    private Process? _llamafileProcess;
    private string? _cachedPythonPath;
    private SimpleWebServer? _webServer;
    private SpeechRecognizer? _speechRecognizer;
    private bool _isAvatarInitialized;
    private bool _isMicEnabled;
    private bool _isTalkerActive;
    private bool _autoSendEnabled = true;
    private bool _isAudioConnected = false;

    public AvatarPage()
    {
        InitializeComponent();
        AutoSendToggle.IsOn = true;
        InitializeAvatar();
    }

    private async void InitializeAvatar()
    {
        if (_isAvatarInitialized) return;

        try
        {
            LogToConsole("[INIT] Starting HeadTTS avatar setup...");

            var options = new Microsoft.Web.WebView2.Core.CoreWebView2EnvironmentOptions
            {
                AdditionalBrowserArguments = "--ignore-gpu-blocklist --enable-gpu-rasterization"
            };
            var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateWithOptionsAsync(null, null, options);
            await AvatarWebView.EnsureCoreWebView2Async(env);

            if (AvatarWebView.CoreWebView2 == null)
            {
                LogToConsole("[ERROR] CoreWebView2 failed to initialize.");
                return;
            }

            _isAvatarInitialized = true;
            LogToConsole("[INIT] CoreWebView2 ready");

            // Open Developer Tools
            try
            {
                AvatarWebView.CoreWebView2.OpenDevToolsWindow();
                LogToConsole("[INIT] DevTools opened");
            }
            catch (Exception ex)
            {
                LogToConsole($"[INIT] DevTools failed: {ex.Message}");
            }

            AvatarWebView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = true;
            AvatarWebView.CoreWebView2.Settings.IsWebMessageEnabled = true;

            string baseDir = AppContext.BaseDirectory;
            string contentPath = Path.Combine(baseDir, "HeadTTS");

            LogToConsole($"[SERVER] Content path: {contentPath}");

            if (!Directory.Exists(contentPath))
            {
                LogToConsole($"[ERROR] HeadTTS folder NOT FOUND at: {contentPath}");
                return;
            }

            // Start local web server
            if (_webServer == null)
            {
                var prefix = "http://127.0.0.1:5501/";
                _webServer = new SimpleWebServer(contentPath, prefix);
                _webServer.Start();
                LogToConsole($"[SERVER] Started at {prefix}");
            }

            AvatarWebView.NavigationStarting += (s, e) =>
            {
                DispatcherQueue.TryEnqueue(() => LogToConsole($"[NAV] Starting: {e.Uri}"));
            };

            AvatarWebView.NavigationCompleted += (s, e) =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    LogToConsole($"[NAV] Completed: Success={e.IsSuccess}");
                    if (!e.IsSuccess)
                    {
                        LogToConsole($"[NAV] Error: {e.WebErrorStatus}");
                    }
                });
            };

            // Navigate to HeadTTS index.html
            LogToConsole("[NAV] Navigating to HeadTTS index.html...");
            AvatarWebView.CoreWebView2.Navigate("http://127.0.0.1:5501/index.html");

            // Show audio connection overlay after avatar loads
            await Task.Delay(2000); // Wait for page to load
            DispatcherQueue.TryEnqueue(() =>
            {
                if (AudioConnectionOverlay != null)
                {
                    AudioConnectionOverlay.Visibility = Visibility.Visible;
                    LogToConsole("[AVATAR] Audio connection overlay shown");
                }
            });

            // Start AI process after avatar is fully initialized
            StartAIProcess();
        }
        catch (Exception ex)
        {
            _isAvatarInitialized = false;
            LogToConsole($"[ERROR] Avatar setup: {ex.Message}");
        }
    }

    private void LogToConsole(string message)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            var rtb = ChatDisplay as RichTextBlock;
            if (rtb != null)
            {
                var paragraph = new Microsoft.UI.Xaml.Documents.Paragraph();
                foreach (var run in ParseAnsiRuns(message + "\n"))
                {
                    paragraph.Inlines.Add(run);
                }
                rtb.Blocks.Add(paragraph);
                if (rtb.Blocks.Count > 500)
                    rtb.Blocks.RemoveAt(0);
            }
            ChatScroll.ChangeView(null, ChatScroll.ScrollableHeight, null);
        });
    }

    // Minimal ANSI color parser for foreground codes 30-37, 90-97
    private IEnumerable<Microsoft.UI.Xaml.Documents.Run> ParseAnsiRuns(string text)
    {
        var regex = new Regex("\u001B\\[([0-9;]+)m");
        int lastIndex = 0;
        var matches = regex.Matches(text);
        var currentBrush = new SolidColorBrush(Microsoft.UI.Colors.White);

        foreach (Match match in matches)
        {
            if (match.Index > lastIndex)
            {
                yield return new Microsoft.UI.Xaml.Documents.Run
                {
                    Text = text.Substring(lastIndex, match.Index - lastIndex),
                    Foreground = currentBrush
                };
            }
            string code = match.Groups[1].Value;
            currentBrush = AnsiCodeToBrush(code) ?? new SolidColorBrush(Microsoft.UI.Colors.White);
            lastIndex = match.Index + match.Length;
        }
        if (lastIndex < text.Length)
        {
            yield return new Microsoft.UI.Xaml.Documents.Run
            {
                Text = text.Substring(lastIndex),
                Foreground = currentBrush
            };
        }
    }

    private SolidColorBrush? AnsiCodeToBrush(string code)
    {
        switch (code)
        {
            case "30": return new SolidColorBrush(Microsoft.UI.Colors.Black);
            case "31": return new SolidColorBrush(Microsoft.UI.Colors.DarkRed);
            case "32": return new SolidColorBrush(Microsoft.UI.Colors.DarkGreen);
            case "33": return new SolidColorBrush(Microsoft.UI.Colors.Goldenrod);
            case "34": return new SolidColorBrush(Microsoft.UI.Colors.DarkBlue);
            case "35": return new SolidColorBrush(Microsoft.UI.Colors.DarkMagenta);
            case "36": return new SolidColorBrush(Microsoft.UI.Colors.DarkCyan);
            case "37": return new SolidColorBrush(Microsoft.UI.Colors.LightGray);
            case "90": return new SolidColorBrush(Microsoft.UI.Colors.Gray);
            case "91": return new SolidColorBrush(Microsoft.UI.Colors.Red);
            case "92": return new SolidColorBrush(Microsoft.UI.Colors.Green);
            case "93": return new SolidColorBrush(Microsoft.UI.Colors.Yellow);
            case "94": return new SolidColorBrush(Microsoft.UI.Colors.Blue);
            case "95": return new SolidColorBrush(Microsoft.UI.Colors.Magenta);
            case "96": return new SolidColorBrush(Microsoft.UI.Colors.Cyan);
            case "97": return new SolidColorBrush(Microsoft.UI.Colors.White);
            case "0":  return new SolidColorBrush(Microsoft.UI.Colors.White);
        }
        return null;
    }

    private void OnConsoleToggleChanged(object sender, RoutedEventArgs e)
    {
        if (ConsoleOverlay == null || ConsoleToggle == null) return;
        var isOpen = ConsoleToggle.IsChecked == true;
        ConsoleOverlay.Visibility = isOpen ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnSettingsToggleChanged(object sender, RoutedEventArgs e)
    {
        if (SettingsOverlay == null || SettingsToggle == null) return;
        var isOpen = SettingsToggle.IsChecked == true;
        SettingsOverlay.Visibility = isOpen ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnCloseSettingsClicked(object sender, RoutedEventArgs e)
    {
        if (SettingsToggle != null)
        {
            SettingsToggle.IsChecked = false;
        }
        if (SettingsOverlay != null)
        {
            SettingsOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private void OnAutoSendToggled(object sender, RoutedEventArgs e)
    {
        _autoSendEnabled = AutoSendToggle?.IsOn ?? false;
        LogToConsole($"[SETTINGS] Auto-send: {(_autoSendEnabled ? "ON" : "OFF")}");
    }

    private string GetPythonPath()
    {
        if (!string.IsNullOrEmpty(_cachedPythonPath)) return _cachedPythonPath;

        string baseDir = AppContext.BaseDirectory;
        for (int i = 0; i <= 9; i++)
        {
            string probePath = Path.GetFullPath(Path.Combine(baseDir, new string('.', i * 3).Replace("...", "../"), "env/Scripts/python.exe"));
            if (File.Exists(probePath))
            {
                LogToConsole($"[SUCCESS] Python found at: {probePath}");
                return _cachedPythonPath = probePath;
            }
        }

        string buildVenvPath = Path.Combine(baseDir, "env", "Scripts", "python.exe");
        if (File.Exists(buildVenvPath)) return _cachedPythonPath = buildVenvPath;

        string sourceVenvPath = Path.GetFullPath(Path.Combine(baseDir, "..\\..\\..\\..\\env\\Scripts\\python.exe"));
        if (File.Exists(sourceVenvPath)) return _cachedPythonPath = sourceVenvPath;

        LogToConsole("[WARNING] Virtual env not found. Falling back to system 'python'");
        return _cachedPythonPath = "python";
    }

    private async Task<bool> IsPortAvailable(int port, int maxRetries = 30, int delayMs = 1000)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                using var client = new System.Net.Sockets.TcpClient();
                await client.ConnectAsync("127.0.0.1", port);
                LogToConsole($"[PORT CHECK] Port {port} is ready");
                return true;
            }
            catch
            {
                if (i == 0)
                {
                    LogToConsole($"[PORT CHECK] Waiting for port {port}...");
                }
                await Task.Delay(delayMs);
            }
        }
        LogToConsole($"[PORT CHECK] Timeout waiting for port {port}");
        return false;
    }

    private void StartLlamafileServer()
    {
        try
        {
            string backendPath = Path.Combine(AppContext.BaseDirectory, "Backend");
            if (!Directory.Exists(backendPath))
            {
                LogToConsole("[ERROR] Backend folder not found");
                return;
            }

            var llamafilePath = Directory.GetFiles(backendPath, "*.llamafile").FirstOrDefault();
            if (string.IsNullOrEmpty(llamafilePath))
            {
                LogToConsole("[ERROR] No .llamafile found in Backend folder");
                return;
            }

            LogToConsole($"[LLAMAFILE] Found: {llamafilePath}");

            var start = new ProcessStartInfo
            {
                FileName = llamafilePath,
                Arguments = "--server --host 127.0.0.1 --port 8081 --ctx-size 4096 -ngl 9999 --nobrowser",
                WorkingDirectory = Path.GetDirectoryName(llamafilePath),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            _llamafileProcess = new Process { StartInfo = start, EnableRaisingEvents = true };
            _llamafileProcess.Start();
            _llamafileProcess.BeginOutputReadLine();
            _llamafileProcess.BeginErrorReadLine();

            LogToConsole("[LLAMAFILE] Server process started on port 8081");
        }
        catch (Exception ex)
        {
            LogToConsole($"[LLAMAFILE ERROR] Failed to start: {ex.Message}");
        }
    }

    private async void StartAIProcess()
    {
        try
        {
            // Start llamafile server first
            StartLlamafileServer();

            // Wait for llamafile to be ready
            bool llamafileReady = await IsPortAvailable(8081);
            if (!llamafileReady)
            {
                LogToConsole("[ERROR] Llamafile server did not start in time");
                return;
            }

            string baseDir = AppContext.BaseDirectory;
            string scriptPath = Path.Combine(AppContext.BaseDirectory, "Backend", "main.py");

            var start = new ProcessStartInfo
            {
                FileName = GetPythonPath(),
                Arguments = $"-u \"{scriptPath}\"",
                WorkingDirectory = Path.GetDirectoryName(scriptPath),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            string pythonExe = GetPythonPath();
            if (pythonExe.Contains("env"))
            {
                string? venvRoot = Path.GetDirectoryName(Path.GetDirectoryName(pythonExe));
                if (venvRoot != null)
                {
                    string sitePackages = Path.Combine(venvRoot, "Lib", "site-packages");
                    start.EnvironmentVariables["PYTHONPATH"] = sitePackages;
                }
            }

            _pythonProcess = new Process { StartInfo = start, EnableRaisingEvents = true };

            _pythonProcess.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        LogToConsole(e.Data);
                        if (e.Data.Trim().StartsWith("Talker:", StringComparison.OrdinalIgnoreCase))
                        {
                            _isTalkerActive = true;
                        }
                        else if (e.Data.Trim().StartsWith("Critic:", StringComparison.OrdinalIgnoreCase))
                        {
                            _isTalkerActive = false;
                        }

                        ChatScroll.ChangeView(null, ChatScroll.ScrollableHeight, null);
                    });
                }
            };

            _pythonProcess.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    string cleanedError = Regex.Replace(e.Data, @"\x1B\[[^@-~]*[@-~]", string.Empty);
                    LogToConsole($"[PYTHON ERROR]: {cleanedError}");
                }
            };

            _pythonProcess.Start();
            _pythonProcess.BeginOutputReadLine();
            _pythonProcess.BeginErrorReadLine();

            LogToConsole("[AI] Python process started");
        }
        catch (Exception ex)
        {
            LogToConsole($"[RUNTIME ERROR]: {ex.Message}");
        }
    }

    private void SendMessage()
    {
        if (_pythonProcess == null || _pythonProcess.HasExited) return;

        var message = UserInput.Text;
        if (!string.IsNullOrWhiteSpace(message))
        {
            _pythonProcess.StandardInput.WriteLine(message);
            _pythonProcess.StandardInput.Flush();
            LogToConsole($"You: {message}");
            UserInput.Text = "";
        }
    }

    private void OnSendClicked(object sender, RoutedEventArgs e) => SendMessage();

    private void OnInputKeyDown(object sender, KeyRoutedEventArgs e)
    {
        // No auto-send on Enter - only send via button
    }

    private void OnInputTextChanged(object sender, TextChangedEventArgs e)
    {
        // No auto-send behavior
    }

    private async void OnMicClicked(object sender, RoutedEventArgs e)
    {
            _isMicEnabled = !_isMicEnabled;

            if (_isMicEnabled)
            {
                MicIcon.Glyph = "\uE720";
                MicIcon.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red);
                await StartVoiceInput();
            }
            else
            {
                MicIcon.Glyph = "\uF12E";
                MicIcon.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black);
                await StopVoiceInput();
            }
        }

    private async Task StartVoiceInput()
    {
        try
        {
            if (_speechRecognizer != null)
            {
                try
                {
                    if (_speechRecognizer.State != SpeechRecognizerState.Idle)
                    {
                        await _speechRecognizer.ContinuousRecognitionSession.StopAsync();
                    }
                }
                catch { }

                _speechRecognizer.Dispose();
                _speechRecognizer = null;
            }

            _speechRecognizer = new SpeechRecognizer();
            _speechRecognizer.ContinuousRecognitionSession.AutoStopSilenceTimeout = TimeSpan.MaxValue;

            var dictationConstraint = new SpeechRecognitionTopicConstraint(SpeechRecognitionScenario.Dictation, "dictation");
            _speechRecognizer.Constraints.Add(dictationConstraint);

            var compilationResult = await _speechRecognizer.CompileConstraintsAsync();
            if (compilationResult.Status != SpeechRecognitionResultStatus.Success)
            {
                throw new Exception($"Constraint compilation failed: {compilationResult.Status}");
            }

            _speechRecognizer.HypothesisGenerated += (s, args) =>
            {
                DispatcherQueue.TryEnqueue(() => UserInput.Text = args.Hypothesis.Text);
            };

            _speechRecognizer.ContinuousRecognitionSession.ResultGenerated += (s, args) =>
            {
                if (args.Result.Status == SpeechRecognitionResultStatus.Success)
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        UserInput.Text = args.Result.Text;
                        
                        // Auto-send if enabled
                        if (_autoSendEnabled)
                        {
                            SendMessage();
                        }
                    });
                }
            };

            await _speechRecognizer.ContinuousRecognitionSession.StartAsync();
        }
        catch (Exception ex)
        {
        _isMicEnabled = false;
            MicIcon.Glyph = "\uF12E";
            MicIcon.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black);

            _speechRecognizer?.Dispose();
            _speechRecognizer = null;

            string hexError = string.Format("0x{0:X8}", (uint)ex.HResult);
            LogToConsole($"[MIC ERROR {hexError}]: {ex.Message}");

            if ((uint)ex.HResult == 0x8004503A || (uint)ex.HResult == 0x80131509 || ex.Message.Contains("privacy"))
            {
                LogToConsole("-> Opening Windows Speech Settings. Please turn ON 'Online Speech Recognition'.");
                var uri = new Uri("ms-settings:privacy-speech");
                _ = Windows.System.Launcher.LaunchUriAsync(uri);
            }
        }
    }

    private async Task StopVoiceInput()
    {
        if (_speechRecognizer?.ContinuousRecognitionSession != null)
        {
            try
            {
                await _speechRecognizer.ContinuousRecognitionSession.StopAsync();
            }
            catch { }
        }
    }

    private void OnConnectAudioClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            // Hide the overlay
            if (AudioConnectionOverlay != null)
            {
                AudioConnectionOverlay.Visibility = Visibility.Collapsed;
            }

            _isAudioConnected = true;
            LogToConsole("[AVATAR] Audio connected - TTS enabled");

            // Execute JavaScript to enable audio in HeadTTS
            if (AvatarWebView?.CoreWebView2 != null)
            {
                AvatarWebView.CoreWebView2.ExecuteScriptAsync(@"
                    if (typeof enableAudio === 'function') {
                        enableAudio();
                    } else {
                        console.log('Audio enabled by user interaction');
                    }
                ");
            }
        }
        catch (Exception ex)
        {
            LogToConsole($"[ERROR] Failed to connect audio: {ex.Message}");
        }
    }

    private async void OnBackClicked(object sender, RoutedEventArgs e)
    {
        if (_pythonProcess != null && !_pythonProcess.HasExited)
        {
            try { _pythonProcess.Kill(); } catch { }
        }

        if (_llamafileProcess != null && !_llamafileProcess.HasExited)
        {
            try { _llamafileProcess.Kill(); } catch { }
        }

        _isMicEnabled = false;
        MicIcon.Glyph = "\uE720";
        MicIcon.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black);

        await StopVoiceInput();

        var rtb = ChatDisplay as RichTextBlock;
        if (rtb != null)
        {
            rtb.Blocks.Clear();
        }
        UserInput.Text = "";

        if (Frame.CanGoBack)
        {
            Frame.GoBack();
        }
    }
}
