using System;
using System.IO;
using System.Text.RegularExpressions;
using AvatarFormsApp.Contracts.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Media.SpeechRecognition;
using Windows.Media.Playback;
using Windows.Media.Core;
using WinUIEx.Messaging;

namespace AvatarFormsApp.Views;

public sealed partial class AvatarPage : Page
{
    private readonly IPythonProcessService _pythonProcessService;
    private readonly ILlamafileProcessService _llamafileProcessService;
    private readonly IResponseAPIService _responseAPIService;
    private readonly ILocalSettingsService _localSettingsService;
    private SimpleWebServer? _webServer;
    private SpeechRecognizer? _speechRecognizer;
    private bool _isAvatarInitialized;
    private bool _isMicEnabled;
    private bool _isTalkerActive;
    private bool _autoSendEnabled = false;
    private string _selectedAvatar = "julia";

    private DispatcherTimer? _speechSilenceTimer;
    private string _finalizedSpeech = "";

    private readonly MediaPlayer _micOnPlayer;
    private readonly MediaPlayer _micOffPlayer;

    // Stored so they can be unsubscribed when leaving the page
    private readonly Action<string> _onPythonOutput;
    private readonly Action<string> _onPythonError;
    private readonly Action<string> _onLlamaOutput;

    public AvatarPage()
    {
        InitializeComponent();

        _pythonProcessService = App.GetService<IPythonProcessService>();
        _llamafileProcessService = App.GetService<ILlamafileProcessService>();
        _responseAPIService = App.GetService<IResponseAPIService>();
        _localSettingsService = App.GetService<ILocalSettingsService>();

        _micOnPlayer = new MediaPlayer();
        _micOnPlayer.Source = MediaSource.CreateFromUri(new Uri("C:\\Windows\\Media\\Speech On.wav"));

        _micOffPlayer = new MediaPlayer();
        _micOffPlayer.Source = MediaSource.CreateFromUri(new Uri("C:\\Windows\\Media\\Speech Off.wav"));

        // Store handlers so they can be unsubscribed when leaving the page
        _onPythonOutput = msg => DispatcherQueue.TryEnqueue(() => LogToConsole(msg));
        _onPythonError  = msg => DispatcherQueue.TryEnqueue(() => LogToConsole(msg));
        _onLlamaOutput  = msg => DispatcherQueue.TryEnqueue(() => LogToConsole(msg));

        _pythonProcessService.OutputReceived += _onPythonOutput;
        _pythonProcessService.ErrorReceived  += _onPythonError;
        _llamafileProcessService.OutputReceived += _onLlamaOutput;

        _speechSilenceTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _speechSilenceTimer.Tick += (s, e) =>
        {
            _speechSilenceTimer.Stop();
            if (_autoSendEnabled && !string.IsNullOrWhiteSpace(UserInput.Text))
            {
                SendMessage();
            }
        };

        AutoSendToggle.IsOn = true;
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        var saved = await _localSettingsService.ReadSettingAsync<string>("SelectedAvatar");
        if (saved is "julia" or "david")
        {
            _selectedAvatar = saved;
            if (_selectedAvatar == "david" && AvatarDavidRadio != null)
                AvatarDavidRadio.IsChecked = true;
        }

        InitializeAvatar();
    }

    private async void InitializeAvatar()
    {
        if (_isAvatarInitialized) return;

        try
        {
            LogToConsole("[INIT] Starting HeadTTS avatar setup...");

            var env = await App.GetOrCreateWebViewEnvironmentAsync();
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

            AvatarWebView.NavigationCompleted += async (s, e) =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    LogToConsole($"[NAV] Completed: Success={e.IsSuccess}");
                    if (!e.IsSuccess)
                        LogToConsole($"[NAV] Error: {e.WebErrorStatus}");
                });

                if (!e.IsSuccess) return;

                await Task.Delay(3000); // Wait for avatar and TTS engine to finish loading
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (AvatarWebView?.CoreWebView2 != null)
                    {
                        AvatarWebView.CoreWebView2.ExecuteScriptAsync(
                            "document.body.click(); if(window.head?.audioCtx) window.head.audioCtx.resume();"
                        );
                        LogToConsole("[AVATAR] Resumed audio context");
                    }
                });
            };

            // Navigate to HeadTTS index.html
            LogToConsole("[NAV] Navigating to HeadTTS index.html...");
            AvatarWebView.CoreWebView2.Navigate($"http://127.0.0.1:5501/index.html?avatar={_selectedAvatar}");
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

    private void OnAvatarSelectionChanged(object sender, RoutedEventArgs e)
    {
        if (AvatarJuliaRadio == null || AvatarDavidRadio == null) return;

        _selectedAvatar = AvatarDavidRadio.IsChecked == true ? "david" : "julia";
        _ = _localSettingsService.SaveSettingAsync("SelectedAvatar", _selectedAvatar);

        if (_isAvatarInitialized && AvatarWebView?.CoreWebView2 != null)
            AvatarWebView.CoreWebView2.Navigate($"http://127.0.0.1:5501/index.html?avatar={_selectedAvatar}");
    }

    private void SendMessage()
    {
        if (!_pythonProcessService.IsRunning) return;

        var message = UserInput.Text;
        if (!string.IsNullOrWhiteSpace(message))
        {
            _pythonProcessService.SendInput(message);
            LogToConsole($"You: {message}");
            UserInput.Text = "";
            _finalizedSpeech = "";

            if (_isMicEnabled)
            {
                _isMicEnabled = false;
                _speechSilenceTimer?.Stop();
                if (MicIcon != null)
                {
                    MicIcon.Glyph = "\uF12E";
                    MicIcon.Foreground = (SolidColorBrush)Application.Current.Resources["TextFillColorPrimaryBrush"];
                }
                try { _micOffPlayer.Play(); } catch { }
                _ = StopVoiceInput();
            }
        }
    }

    private void OnSendClicked(object sender, RoutedEventArgs e) => SendMessage();

    private void OnInputKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            SendMessage();
            e.Handled = true;
        }
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
                MicIcon.Foreground = (SolidColorBrush)Application.Current.Resources["SystemFillColorCriticalBrush"];
                try { _micOnPlayer.Play(); } catch { }
                await StartVoiceInput();
            }
            else
            {
                _speechSilenceTimer?.Stop();
                MicIcon.Glyph = "\uF12E";
                MicIcon.Foreground = (SolidColorBrush)Application.Current.Resources["TextFillColorPrimaryBrush"];
                try { _micOffPlayer.Play(); } catch { }
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
                DispatcherQueue.TryEnqueue(() => 
                {
                    _speechSilenceTimer?.Stop();
                    var currentText = args.Hypothesis.Text;
                    UserInput.Text = string.IsNullOrWhiteSpace(_finalizedSpeech) 
                        ? currentText 
                        : $"{_finalizedSpeech} {currentText}";

                    if (_autoSendEnabled) _speechSilenceTimer?.Start();
                });
            };

            _speechRecognizer.ContinuousRecognitionSession.ResultGenerated += (s, args) =>
            {
                if (args.Result.Status == SpeechRecognitionResultStatus.Success)
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        _speechSilenceTimer?.Stop();
                        var newText = args.Result.Text;
                        if (!string.IsNullOrWhiteSpace(newText))
                        {
                            _finalizedSpeech = string.IsNullOrWhiteSpace(_finalizedSpeech)
                                ? newText
                                : $"{_finalizedSpeech} {newText}";
                        }

                        UserInput.Text = _finalizedSpeech;

                        // Start timer for natural pause before auto-send
                        if (_autoSendEnabled)
                        {
                            _speechSilenceTimer?.Start();
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
            MicIcon.Foreground = (SolidColorBrush)Application.Current.Resources["TextFillColorPrimaryBrush"];

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

    private async Task StopAllServicesAsync()
    {
        // Unsubscribe console log handlers before stopping so trailing output
        // from this session doesn't re-register on the next visit
        _pythonProcessService.OutputReceived   -= _onPythonOutput;
        _pythonProcessService.ErrorReceived    -= _onPythonError;
        _llamafileProcessService.OutputReceived -= _onLlamaOutput;

        // Stop Python backend (also kills the Python-side HTTP servers on ports 8882 and 8883)
        _pythonProcessService.Stop();

        // Stop llamafile LLM server
        _llamafileProcessService.Stop();

        // Stop C# response API listener (port 5000)
        if (_responseAPIService.IsRunning)
        {
            try { await _responseAPIService.StopServerAsync(); }
            catch (Exception ex) { LogToConsole($"[CLEANUP] Response API stop error: {ex.Message}"); }
        }

        // Stop HeadTTS web server and unload WebView
        _webServer?.Stop();
        _webServer = null;

        if (AvatarWebView?.CoreWebView2 != null)
        {
            AvatarWebView.CoreWebView2.Navigate("about:blank");
        }

        _isAvatarInitialized = false;
    }

    private async void OnBackClicked(object sender, RoutedEventArgs e)
    {
        _isMicEnabled = false;
        _speechSilenceTimer?.Stop();
        MicIcon.Glyph = "\uE720";
        MicIcon.Foreground = (SolidColorBrush)Application.Current.Resources["TextFillColorPrimaryBrush"];

        await StopVoiceInput();
        await StopAllServicesAsync();

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
