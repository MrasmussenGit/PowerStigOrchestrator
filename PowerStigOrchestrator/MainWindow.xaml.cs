using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace PowerStigOrchestrator
{
    public partial class MainWindow : Window
    {
        private const string PowerStigConverterDisplay = "PowerStig Converter UI";
        private const string MOFInspectorDisplay = "MOF Inspector";

        private string? _powerStigConverterPath;
        private string? _mofInspectorPath;

        private System.Windows.Threading.DispatcherTimer? _busyTimer;
        private DateTime _busyStart;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Set version text from AssemblyInformationalVersion (maps to <Version> in .csproj)
            VersionTextBlock.Text = $"Version: {GetProductVersion()}";

            // Find the nearest "Apps" folder by walking up from the executable location
            var appsFolder = FindAppsFolder(AppContext.BaseDirectory);
            if (appsFolder is null)
            {
                PowerStigConverterUIButton.Content = $"{PowerStigConverterDisplay} (not found)";
                PowerStigConverterUIButton.IsEnabled = false;
                MOFInspectorButton.Content = $"{MOFInspectorDisplay} (not found)";
                MOFInspectorButton.IsEnabled = false;
                return;
            }

            var currentExe = Path.GetFileName(Environment.ProcessPath ?? string.Empty);

            var exes = Directory.EnumerateFiles(appsFolder, "*.exe", SearchOption.TopDirectoryOnly)
                                .Where(p => !string.Equals(Path.GetFileName(p), currentExe, StringComparison.OrdinalIgnoreCase))
                                .ToList();

            _powerStigConverterPath = FindClosestMatch(exes, PowerStigConverterDisplay);
            if (!string.IsNullOrWhiteSpace(_powerStigConverterPath) && File.Exists(_powerStigConverterPath))
            {
                PowerStigConverterUIButton.Content = PowerStigConverterDisplay;
                PowerStigConverterUIButton.IsEnabled = true;
                PowerStigConverterUIButton.ToolTip = _powerStigConverterPath;
            }
            else
            {
                PowerStigConverterUIButton.Content = $"{PowerStigConverterDisplay} (not found)";
                PowerStigConverterUIButton.IsEnabled = false;
            }

            _mofInspectorPath = FindClosestMatch(exes, MOFInspectorDisplay);
            if (!string.IsNullOrWhiteSpace(_mofInspectorPath) && File.Exists(_mofInspectorPath))
            {
                MOFInspectorButton.Content = MOFInspectorDisplay;
                MOFInspectorButton.IsEnabled = true;
                MOFInspectorButton.ToolTip = _mofInspectorPath;
            }
            else
            {
                MOFInspectorButton.Content = $"{MOFInspectorDisplay} (not found)";
                MOFInspectorButton.IsEnabled = false;
            }
        }

        private static string GetProductVersion()
        {
            var asm = Assembly.GetExecutingAssembly();

            string?[] candidates =
            [
                asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
                FileVersionInfo.GetVersionInfo(asm.Location).ProductVersion,
                asm.GetName().Version?.ToString()
            ];

            foreach (var candidate in candidates)
            {
                if (string.IsNullOrWhiteSpace(candidate)) continue;

                var s = candidate.Trim();
                if (s.Length > 0 && (s[0] == 'v' || s[0] == 'V'))
                    s = s[1..];

                var numeric = ExtractVersionPrefix(s);
                if (!string.IsNullOrEmpty(numeric))
                    return numeric;
            }

            return "unknown";
        }

        private static string ExtractVersionPrefix(string s)
        {
            int i = 0;
            bool sawDigit = false;
            while (i < s.Length)
            {
                char c = s[i];
                if (char.IsDigit(c))
                {
                    sawDigit = true;
                    i++;
                    continue;
                }
                if (c == '.' && sawDigit)
                {
                    i++;
                    continue;
                }
                break;
            }
            var result = s.Substring(0, i).Trim('.');
            return result;
        }

        private static string? FindAppsFolder(string startDirectory)
        {
            var dir = new DirectoryInfo(startDirectory);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "Apps");
                if (Directory.Exists(candidate))
                    return candidate;
                dir = dir.Parent;
            }
            return null;
        }

        private static string? FindClosestMatch(System.Collections.Generic.IEnumerable<string> exePaths, string targetDisplayName)
        {
            static string Normalize(string s) =>
                new string(s.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

            static string[] Tokens(string s) =>
                s.Split(new[] { ' ', '.', '-', '_', '+' }, StringSplitOptions.RemoveEmptyEntries)
                 .Select(t => t.ToLowerInvariant()).ToArray();

            var targetNorm = Normalize(targetDisplayName);
            var targetTokens = Tokens(targetDisplayName);

            string? bestPath = null;
            int bestScore = int.MinValue;

            foreach (var path in exePaths)
            {
                var name = Path.GetFileNameWithoutExtension(path);
                var nameNorm = Normalize(name);
                var nameTokens = Tokens(name);

                int score = 0;

                if (nameNorm.Contains(targetNorm))
                    score += 100;

                int overlap = targetTokens.Count(t => nameTokens.Contains(t));
                score += overlap * 10;

                if (targetTokens.Length > 0 && nameTokens.Length > 0 && nameTokens[0].StartsWith(targetTokens[0]))
                    score += 5;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestPath = path;
                }
            }

            return bestScore >= 10 ? bestPath : null;
        }

        // Show/hide busy UI (same UX pattern as Convert STIG)
        private void SetBusy(bool isBusy, string? status = null)
        {
            BusyPanel.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;
            BusyStatusText.Text = isBusy ? (status ?? "Launching…") : string.Empty;
            Cursor = isBusy ? Cursors.Wait : Cursors.Arrow;

            if (isBusy)
            {
                _busyStart = DateTime.Now;
                _busyTimer ??= new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(1)
                };
                _busyTimer.Tick -= BusyTimer_Tick;
                _busyTimer.Tick += BusyTimer_Tick;
                _busyTimer.Start();

                // Disable actions while launching
                PowerStigConverterUIButton.IsEnabled = false;
                MOFInspectorButton.IsEnabled = false;
                ExitButton.IsEnabled = false;
            }
            else
            {
                if (_busyTimer is not null) _busyTimer.Stop();

                // Re-enable based on availability
                PowerStigConverterUIButton.IsEnabled = !string.IsNullOrWhiteSpace(_powerStigConverterPath) && File.Exists(_powerStigConverterPath);
                MOFInspectorButton.IsEnabled = !string.IsNullOrWhiteSpace(_mofInspectorPath) && File.Exists(_mofInspectorPath);
                ExitButton.IsEnabled = true;
            }
        }

        private void BusyTimer_Tick(object? sender, EventArgs e)
        {
            var elapsed = DateTime.Now - _busyStart;
            BusyStatusText.Text = $"Launching… elapsed {elapsed:mm\\:ss}";
        }

        private async Task<bool> WaitForGuiReadyAsync(Process proc, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;

            // A quick try; some apps are ready almost immediately
            try { proc.WaitForInputIdle(100); } catch { /* ignore */ }

            while (DateTime.UtcNow < deadline)
            {
                if (proc.HasExited) return true;

                try
                {
                    if (proc.MainWindowHandle != IntPtr.Zero)
                    {
                        await Task.Delay(200);
                        return true;
                    }

                    try
                    {
                        if (proc.WaitForInputIdle(250)) return true;
                    }
                    catch { /* ignore; not ready */ }
                }
                catch { /* ignore */ }

                await Task.Delay(250);
            }

            return false; // timed out waiting for UI
        }

        private async Task LaunchWithBusyAsync(string displayName, string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                MessageBox.Show(
                    $"{displayName} wasn't found at:\n{(string.IsNullOrWhiteSpace(path) ? "(no path resolved)" : path)}\n\n" +
                    "Please download the app you are missing from:\nhttps://github.com/MrasmussenGit\n\n" +
                    "Open the repository for the app, go to the Releases page, and download the x64 Windows binary from the Assets section. " +
                    "Place the .exe into an 'Apps' folder next to the launcher.",
                    "Application not found",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            SetBusy(true, "Launching…");

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = path,
                    WorkingDirectory = Path.GetDirectoryName(path)!,
                    UseShellExecute = false // needed to wait for GUI readiness
                };

                using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };

                if (!proc.Start())
                    throw new InvalidOperationException("Process failed to start.");

                // Wait until the app's UI is ready or timeout
                await WaitForGuiReadyAsync(proc, TimeSpan.FromSeconds(60));
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to launch {displayName} at:\n{path}\n\n{ex.Message}",
                    "Error launching application",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async void PowerStigConverterUIButton_Click(object sender, RoutedEventArgs e) =>
            await LaunchWithBusyAsync(PowerStigConverterDisplay, _powerStigConverterPath);

        private async void MOFInspectorButton_Click(object sender, RoutedEventArgs e) =>
            await LaunchWithBusyAsync(MOFInspectorDisplay, _mofInspectorPath);

        private void ExitButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}