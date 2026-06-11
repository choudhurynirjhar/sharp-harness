using System.Diagnostics;
using System.Text.Json;

namespace AgentHarness.Tools;

public sealed class CommandPromptTool : IAgentTool
{
    private const int MaxOutputLength = 32_000;
    private const int DefaultTimeoutSeconds = 30;

    private static readonly JsonElement Schema = JsonSerializer.Deserialize<JsonElement>(
        """
        {
          "type": "object",
          "required": ["command"],
          "properties": {
            "command": {
              "type": "string",
              "description": "Shell command to execute."
            },
            "working_directory": {
              "type": "string",
              "description": "Optional working directory for the command."
            },
            "timeout_seconds": {
              "type": "integer",
              "description": "Optional timeout in seconds. Defaults to 30."
            }
          }
        }
        """);

    public string Name => "run_command";

    public string Description =>
        "Runs a shell command and returns exit code, stdout, and stderr.";

    public JsonElement ParametersSchema => Schema;

    public async Task<string> InvokeAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        if (!arguments.TryGetProperty("command", out var commandElement) ||
            commandElement.ValueKind != JsonValueKind.String)
        {
            throw new ArgumentException("Missing required argument 'command'.");
        }

        var command = commandElement.GetString()?.Trim();
        if (string.IsNullOrWhiteSpace(command))
        {
            throw new ArgumentException("Argument 'command' must not be empty.");
        }

        var workingDirectory = arguments.TryGetProperty("working_directory", out var directoryElement) &&
                               directoryElement.ValueKind == JsonValueKind.String
            ? directoryElement.GetString()
            : null;

        if (!string.IsNullOrWhiteSpace(workingDirectory) && !Directory.Exists(workingDirectory))
        {
            throw new ArgumentException($"Working directory '{workingDirectory}' does not exist.");
        }

        var timeoutSeconds = DefaultTimeoutSeconds;
        if (arguments.TryGetProperty("timeout_seconds", out var timeoutElement) &&
            timeoutElement.TryGetInt32(out var requestedTimeout) &&
            requestedTimeout > 0)
        {
            timeoutSeconds = requestedTimeout;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        var processStartInfo = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/bash",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            processStartInfo.WorkingDirectory = workingDirectory;
        }

        if (OperatingSystem.IsWindows())
        {
            processStartInfo.ArgumentList.Add("/c");
        }
        else
        {
            processStartInfo.ArgumentList.Add("-c");
        }

        processStartInfo.ArgumentList.Add(command);

        using var process = new Process { StartInfo = processStartInfo };
        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
        var stderrTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Best effort if the process already exited.
            }

            throw new TimeoutException($"Command timed out after {timeoutSeconds} seconds.");
        }

        var stdout = Truncate(await stdoutTask);
        var stderr = Truncate(await stderrTask);

        var result = new
        {
            exit_code = process.ExitCode,
            stdout = stdout.Text,
            stderr = stderr.Text,
            stdout_truncated = stdout.Truncated,
            stderr_truncated = stderr.Truncated,
            command,
            working_directory = workingDirectory
        };

        return JsonSerializer.Serialize(result);
    }

    private static (string Text, bool Truncated) Truncate(string value)
    {
        if (value.Length <= MaxOutputLength)
        {
            return (value, false);
        }

        return (value[..MaxOutputLength], true);
    }
}
