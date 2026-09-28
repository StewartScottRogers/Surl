using Surl.Protocol.Abstractions;

namespace Surl.Console;

/// <summary>
/// Entry point of the <c>surl</c> executable.
/// </summary>
internal static class Program
{
    /// <summary>
    /// Runs Surl with the command line it was given. Phase 0 placeholder: it serves nothing,
    /// writes <c>surl: not implemented yet</c> to standard error and reports that Surl
    /// could not start.
    /// </summary>
    /// <param name="args">The command-line arguments, without the program name.</param>
    /// <returns><see cref="SurlExitCode.FailedInit"/>, for every command line.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="args"/> is null.</exception>
    internal static Task<int> Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        // Phase 1 wires the option parser, the listeners and every protocol server here,
        // with explicit dependency injection. Until then the executable exists so the
        // solution has a buildable, publishable target and the reference graph is real.
        System.Console.Error.WriteLine("surl: not implemented yet");

        return Task.FromResult((int)SurlExitCode.FailedInit);
    }
}
