using System;
using System.Diagnostics;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace AvatarFormsApp;

public sealed partial class MainPage : Page
{
    private Process? _pythonProcess;

    public MainPage()
    {
        try
        {
            InitializeComponent();

            LandingPage.Visibility = Visibility.Visible;
            ChatInterface.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[XAML ERROR] MainPage.xaml failed: {ex}");
            throw;
        }
    }

    private void OnChatAIClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is Button button)
            {
                string mode = button.Tag?.ToString() ?? "local";
                LandingPage.Visibility = Visibility.Collapsed;
                ChatInterface.Visibility = Visibility.Visible;
                ChatDisplay.Text += $"[SYSTEM]: Initializing {mode.ToUpper()} AI Agent...\n";
                StartAIProcess(mode);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RUNTIME ERROR] OnChatAIClicked failed: {ex}");
        }
    }

    private string GetPythonPath()
    {
        string baseDir = AppContext.BaseDirectory;
        bool isWindows = System.Runtime.InteropServices.RuntimeInformation
            .IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);

        string venvPath = isWindows
            ? Path.Combine(baseDir, "env", "Scripts", "python.exe")
            : Path.Combine(baseDir, "env", "bin", "python3");

        return File.Exists(venvPath) ? venvPath : (isWindows ? "python" : "python3");
    }

    private void StartAIProcess(string mode)
    {
        try
        {
            string baseDir = AppContext.BaseDirectory;
            string subFolder = mode == "cloud" ? "Cloud" : "Local";
            string fileName = mode == "cloud" ? "cloud_prototype.py" : "local_prototype.py";
            string scriptPath = Path.Combine(baseDir, subFolder, fileName);

            if (!File.Exists(scriptPath))
            {
                ChatDisplay.Text += $"[SYSTEM ERROR]: Missing script at {scriptPath}\n";
                return;
            }

            var start = new ProcessStartInfo
            {
                FileName = GetPythonPath(),
                Arguments = $"-u \"{scriptPath}\"",
                WorkingDirectory = Path.Combine(baseDir, subFolder),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            _pythonProcess = new Process { StartInfo = start, EnableRaisingEvents = true };

            _pythonProcess.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        ChatDisplay.Text += $"{e.Data}\n";
                        ChatScroll.ChangeView(null, ChatScroll.ScrollableHeight, null);
                    });
                }
            };

            _pythonProcess.Start();
            _pythonProcess.BeginOutputReadLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RUNTIME ERROR] StartAIProcess failed: {ex}");
        }
    }

    private void OnSendClicked(object sender, RoutedEventArgs e) => SendMessage();

    private void OnInputKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
            SendMessage();
    }

    private void SendMessage()
    {
        try
        {
            if (_pythonProcess == null || _pythonProcess.HasExited)
                return;

            var message = UserInput.Text;
            if (!string.IsNullOrWhiteSpace(message))
            {
                _pythonProcess.StandardInput.WriteLine(message);
                ChatDisplay.Text += $"You: {message}\n";
                UserInput.Text = "";
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RUNTIME ERROR] SendMessage failed: {ex}");
        }
    }
}
