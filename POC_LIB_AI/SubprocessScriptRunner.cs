using System.Diagnostics;
using System.Text.Json;
using Microsoft.Agents.AI;

namespace POC_LIB_AI;

/// <summary>Runs file-based skill scripts as local subprocesses.</summary>
/// <remarks>
/// Scripts execute with the permissions of the current application. Only use this runner with
/// skill folders whose contents are trusted.
/// </remarks>
internal static class SubprocessScriptRunner
{
    private static readonly TimeSpan s_scriptTimeout = TimeSpan.FromSeconds(30);

    /// <summary>Runs a skill script and returns its output or an error message.</summary>
    public static async Task<object?> RunAsync(
        AgentFileSkill skill,
        AgentFileSkillScript script,
        JsonElement? arguments,
        IServiceProvider? serviceProvider,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(script.FullPath))
        {
            return $"Error: Script file not found: {script.FullPath}";
        }

        string extension = Path.GetExtension(script.FullPath);
        string? interpreter = extension.ToLowerInvariant() switch
        {
            ".py" => OperatingSystem.IsWindows() ? "python" : "python3",
            ".js" => "node",
            ".sh" => "bash",
            ".ps1" => "pwsh",
            _ => null,
        };

        if (interpreter is null)
        {
            return $"Error: Unsupported skill script type '{extension}'. Supported types are .py, .js, .sh, and .ps1.";
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = interpreter,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(script.FullPath) ?? ".",
        };

        if (extension.Equals(".ps1", StringComparison.OrdinalIgnoreCase))
        {
            startInfo.ArgumentList.Add("-File");
        }

        startInfo.ArgumentList.Add(script.FullPath);

        if (arguments is { ValueKind: JsonValueKind.Array } json)
        {
            foreach (JsonElement element in json.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.String)
                {
                    throw new InvalidOperationException(
                        "Skill scripts accept positional CLI arguments as a JSON array of strings.");
                }

                startInfo.ArgumentList.Add(element.GetString()!);
            }
        }
        else if (arguments is { } value && value.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
        {
            throw new InvalidOperationException(
                $"Expected a JSON array of CLI arguments but received {value.ValueKind}.");
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(s_scriptTimeout);

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
            {
                return $"Error: Failed to start script '{script.Name}'.";
            }

            Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
            Task<string> errorTask = process.StandardError.ReadToEndAsync();

            try
            {
                await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }

                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);

                if (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }

                return $"Error: Script '{script.Name}' exceeded the {s_scriptTimeout.TotalSeconds:0}-second time limit.";
            }

            string output = await outputTask.ConfigureAwait(false);
            string error = await errorTask.ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(error))
            {
                output += $"\nStderr:\n{error}";
            }

            if (process.ExitCode != 0)
            {
                output += $"\nScript exited with code {process.ExitCode}.";
            }

            return string.IsNullOrWhiteSpace(output) ? "(no output)" : output.Trim();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return $"Error: Failed to execute script '{script.Name}': {ex.Message}";
        }
    }
}
