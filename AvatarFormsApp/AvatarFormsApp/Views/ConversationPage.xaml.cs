using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using AvatarFormsApp.ViewModels;
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

    // State Variables
    private bool _isMicEnabled = false;
    private bool _isUserEditing = false;
    private bool _isAiSpeaking = false;
    private DispatcherTimer _silenceTimer;

    private bool _isTalkerActive = false;

    private System.Collections.Generic.Queue<string> _speechQueue = new();
    private bool _isProcessingQueue = false;

#if WINDOWS
    private SpeechRecognizer _winRecognizer;
#endif

#if __MACCATALYST__
    private SFSpeechRecognizer _speechRecognizer = new SFSpeechRecognizer(new NSLocale("en-US"));
    private SFSpeechAudioBufferRecognitionRequest _recognitionRequest;
    private SFSpeechRecognitionTask _recognitionTask;
    private AVAudioEngine _audioEngine = new AVAudioEngine();
#endif

    public ConversationPageViewModel ViewModel { get; }
    public ConversationPage()
    {
        try { 
            InitializeComponent(); 
            ViewModel = App.GetService<ConversationPageViewModel>();
        }
        catch (Exception ex) { Console.WriteLine($"[XAML ERROR]: {ex}"); throw; }

        _silenceTimer = new DispatcherTimer();
        _silenceTimer.Interval = TimeSpan.FromSeconds(2.0);
        _silenceTimer.Tick += (s, e) => {
            _silenceTimer.Stop();
            if (AutoSendToggle.IsOn && !_isUserEditing && !_isAiSpeaking && !string.IsNullOrWhiteSpace(UserInput.Text)) 
            {
                SendMessage(); 
            }
        };
    }

    private void OnAutoSendToggled(object sender, RoutedEventArgs e)
    {

    }

    private string GetPythonPath()
    {
        if (!string.IsNullOrEmpty(_cachedPythonPath)) return _cachedPythonPath;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            string baseDir = AppContext.BaseDirectory;
            string buildVenvPath = Path.Combine(baseDir, "env", "Scripts", "python.exe");
            if (File.Exists(buildVenvPath)) return buildVenvPath;

            string sourceVenvPath = Path.GetFullPath(Path.Combine(baseDir, "..\\..\\..\\..\\env\\Scripts\\python.exe"));
            if (File.Exists(sourceVenvPath)) return sourceVenvPath;

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
        _isProcessingQueue = true;
        _isAiSpeaking = true;

        bool wasMicOn = _isMicEnabled;
        if (wasMicOn)
        {
            #if __MACCATALYST__
            StopMacVoiceInput();
            #endif
        }

        while (_speechQueue.Count > 0)
        {
            string text = _speechQueue.Dequeue();
            string safeText = text.Replace("\"", "'").Replace("\r", " ").Replace("\n", " ");
            
            try 
            {
                Process? proc = null;

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    string psCommand = $"Add-Type -AssemblyName System.Speech; (New-Object System.Speech.Synthesis.SpeechSynthesizer).Speak('{safeText}')";
                    proc = Process.Start(new ProcessStartInfo 
                    { 
                        FileName = "powershell", 
                        Arguments = $"-Command \"{psCommand}\"", 
                        CreateNoWindow = true, 
                        UseShellExecute = false 
                    });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || RuntimeInformation.IsOSPlatform(OSPlatform.Create("MACCATALYST")))
                {
                    string macSafeText = safeText.Replace("'", "");
                    proc = Process.Start(new ProcessStartInfo 
                    { 
                        FileName = "say", 
                        Arguments = $"\"{macSafeText}\"", 
                        UseShellExecute = false, 
                        CreateNoWindow = true 
                    });
                }

                if (proc != null) await proc.WaitForExitAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Speech Error: {ex.Message}");
            }
        }

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
                    } else if (!cleanedData.Trim().StartsWith("Critic:", StringComparison.OrdinalIgnoreCase) && _isTalkerActive)
                    {
                        cleanedData = cleanedData.Replace("\r", " ").Replace("\n", " ");
                        SpeakText(cleanedData);
                    } else
                    {
                        _isTalkerActive = false;
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
        catch (Exception ex) { ChatDisplay.Text += $"[RUNTIME ERROR]: {ex.Message}\n"; }
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
            MicIcon.Glyph = "\uE720"; // Mic
            MicIcon.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red);
            _isUserEditing = false;
#if __MACCATALYST__
            await StartMacVoiceInput();
#elif WINDOWS
            await StartWindowsVoiceInput();
            #endif

        }
        else {
            MicIcon.Glyph = "\uF12E"; // Mic with Slash
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
            if (_winRecognizer == null)
            {
                _winRecognizer = new SpeechRecognizer();
                var dictationConstraint = new SpeechRecognitionTopicConstraint(SpeechRecognitionScenario.Dictation, "dictation");
                _winRecognizer.Constraints.Add(dictationConstraint);
                await _winRecognizer.CompileConstraintsAsync();

                _winRecognizer.HypothesisGenerated += (s, args) => {
                    DispatcherQueue.TryEnqueue(() => {
                        if (!_isAiSpeaking && !_isUserEditing)
                        {
                            _silenceTimer.Stop();
                            UserInput.Text = args.Hypothesis.Text;
                        }
                    });
                };

                _winRecognizer.ContinuousRecognitionSession.ResultGenerated += async (s, args) => {
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
            }

            await _winRecognizer.ContinuousRecognitionSession.StartAsync();
        }
        catch (Exception ex)
        {
            _isMicEnabled = false;
            MicIcon.Glyph = "\uF12E";
            MicIcon.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black);

            string hexError = string.Format("0x{0:X8}", (uint)ex.HResult);
            ChatDisplay.Text += $"\n[MIC ERROR {hexError}]: {ex.Message}\n";

            if ((uint)ex.HResult == 0x8004503A || ex.Message.Contains("privacy"))
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

                await Task.Delay(100);

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
                            // "Half-Duplex" Check: Ignore any input if AI is marked as speaking
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
