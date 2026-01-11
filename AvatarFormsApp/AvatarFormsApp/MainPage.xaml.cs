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
#if __MACCATALYST__
using Speech;
using AVFoundation;
using Foundation;
#endif

namespace AvatarFormsApp;

public sealed partial class MainPage : Page
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

    #if __MACCATALYST__
    private SFSpeechRecognizer _speechRecognizer = new SFSpeechRecognizer(new NSLocale("en-US"));
    private SFSpeechAudioBufferRecognitionRequest _recognitionRequest;
    private SFSpeechRecognitionTask _recognitionTask;
    private AVAudioEngine _audioEngine = new AVAudioEngine();
    #endif

    public MainPage()
    {
        try { InitializeComponent(); }
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

        // 1. Enqueue the text instead of speaking immediately
        _speechQueue.Enqueue(text);

        // 2. If the processor isn't running, start it
        if (!_isProcessingQueue)
        {
            _ = ProcessSpeechQueue();
        }
    }

    private async Task ProcessSpeechQueue()
    {
        _isProcessingQueue = true;
        _isAiSpeaking = true;

        // A. Stop the mic ONLY ONCE for the entire batch of lines
        bool wasMicOn = _isMicEnabled;
        if (wasMicOn)
        {
            #if __MACCATALYST__
            StopMacVoiceInput();
            #endif
        }

        // B. Process every line in the queue sequentially
        while (_speechQueue.Count > 0)
        {
            // Get the next line
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

                // Wait for this specific line to finish before starting the next one
                if (proc != null) await proc.WaitForExitAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Speech Error: {ex.Message}");
            }
        }

        // C. Batch finished: Reset state and restart mic
        _isAiSpeaking = false;
        _isProcessingQueue = false;

        // Ensure we are on the UI thread to restart the mic
        DispatcherQueue.TryEnqueue(async () => {
            if (wasMicOn && _isMicEnabled) 
            {
                #if __MACCATALYST__
                await StartMacVoiceInput();
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

    private void OnMicClicked(object sender, RoutedEventArgs e) {
        _isMicEnabled = !_isMicEnabled;
        
        if (_isMicEnabled) {
            MicIcon.Glyph = "\uE720"; // Mic
            MicIcon.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red);
            _isUserEditing = false;
            #if __MACCATALYST__
            _ = StartMacVoiceInput();
            #endif
        } else {
            MicIcon.Glyph = "\uF12E"; // Mic with Slash
            MicIcon.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black);
            #if __MACCATALYST__
            StopMacVoiceInput();
            #endif
        }
    }

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

                // Simplified Session: Just "PlayAndRecord" to allow mic + speaker usage, no AEC complications
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
                // Critical crash prevention: Must remove tap before stopping session
                _audioEngine.InputNode.RemoveTapOnBus(0);
            }
            
            _recognitionRequest?.EndAudio();
            _recognitionTask?.Cancel();
            
            var audioSession = AVAudioSession.SharedInstance();
            audioSession.SetActive(false, AVAudioSessionSetActiveOptions.NotifyOthersOnDeactivation, out _);
        }
    #endif

        private void OnBackClicked(object sender, RoutedEventArgs e)
        {
            // 1. Kill the Python process so it doesn't run in the background
            if (_pythonProcess != null && !_pythonProcess.HasExited)
            {
                try { _pythonProcess.Kill(); } catch { }
            }

            // 2. Ensure the Mic is stopped
            _isMicEnabled = false;
            MicIcon.Glyph = "\uE720";
            MicIcon.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black);
            #if __MACCATALYST__
            StopMacVoiceInput();
            #endif

            // 3. Reset UI state
            ChatDisplay.Text = "";
            UserInput.Text = "";
            ChatInterface.Visibility = Visibility.Collapsed;
            LandingPage.Visibility = Visibility.Visible;
        }
}