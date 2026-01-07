using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace AvatarFormsApp;

public sealed partial class MainPage : Page
{
    private Process? _pythonProcess;

    public MainPage()
    {
        try { InitializeComponent(); }
        catch (Exception ex) { Console.WriteLine($"[XAML ERROR]: {ex}"); throw; }
    }

    private string GetPythonPath()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            string baseDir = AppContext.BaseDirectory;
            // 1. Check build output folder (standard)
            string buildVenvPath = Path.Combine(baseDir, "env", "Scripts", "python.exe");
            if (File.Exists(buildVenvPath)) return buildVenvPath;

            // 2. Check source project folder fallback
            string sourceVenvPath = Path.GetFullPath(Path.Combine(baseDir, "..\\..\\..\\..\\env\\Scripts\\python.exe"));
            if (File.Exists(sourceVenvPath)) return sourceVenvPath;

            return "python";
        }
        else
        {
#if DEV_PYTHON_ENV
            if (File.Exists(DEV_PYTHON_ENV)) return DEV_PYTHON_ENV;
#endif
            return "python3";
        }
    }

    private void StartAIProcess(string mode)
    {
        try
        {
            string baseDir = AppContext.BaseDirectory;
            string subFolder = mode == "cloud" ? "Cloud" : "Local";
            string fileName = mode == "cloud" ? "cloud_prototype.py" : "local_prototype.py";

            string scriptPath = Path.Combine(baseDir, subFolder, fileName);

            // Mac-specific bundle fallback
            if (!File.Exists(scriptPath) && !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                scriptPath = Path.GetFullPath(Path.Combine(baseDir, "..", "Resources", subFolder, fileName));
            }

            if (!File.Exists(scriptPath))
            {
                ChatDisplay.Text += $"[SYSTEM ERROR]: Missing script at {scriptPath}\n";
                return;
            }

            var start = new ProcessStartInfo
            {
                FileName = GetPythonPath(),
                Arguments = $"-u \"{scriptPath}\"",
                // Start inside the folder (Cloud or Local) so Python finds token.txt natively
                WorkingDirectory = Path.GetDirectoryName(scriptPath),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            // Windows-specific environment fix for 'dotenv'
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
                    DispatcherQueue.TryEnqueue(() => {
                        ChatDisplay.Text += $"{cleanedData}\n";
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
}
