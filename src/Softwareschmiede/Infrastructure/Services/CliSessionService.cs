using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;


namespace Softwareschmiede.Infrastructure.Services
{
    using System.Diagnostics;

    public class CliSessionService
    {
        private Process? _process;
        private StreamWriter? _stdin;
        private Func<string, Task>? _onOutput;

        public bool IsRunning => _process != null && !_process.HasExited;

        public async Task StartAsync(string cliName, string workingDir, Func<string, Task> onOutput)
        {
            if (IsRunning)
                return;

            _onOutput = onOutput;

            var baseCommand = cliName.ToLower() switch
            {
                "copilot" => "copilot chat .",
                "claude" => "claude chat .",
                _ => throw new InvalidOperationException("Unknown CLI")
            };

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoExit -Command \"{baseCommand}\"",
                WorkingDirectory = workingDir,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            _process = Process.Start(psi);
            _stdin = _process.StandardInput;

            _ = Task.Run(ReadOutputLoop);
        }

        private async Task ReadOutputLoop()
        {
            while (_process != null && !_process.HasExited)
            {
                var line = await _process.StandardOutput.ReadLineAsync();
                if (line != null && _onOutput != null)
                    await _onOutput(line + "\n");
            }
        }

        public Task SendAsync(string input)
        {
            _stdin?.WriteLine(input);
            _stdin?.Flush();
            return Task.CompletedTask;
        }
    }

}
