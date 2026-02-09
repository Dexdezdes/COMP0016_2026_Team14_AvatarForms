using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.UI.Xaml.Input;
using AvatarFormsApp.ViewModels;
using System.Text.Json;

#if WINDOWS
using Windows.Media.SpeechRecognition;

#endif

#if __MACCATALYST__
using Speech;
using AVFoundation;
using Foundation;
#endif

namespace AvatarFormsApp.Views;

public sealed partial class ConversationPage : Page
{
    private Process? _pythonProcess;
    private string? _cachedPythonPath;

    private bool _isMicEnabled = false;
    private bool _isUserEditing = false;
    private bool _isAiSpeaking = false;
    private DispatcherTimer _silenceTimer;

    private bool _isTalkerActive = false;

    private System.Collections.Generic.Queue<string> _speechQueue = new();
    private bool _isProcessingQueue = false;
    
    private bool _isWebViewReady = false;
    private SimpleWebServer _webServer;
    private string _speechBuffer = "";

    private bool _isAvatarInitialized = false;
    private bool _wasMicOnBeforeSpeech = false;

#if WINDOWS
    private SpeechRecognizer _winRecognizer;
    private Windows.Media.SpeechSynthesis.SpeechSynthesizer _synth;
    private Windows.Media.Playback.MediaPlayer _speechPlayer;
    private System.Speech.Synthesis.SpeechSynthesizer _systemSynth;
    private TaskCompletionSource<bool> _speechCompletionTcs;
#endif

#if __MACCATALYST__
    private SFSpeechRecognizer _speechRecognizer = new SFSpeechRecognizer(new NSLocale("en-US"));
    private SFSpeechAudioBufferRecognitionRequest _recognitionRequest;
    private SFSpeechRecognitionTask _recognitionTask;
    private AVAudioEngine _audioEngine = new AVAudioEngine();
    private AVSpeechSynthesizer _nativeSynth = new AVSpeechSynthesizer();

    private bool _wasMicOnBeforeSpeaking = false;
    
    private class SpeechSyncDelegate : AVSpeechSynthesizerDelegate {
        private readonly Action<string> _onWord;
        private readonly Action _onStart;
        private readonly Action _onFinish;

        public SpeechSyncDelegate(Action onStart, Action<string> onWord, Action onFinish) {
            _onStart = onStart;
            _onWord = onWord;
            _onFinish = onFinish;
        }

        public override void DidStartSpeechUtterance(AVSpeechSynthesizer s, AVSpeechUtterance u) => _onStart?.Invoke();
        public override void DidFinishSpeechUtterance(AVSpeechSynthesizer s, AVSpeechUtterance u) => _onFinish?.Invoke();
        
        public override void WillSpeakRangeOfSpeechString(AVSpeechSynthesizer s, NSRange r, AVSpeechUtterance u) {
            var word = u.SpeechString.Substring((int)r.Location, (int)r.Length);
            _onWord?.Invoke(word);
        }
    }
#endif

    public ConversationPageViewModel ViewModel { get; }
    
    public ConversationPage()
    {
        try { 
            InitializeComponent(); 
            ViewModel = App.GetService<ConversationPageViewModel>();
            InitializeAvatar();
        }
        catch (Exception ex) { Console.WriteLine($"[XAML ERROR]: {ex}"); throw; }

        _silenceTimer = new DispatcherTimer();
        _silenceTimer.Interval = TimeSpan.FromSeconds(2.0);
        _silenceTimer.Tick += (s, e) =>
        {
            _silenceTimer.Stop();
            if (!string.IsNullOrWhiteSpace(UserInput.Text))
            {
                SendMessage();

                #if __MACCATALYST__
                if (_isMicEnabled) 
                {
                    StopMacVoiceInput();
                    
                    Task.Delay(100).ContinueWith(_ => {
                        DispatcherQueue.TryEnqueue(() => {
                            if (_isMicEnabled && !_wasMicOnBeforeSpeaking) 
                            {
                                StartMacVoiceInput();
                            }
                        });
                    });
                }
                #endif
                
                UserInput.Text = "";
            }
        };
    }

    private async void InitializeAvatar()
    {   
        if (_isAvatarInitialized) return; 
        try
        {
            ChatDisplay.Text += "[INIT] Starting avatar setup...\n";

#if WINDOWS
            try
            {
                // 1. Create the options
                var options = new Microsoft.Web.WebView2.Core.CoreWebView2EnvironmentOptions
                {
                    AdditionalBrowserArguments = "--ignore-gpu-blocklist --enable-gpu-rasterization"
                };

                // 2. Create the environment using the specific WinUI 3 static method
                // If 'CreateAsync' still fails with 3 args, try this exact line:
                var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateWithOptionsAsync(null, null, options);

                // 3. Initialize the WebView
                await AvatarWebView.EnsureCoreWebView2Async(env);
            }
            catch (Exception ex)
            {
                ChatDisplay.Text += $"[WEBVIEW SETUP ERROR]: {ex.Message}\n";
            }
#else
    await AvatarWebView.EnsureCoreWebView2Async();
#endif

            if (AvatarWebView.CoreWebView2 == null)
            {
                ChatDisplay.Text += "[ERROR] CoreWebView2 failed to initialize.\n";
                return;
            }

            _isAvatarInitialized = true;
            ChatDisplay.Text += "[INIT] CoreWebView2 ready\n";
            
            AvatarWebView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = true;
            AvatarWebView.CoreWebView2.Settings.IsWebMessageEnabled = true;
            string bridgeScript = @"
                (function() {
                    console.log('[Bridge] Initializing Mac->WebView2 bridge');
                    
                    var originalPostMessage = window.webkit?.messageHandlers?.bridge?.postMessage;
                    if (originalPostMessage) {
                        window.webkit.messageHandlers.bridge.postMessage = function(msg) {
                            console.log('[Bridge] Intercepted webkit message:', msg);
                            window.chrome.webview.postMessage(typeof msg === 'string' ? msg : JSON.stringify(msg));
                        };
                    }
                    
                    window.sendToHost = function(data) {
                        console.log('[Bridge] sendToHost called:', data);
                        if (window.chrome && window.chrome.webview) {
                            window.chrome.webview.postMessage(typeof data === 'string' ? data : JSON.stringify(data));
                        }
                    };
                    void(0);
                })();
            ";

#if __MACCATALYST__
            ChatDisplay.Text += "[INIT] Setting up Mac message handler...\n";
            
            
                AvatarWebView.NavigationCompleted += async (s, e) =>
                {
                    if (e.IsSuccess)
                    {
                        await AvatarWebView.ExecuteScriptAsync(bridgeScript);
                        
                        
                    }
                };  
#else
            await AvatarWebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(bridgeScript);
                ChatDisplay.Text += "[INIT] Bridge script injected\n";
            #endif
            
            AvatarWebView.CoreWebView2.WebMessageReceived += (s, ev) =>
            {
                try
                {
                    string rawMessage = null;
                    try
                    {
                        rawMessage = ev.TryGetWebMessageAsString();
                    }
                    catch
                    {
                        try
                        {
                            rawMessage = ev.WebMessageAsJson;
                        }
                        catch { }
                    }
                    
                    DispatcherQueue.TryEnqueue(() => {
                        ChatDisplay.Text += $"[JS->C#] RAW: {rawMessage}\n";
                        ChatScroll.ChangeView(null, ChatScroll.ScrollableHeight, null);
                    });

                    if (string.IsNullOrEmpty(rawMessage)) return;

                    using var doc = JsonDocument.Parse(rawMessage);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("type", out var typeElem))
                    {
                        var msgType = typeElem.GetString();
                        
                        DispatcherQueue.TryEnqueue(() => {
                            ChatDisplay.Text += $"[JS->C#] Type: {msgType}\n";
                        });

                        if (msgType == "avatarReady")
                        {
                            _isWebViewReady = true;
                            DispatcherQueue.TryEnqueue(() => {
                                ChatDisplay.Text += "[AVATAR] ✓✓✓ READY ✓✓✓\n";
                                ChatScroll.ChangeView(null, ChatScroll.ScrollableHeight, null);
                            });
                        }
                        else if (msgType == "speak_request")
                            {
                            string textToSay = root.GetProperty("text").GetString();
#if __MACCATALYST__
                            _wasMicOnBeforeSpeech = _isMicEnabled;
                            _isMicEnabled = false; 
                            StopMacVoiceInput(); 

                            _nativeSynth.Delegate = new SpeechSyncDelegate(
                                onStart: null, 
                                onWord: (word) => {
                                    DispatcherQueue.TryEnqueue(async () => {
                                        await AvatarWebView.ExecuteScriptAsync($"window.syncWord('{word.Replace("'", "\\'")}');");
                                    });
                                },
                                onFinish: () => {
                                    DispatcherQueue.TryEnqueue(async () => {
                                        await AvatarWebView.ExecuteScriptAsync("window.stopMouth();");
                                        
                                        _isMicEnabled = _wasMicOnBeforeSpeech;
                                        if (_isMicEnabled) 
                                        {
                                            await Task.Delay(200); 
                                            StartMacVoiceInput();
                                        }
                                    });
                                }
                            );

                            var utterance = new AVSpeechUtterance(textToSay);
                            var voice = Array.Find(AVSpeechSynthesisVoice.GetSpeechVoices(), v => v.Name == "Samantha");
                            if (voice != null) utterance.Voice = voice;
                            _nativeSynth.SpeakUtterance(utterance);
#elif WINDOWS
                            Task.Run(async () => {
                                try
                                {
                                    string safeText = textToSay.Replace("\"", "'").Replace("\r", " ").Replace("\n", " ");

                                    DispatcherQueue.TryEnqueue(() => {
                                        ChatDisplay.Text += $"[WINDOWS SPEAK_REQUEST] Starting: {safeText}\n";
                                    });

                                    _speechCompletionTcs = new TaskCompletionSource<bool>();
                                    var completionTcs = _speechCompletionTcs;

                                    DispatcherQueue.TryEnqueue(() => {
                                        try
                                        {
                                            // Initialize System.Speech synthesizer
                                            if (_systemSynth == null)
                                            {
                                                _systemSynth = new System.Speech.Synthesis.SpeechSynthesizer();
                                                _systemSynth.SetOutputToDefaultAudioDevice();
                                            }

                                            // Hook up the word boundary event
                                            void OnSpeakProgress(object sender, System.Speech.Synthesis.SpeakProgressEventArgs e)
                                            {
                                                if (e.Text != null)
                                                {
                                                    string word = e.Text;
                                                    DispatcherQueue.TryEnqueue(async () => {
                                                        await AvatarWebView.ExecuteScriptAsync($"window.syncWord('{word.Replace("'", "\\'")}');");
                                                    });
                                                }
                                            }

                                            void OnSpeakCompleted(object sender, System.Speech.Synthesis.SpeakCompletedEventArgs e)
                                            {
                                                DispatcherQueue.TryEnqueue(async () => {
                                                    ChatDisplay.Text += $"[WINDOWS SPEAK_REQUEST] Speech completed\n";
                                                    await AvatarWebView.ExecuteScriptAsync("window.stopMouth();");
                                                });

                                                _systemSynth.SpeakProgress -= OnSpeakProgress;
                                                _systemSynth.SpeakCompleted -= OnSpeakCompleted;
                                                _isAiSpeaking = false;
                                                completionTcs.TrySetResult(true);
                                            }

                                            _systemSynth.SpeakProgress += OnSpeakProgress;
                                            _systemSynth.SpeakCompleted += OnSpeakCompleted;

                                            // Speak asynchronously with word timing events
                                            _systemSynth.SpeakAsync(safeText);

                                            ChatDisplay.Text += $"[WINDOWS SPEAK_REQUEST] Speaking with real word timing...\n";
                                        }
                                        catch (Exception ex)
                                        {
                                            ChatDisplay.Text += $"[ERROR] System.Speech setup failed: {ex.Message}\n";
                                            completionTcs.TrySetException(ex);
                                        }
                                    });

                                    // Wait for speech to complete
                                    await completionTcs.Task;

                                    DispatcherQueue.TryEnqueue(() => {
                                        ChatDisplay.Text += $"[WINDOWS SPEAK_REQUEST] Finished\n";
                                    });
                                }
                                catch (Exception ex)
                                {
                                    DispatcherQueue.TryEnqueue(() => {
                                        ChatDisplay.Text += $"[WINDOWS SPEAK_REQUEST ERROR]: {ex.Message}\n";
                                        ChatDisplay.Text += $"Stack: {ex.StackTrace}\n";
                                    });
                                }
                            });
#endif
                        }
                    }
                }
                catch (Exception ex)
                {
                    DispatcherQueue.TryEnqueue(() => {
                        ChatDisplay.Text += $"[ERROR] WebMessage parse: {ex.Message}\n";
                        ChatScroll.ChangeView(null, ChatScroll.ScrollableHeight, null);
                    });
                }
            };


            try { 
                AvatarWebView.CoreWebView2.OpenDevToolsWindow(); 
                ChatDisplay.Text += "[INIT] DevTools opened\n";
            } catch (Exception ex) {
                ChatDisplay.Text += $"[INIT] DevTools failed: {ex.Message}\n";
            }

            string baseDir = AppContext.BaseDirectory;
            string contentPath = Path.Combine(baseDir, "TalkingHead");
    #if __MACCATALYST__
            contentPath = Path.GetFullPath(Path.Combine(baseDir, "..", "Resources", "TalkingHead"));
    #endif

            ChatDisplay.Text += $"[SERVER] Content path: {contentPath}\n";
            
            if (!Directory.Exists(contentPath))
            {
                ChatDisplay.Text += $"[ERROR] TalkingHead folder NOT FOUND!\n";
                return;
            }

            if (_webServer == null)
            {
                var prefix = "http://127.0.0.1:5500/";
                _webServer = new SimpleWebServer(contentPath, prefix);
                _webServer.Start();
                ChatDisplay.Text += $"[SERVER] Started at {prefix}\n";
            }

            AvatarWebView.NavigationStarting += (s, e) =>
            {
                DispatcherQueue.TryEnqueue(() => {
                    ChatDisplay.Text += $"[NAV] Starting: {e.Uri}\n";
                });
            };

            AvatarWebView.NavigationCompleted += (s, e) =>
            {
                DispatcherQueue.TryEnqueue(() => {
                    ChatDisplay.Text += $"[NAV] Completed: Success={e.IsSuccess}\n";
                    if (!e.IsSuccess)
                    {
                        ChatDisplay.Text += $"[NAV] Error: {e.WebErrorStatus}\n";
                    }
                    else
                    {
                        _ = AvatarWebView.CoreWebView2.ExecuteScriptAsync(@"
                            console.log('[Test] Script execution working');
                            if (window.sendToHost) {
                                window.sendToHost({type: 'test', message: 'JavaScript bridge test'});
                            } else {
                                console.error('[Test] window.sendToHost not defined!');
                            }
                            void(0);
                        ");
                    }
                });
            };

            ChatDisplay.Text += "[NAV] Navigating to minimal.html...\n";
            AvatarWebView.CoreWebView2.Navigate("http://127.0.0.1:5500/minimal.html");
        }
        catch (Exception ex)
        {
            _isAvatarInitialized = false;
            ChatDisplay.Text += $"[ERROR] Avatar setup: {ex.Message}\n";
            ChatDisplay.Text += $"[ERROR] Stack: {ex.StackTrace}\n";
        }
    }

    private void OnAutoSendToggled(object sender, RoutedEventArgs e)
    {

    }

    private void OnConsoleToggleChanged(object sender, RoutedEventArgs e)
    {
        if (ConsoleOverlay == null || ConsoleToggle == null) return;
        var isOpen = ConsoleToggle.IsChecked == true;
        ConsoleOverlay.Visibility = isOpen ? Visibility.Visible : Visibility.Collapsed;
    }

    private string GetPythonPath()
    {
        if (!string.IsNullOrEmpty(_cachedPythonPath)) return _cachedPythonPath;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
#if DEV_PYTHON_ENV
            if (File.Exists(DEV_PYTHON_ENV)) 
            {
                return _cachedPythonPath = DEV_PYTHON_ENV;
            }
#endif
            string baseDir = AppContext.BaseDirectory;
            for (int i = 0; i <= 9; i++)
            {
                string probePath = Path.GetFullPath(Path.Combine(baseDir, new string('.', i * 3).Replace("...", "../"), "env/Scripts/python.exe"));
                if (File.Exists(probePath))
                {
                    ChatDisplay.Text += $"[SUCCESS] Python found at: {probePath}\n";
                    return _cachedPythonPath = probePath;
                }
            }
            string buildVenvPath = Path.Combine(baseDir, "env", "Scripts", "python.exe");
            if (File.Exists(buildVenvPath)) return buildVenvPath;

            string sourceVenvPath = Path.GetFullPath(Path.Combine(baseDir, "..\\..\\..\\..\\env\\Scripts\\python.exe"));
            if (File.Exists(sourceVenvPath)) return sourceVenvPath;
            ChatDisplay.Text += $"[WARNING] Virtual env not found. Falling back to system 'python'\n";
            return "python";
        }
        else
        {
            #if DEV_PYTHON_ENV
                if (File.Exists(DEV_PYTHON_ENV)) return DEV_PYTHON_ENV;
            #endif

            string baseDir = AppContext.BaseDirectory;
            string relativePrefix = "";

            for (int i = 0; i <= 12; i++)
            {
                string combinedPath = Path.Combine(baseDir, relativePrefix + "env/bin/python3");
                string fullPath = Path.GetFullPath(combinedPath);

                if (File.Exists(fullPath)) return _cachedPythonPath = fullPath;
                relativePrefix += "../";
            }

            return _cachedPythonPath = "python3";
        }
    }

    private void SpeakText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        _speechQueue.Enqueue(text);

        if (!_isProcessingQueue)
        {
            _ = ProcessSpeechQueue();
        }
    }
    private async Task ProcessSpeechQueue()
    {
        if (_isProcessingQueue) return; // Prevent multiple instances

        _isProcessingQueue = true;
        _isAiSpeaking = true;

        bool wasMicOn = _isMicEnabled;
        if (wasMicOn)
        {
#if __MACCATALYST__
        StopMacVoiceInput();
#elif WINDOWS
            await StopWindowsVoiceInput();
#endif
        }

        // Keep processing until queue is empty
        do
        {
            while (_speechQueue.Count > 0)
            {
                string text = _speechQueue.Dequeue();
                string safeText = text.Replace("\"", "'").Replace("\r", " ").Replace("\n", " ");

                DispatcherQueue.TryEnqueue(() => {
                    ChatDisplay.Text += $"\n[SPEAK] Dequeued: '{safeText}'\n";
                    ChatDisplay.Text += $"[SPEAK] WebView ready: {_isWebViewReady}\n";
                    ChatScroll.ChangeView(null, ChatScroll.ScrollableHeight, null);
                });

                if (AvatarWebView?.CoreWebView2 != null)
                {
                    try
                    {
                        string jsSafeText = safeText.Replace("'", "\\'").Replace("\"", "\\\"");

                        DispatcherQueue.TryEnqueue(() => {
                            ChatDisplay.Text += $"[C#->JS] sending to processNativeSpeech...\n";
                            ChatScroll.ChangeView(null, ChatScroll.ScrollableHeight, null);
                        });

#if __MACCATALYST__
                    string js = $"window.speakLine('{jsSafeText}'); void(0);";
                    await AvatarWebView.ExecuteScriptAsync(js);
#else
                        _speechCompletionTcs = new TaskCompletionSource<bool>();

                        await AvatarWebView.ExecuteScriptAsync($"window.processNativeSpeech('{jsSafeText}');");

                        var completionTcs = _speechCompletionTcs;
                        await completionTcs.Task;
#endif

                        DispatcherQueue.TryEnqueue(() => {
                            ChatDisplay.Text += $"[C#->JS] ✓ Completed\n";
                        });
                    }
                    catch (Exception ex)
                    {
                        DispatcherQueue.TryEnqueue(() => {
                            ChatDisplay.Text += $"[ERROR] Avatar send failed: {ex.Message}\n";
                        });
                    }
                }
            }
        } while (_speechQueue.Count > 0); // Check again in case new items arrived

        _isAiSpeaking = false;
        _isProcessingQueue = false;

        DispatcherQueue.TryEnqueue(async () => {
            if (wasMicOn && _isMicEnabled)
            {
#if __MACCATALYST__
            await StartMacVoiceInput();
#elif WINDOWS
                await StartWindowsVoiceInput();
#endif
            }
        });
    }

    private void StartAIProcess(string mode)
    {
        try
        {
            string baseDir = AppContext.BaseDirectory;
            string subFolder = mode == "cloud" ? "Cloud" : "Local";
            string fileName = mode == "cloud" ? "cloud_prototype.py" : "local_prototype.py";

            string scriptPath = Path.Combine(baseDir, subFolder, fileName);

            if (!File.Exists(scriptPath) && !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                scriptPath = Path.GetFullPath(Path.Combine(baseDir, "..", "Resources", subFolder, fileName));
            }

            if (!File.Exists(scriptPath)) return;

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

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string pythonExe = GetPythonPath();
                if (pythonExe.Contains("env"))
                {
                    string venvRoot = Path.GetDirectoryName(Path.GetDirectoryName(pythonExe));
                    string sitePackages = Path.Combine(venvRoot, "Lib", "site-packages");
                    start.EnvironmentVariables["PYTHONPATH"] = sitePackages;
                }
            }

            _pythonProcess = new Process { StartInfo = start, EnableRaisingEvents = true };

            _pythonProcess.OutputDataReceived += (s, e) => {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    string cleanedData = Regex.Replace(e.Data, @"\x1B\[[^@-~]*[@-~]", string.Empty);
                    cleanedData = Regex.Replace(cleanedData, @"[^\u0000-\u007F]+", string.Empty);

                    DispatcherQueue.TryEnqueue(() => {
                        ChatDisplay.Text += $"{cleanedData}\n";

                        if (cleanedData.Trim().StartsWith("Talker:", StringComparison.OrdinalIgnoreCase))
                        {
                            string speechText = cleanedData.Replace("Talker:", "", StringComparison.OrdinalIgnoreCase).Trim();
                            speechText = speechText.Replace("\r", " ").Replace("\n", " ");
                            SpeakText(speechText);
                            _isTalkerActive = true;
                        }
                        else if (cleanedData.Trim().StartsWith("Critic:", StringComparison.OrdinalIgnoreCase))
                        {
                            _isTalkerActive = false;
                        }
                        else if (_isTalkerActive && !string.IsNullOrWhiteSpace(cleanedData.Trim()))
                        {
                            // Only speak if it's a continuation line (not empty, not a prefix)
                            cleanedData = cleanedData.Replace("\r", " ").Replace("\n", " ").Trim();
                            SpeakText(cleanedData);
                        }

                        ChatScroll.ChangeView(null, ChatScroll.ScrollableHeight, null);
                    });
                }
            };

            _pythonProcess.ErrorDataReceived += (s, e) => {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    string cleanedError = Regex.Replace(e.Data, @"\x1B\[[^@-~]*[@-~]", string.Empty);
                    DispatcherQueue.TryEnqueue(() => ChatDisplay.Text += $"[PYTHON ERROR]: {cleanedError}\n");
                }
            };

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string pythonExe = GetPythonPath();
                if (pythonExe.Contains("env")) 
                {
                    string venvRoot = Path.GetDirectoryName(Path.GetDirectoryName(pythonExe));
                    string libFolder = Path.Combine(venvRoot, "lib");
                    if (Directory.Exists(libFolder))
                    {
                        var pythonFolders = Directory.GetDirectories(libFolder, "python3.*");
                        if (pythonFolders.Length > 0)
                        {
                            string sitePackages = Path.Combine(pythonFolders[0], "site-packages");
                            start.EnvironmentVariables["PYTHONPATH"] = sitePackages;
                        }
                    }
                }
            }

            _pythonProcess.Start();
            _pythonProcess.BeginOutputReadLine();
            _pythonProcess.BeginErrorReadLine();
        }
        catch (Exception ex) { 
            ChatDisplay.Text += $"[RUNTIME ERROR]: {ex.Message}\n";
           
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
            ChatDisplay.Text += $"You: {message}\n";
            UserInput.Text = "";

            _silenceTimer.Stop();
            _isUserEditing = false;
        }
    }

    private void OnChatAIClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button b)
        {
            string mode = b.Tag?.ToString() ?? "local";
            LandingPage.Visibility = Visibility.Collapsed;
            ChatInterface.Visibility = Visibility.Visible;
            StartAIProcess(mode);
        }
    }
    
    private void OnSendClicked(object sender, RoutedEventArgs e) => SendMessage();
    private void OnInputKeyDown(object sender, KeyRoutedEventArgs e) { if (e.Key == Windows.System.VirtualKey.Enter) SendMessage(); }
    private void OnInputTextChanged(object sender, TextChangedEventArgs e) {
        if (UserInput.FocusState != FocusState.Unfocused) {
            _isUserEditing = true;
            _silenceTimer.Stop(); 
        }
    }

    private async void OnMicClicked(object sender, RoutedEventArgs e) {
        _isMicEnabled = !_isMicEnabled;
        
        if (_isMicEnabled) {
            MicIcon.Glyph = "\uE720";
            MicIcon.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red);
            _isUserEditing = false;
#if __MACCATALYST__
            await StartMacVoiceInput();
#elif WINDOWS
            await StartWindowsVoiceInput();
            #endif

        }
        else {
            MicIcon.Glyph = "\uF12E";
            MicIcon.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black);
            #if __MACCATALYST__
            StopMacVoiceInput();
            #elif WINDOWS
            await StopWindowsVoiceInput();
            #endif
        }
    }

#if WINDOWS
    private async Task StartWindowsVoiceInput()
    {
        try
        {
            if (_winRecognizer != null)
            {
                try
                {
                    if (_winRecognizer.State != SpeechRecognizerState.Idle)
                    {
                        await _winRecognizer.ContinuousRecognitionSession.StopAsync();
                    }
                }
                catch { }

                _winRecognizer.Dispose();
                _winRecognizer = null;
            }

            _winRecognizer = new SpeechRecognizer();
            _winRecognizer.ContinuousRecognitionSession.AutoStopSilenceTimeout = TimeSpan.MaxValue;

            var dictationConstraint = new SpeechRecognitionTopicConstraint(SpeechRecognitionScenario.Dictation, "dictation");
            _winRecognizer.Constraints.Add(dictationConstraint);

            var compilationResult = await _winRecognizer.CompileConstraintsAsync();
            if (compilationResult.Status != SpeechRecognitionResultStatus.Success)
            {
                throw new Exception($"Constraint compilation failed: {compilationResult.Status}");
            }

            _winRecognizer.HypothesisGenerated += (s, args) => {
                DispatcherQueue.TryEnqueue(() => {
                    if (!_isAiSpeaking && !_isUserEditing)
                    {
                        _silenceTimer.Stop();
                        UserInput.Text = args.Hypothesis.Text;
                    }
                });
            };

            _winRecognizer.ContinuousRecognitionSession.ResultGenerated += (s, args) => {
                if (args.Result.Status == SpeechRecognitionResultStatus.Success)
                {
                    DispatcherQueue.TryEnqueue(() => {
                        UserInput.Text = args.Result.Text;
                        if (AutoSendToggle.IsOn && !_isUserEditing && !_isAiSpeaking)
                        {
                            _silenceTimer.Stop();
                            _silenceTimer.Start();
                        }
                    });
                }
            };

            await _winRecognizer.ContinuousRecognitionSession.StartAsync();
        }
        catch (Exception ex)
        {
            _isMicEnabled = false;
            MicIcon.Glyph = "\uF12E";
            MicIcon.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black);

            if (_winRecognizer != null)
            {
                _winRecognizer.Dispose();
                _winRecognizer = null;
            }

            string hexError = string.Format("0x{0:X8}", (uint)ex.HResult);
            ChatDisplay.Text += $"\n[MIC ERROR {hexError}]: {ex.Message}\n";

            if ((uint)ex.HResult == 0x8004503A || (uint)ex.HResult == 0x80131509 || ex.Message.Contains("privacy"))
            {
                ChatDisplay.Text += "-> Opening Windows Speech Settings. Please turn ON 'Online Speech Recognition'.\n";
                var uri = new Uri("ms-settings:privacy-speech");
                _ = Windows.System.Launcher.LaunchUriAsync(uri);
            }
        }
    }

    private async Task StopWindowsVoiceInput()
    {
        if (_winRecognizer?.ContinuousRecognitionSession != null)
        {
            try
            {
                await _winRecognizer.ContinuousRecognitionSession.StopAsync();
                _silenceTimer.Stop();
            }
            catch { }
        }
    }
#endif

#if __MACCATALYST__
    public async Task StartMacVoiceInput()
    {
        try 
        {
            var tcs = new TaskCompletionSource<bool>();
            SFSpeechRecognizer.RequestAuthorization((status) => {
                tcs.SetResult(status == SFSpeechRecognizerAuthorizationStatus.Authorized);
            });

            if (!await tcs.Task) return;

            var audioSession = AVAudioSession.SharedInstance();
            audioSession.SetCategory(AVAudioSessionCategory.PlayAndRecord, 
                                    AVAudioSessionCategoryOptions.DefaultToSpeaker | 
                                    AVAudioSessionCategoryOptions.AllowBluetooth);
            audioSession.SetActive(true);


            _recognitionRequest = new SFSpeechAudioBufferRecognitionRequest { ShouldReportPartialResults = true };
            
            var inputNode = _audioEngine.InputNode;
            if (inputNode == null) throw new Exception("Audio Input Node is null.");

            var recordingFormat = inputNode.GetBusOutputFormat(0);
            
            inputNode.InstallTapOnBus(0, 1024, recordingFormat, (buffer, when) => {
                _recognitionRequest?.Append(buffer);
            });

            _audioEngine.Prepare();
            _audioEngine.StartAndReturnError(out var err);
            if (err != null) throw new Exception(err.Description);

            _recognitionTask = _speechRecognizer.GetRecognitionTask(_recognitionRequest, (result, error) => {
                if (result != null) {
                    string transcribedText = result.BestTranscription.FormattedString;

                    DispatcherQueue.TryEnqueue(() => {
                        if (!_isUserEditing && !_isAiSpeaking) {
                            UserInput.Text = transcribedText;
                            _silenceTimer.Stop();
                            _silenceTimer.Start(); 
                        }
                    });
                }
            });
        }
        catch (Exception ex)
        {
            DispatcherQueue.TryEnqueue(() => ChatDisplay.Text += $"[MIC ERROR]: {ex.Message}\n");
        }
    }

    public void StopMacVoiceInput()
    {
        _silenceTimer.Stop();
        
        if (_audioEngine.Running)
        {
            _audioEngine.Stop();
            _audioEngine.InputNode.RemoveTapOnBus(0);
        }
        
        _recognitionRequest?.EndAudio();
        _recognitionTask?.Cancel();
        
        var audioSession = AVAudioSession.SharedInstance();
        audioSession.SetActive(false, AVAudioSessionSetActiveOptions.NotifyOthersOnDeactivation, out _);
    }
#endif

    private async void OnBackClicked(object sender, RoutedEventArgs e)
    {
        if (_pythonProcess != null && !_pythonProcess.HasExited)
        {
            try { _pythonProcess.Kill(); } catch { }
        }

        _isMicEnabled = false;
        MicIcon.Glyph = "\uE720";
        MicIcon.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black);
        #if __MACCATALYST__
        StopMacVoiceInput();
        #elif WINDOWS
        await StopWindowsVoiceInput();
        #endif

        ChatDisplay.Text = "";
        UserInput.Text = "";
        ChatInterface.Visibility = Visibility.Collapsed;
        LandingPage.Visibility = Visibility.Visible;
    }
}
