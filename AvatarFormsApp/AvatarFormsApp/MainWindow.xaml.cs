using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace AvatarFormsApp
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private System.Diagnostics.Process _pythonProcess;
        public MainWindow()
        {
            this.InitializeComponent();
            this.Closed += (s, e) => {
                if (_pythonProcess != null && !_pythonProcess.HasExited)
                {
                    _pythonProcess.Kill();
                }
            };
        }

        private void OnChatAIClicked(object sender, RoutedEventArgs e)
        {
            LandingPage.Visibility = Visibility.Collapsed;
            ChatInterface.Visibility = Visibility.Visible;

            ChatDisplay.Text += "[SYSTEM]: Initializing AI Agent...\n";
            StartAIProcess();
        }

        private string GetPythonPath()
        {
            string baseDirectory = Windows.ApplicationModel.Package.Current.InstalledLocation.Path;
            string bundledPath = Path.Combine(baseDirectory, "env", "Scripts", "python.exe");

            if (File.Exists(bundledPath))
            {
                return bundledPath;
            }

            return "python";
        }

        private void StartAIProcess()
        {
            string baseDirectory = Windows.ApplicationModel.Package.Current.InstalledLocation.Path;
            string scriptPath = Path.Combine(baseDirectory, "Local", "local_prototype.py");

            if (!File.Exists(scriptPath))
            {
                ChatDisplay.Text += $"[SYSTEM ERROR]: Missing script at {scriptPath}\n";
                return;
            }

            ProcessStartInfo start = new ProcessStartInfo
            {
                FileName = GetPythonPath(),
                Arguments = $"-u \"{scriptPath}\"",
                WorkingDirectory = baseDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            _pythonProcess = new Process { StartInfo = start, EnableRaisingEvents = true };

            _pythonProcess.OutputDataReceived += (s, e) => {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    string cleanData = System.Text.RegularExpressions.Regex.Replace(e.Data, @"\x1B\[[^m]*m", "");

                    DispatcherQueue.TryEnqueue(() => {
                        ChatDisplay.Text += $"{cleanData}\n";
                        ChatScroll.ChangeView(null, ChatScroll.ScrollableHeight, null);
                    });
                }
            };

            _pythonProcess.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    DispatcherQueue.TryEnqueue(() => {
                        ChatDisplay.Text += $"[PYTHON ERROR]: {e.Data}\n";
                    });
                }
            };

            _pythonProcess.Start();
            _pythonProcess.BeginOutputReadLine();
            _pythonProcess.BeginErrorReadLine();
        }

        private void OnSendClicked(object sender, RoutedEventArgs e)
        {
            SendMessage();
        }

        private void OnInputKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                SendMessage();
            }
        }

        private void SendMessage()
        {
            string message = UserInput.Text;
            if (!string.IsNullOrWhiteSpace(message) && _pythonProcess != null && !_pythonProcess.HasExited)
            {
                try
                {
                    _pythonProcess.StandardInput.WriteLine(message);
                    _pythonProcess.StandardInput.Flush();
                    ChatDisplay.Text += $"You: {message}\n";
                    UserInput.Text = "";
                }
                catch (Exception ex)
                {
                    ChatDisplay.Text += $"[SEND ERROR]: {ex.Message}\n";
                }
            }
        }
    }
}
