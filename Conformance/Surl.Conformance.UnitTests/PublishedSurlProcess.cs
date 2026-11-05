using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Surl.Conformance;

/// <summary>
/// A published <c>surl</c> binary run as a process, for <see cref="SurlOnLoopback"/> when
/// <c>SURL_EXECUTABLE</c> names one (ADR-0085, decision 2): it hands out the first status lines
/// surl writes to its standard output, keeps its standard error as <see cref="Log"/>, and on
/// disposal stops it - SIGTERM and up to 10 seconds' grace on Linux and macOS, then, and at once
/// on Windows, a kill of its process tree - and waits for it to exit.
/// </summary>
internal sealed class PublishedSurlProcess : IAsyncDisposable
{
    /// <summary>The environment variable that holds the full path of a published <c>surl</c>.</summary>
    public const string ExecutableVariable = "SURL_EXECUTABLE";

    private const string StatusLinePrefix = "Listening on ";

    private static readonly TimeSpan TerminateGrace = TimeSpan.FromSeconds(10);

    private readonly Process process;
    private readonly int statusLineCount;
    private readonly List<string> statusLines = [];
    private readonly StringBuilder standardError = new();
    private readonly TaskCompletionSource<string[]> listening = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private PublishedSurlProcess(string executablePath, IReadOnlyList<string> arguments, int statusLineCount)
    {
        this.statusLineCount = statusLineCount;
        var startInfo = new ProcessStartInfo(executablePath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += (_, line) => OnStandardOutputLine(line.Data);
        process.ErrorDataReceived += (_, line) => OnStandardErrorLine(line.Data);
    }

    /// <summary>
    /// Gets the path <c>SURL_EXECUTABLE</c> holds, or <see langword="null"/> when it is unset or
    /// empty, so surl runs in-process.
    /// </summary>
    public static string? ConfiguredExecutablePath =>
        Environment.GetEnvironmentVariable(ExecutableVariable) is { Length: > 0 } path ? path : null;

    /// <summary>
    /// Gets everything surl has written to its standard error so far.
    /// </summary>
    public string Log
    {
        get
        {
            lock (standardError)
            {
                return standardError.ToString();
            }
        }
    }

    /// <summary>
    /// Starts <paramref name="executablePath"/> with <paramref name="arguments"/> and returns it
    /// with its first <paramref name="statusLineCount"/> status lines, once it has written them.
    /// </summary>
    /// <exception cref="FileNotFoundException"><paramref name="executablePath"/> does not exist.</exception>
    /// <exception cref="InvalidOperationException">surl exited before it listened.</exception>
    /// <exception cref="TimeoutException">surl wrote no status line within <paramref name="startTimeout"/>.</exception>
    public static async Task<(PublishedSurlProcess Surl, string[] StatusLines)> StartAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        int statusLineCount,
        TimeSpan startTimeout,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException(
                $"{ExecutableVariable} names {executablePath}, which does not exist.", executablePath);
        }

        var surl = new PublishedSurlProcess(executablePath, arguments, statusLineCount);
        try
        {
            return (surl, await surl.WaitUntilListeningAsync(startTimeout, cancellationToken));
        }
        catch
        {
            await surl.DisposeAsync();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await StopAsync();
        }
        catch (InvalidOperationException)
        {
            // Never started: nothing to stop.
        }
        finally
        {
            process.Dispose();
        }
    }

    private async Task<string[]> WaitUntilListeningAsync(TimeSpan startTimeout, CancellationToken cancellationToken)
    {
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        var exited = process.WaitForExitAsync(cancellationToken);
        if (await Task.WhenAny(listening.Task, exited).WaitAsync(startTimeout, cancellationToken) != listening.Task)
        {
            // The parameterless wait returns once the redirected streams have been read to their end.
            process.WaitForExit();
            throw new InvalidOperationException($"surl exited {process.ExitCode} before it listened: {Log}");
        }

        return await listening.Task;
    }

    private async Task StopAsync()
    {
        if (process.HasExited)
        {
            return;
        }

        if (!OperatingSystem.IsWindows() && await TerminateAsync())
        {
            return;
        }

        process.Kill(entireProcessTree: true);
        await process.WaitForExitAsync(CancellationToken.None);
    }

    // Sends SIGTERM through kill(1), so the test project needs no platform invoke, and reports
    // whether surl exited within the grace period.
    private async Task<bool> TerminateAsync()
    {
        using (var kill = Process.Start("kill", ["-TERM", process.Id.ToString(CultureInfo.InvariantCulture)]))
        {
            if (kill is null)
            {
                return false;
            }

            await kill.WaitForExitAsync(CancellationToken.None);
        }

        try
        {
            await process.WaitForExitAsync(CancellationToken.None).WaitAsync(TerminateGrace);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    private void OnStandardOutputLine(string? line)
    {
        if (line is null || !line.StartsWith(StatusLinePrefix, StringComparison.Ordinal))
        {
            return;
        }

        lock (statusLines)
        {
            statusLines.Add(line.Trim());
            if (statusLines.Count == statusLineCount)
            {
                listening.TrySetResult([.. statusLines]);
            }
        }
    }

    private void OnStandardErrorLine(string? line)
    {
        if (line is null)
        {
            return;
        }

        lock (standardError)
        {
            standardError.AppendLine(line);
        }
    }
}
