using System.Text;
using Surl.Cli;
using Surl.Control;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// The control endpoint's bearer token as the command line asks for it (ADR-0124 decision 7): with
/// <c>--control</c>, the first line of the <c>--control-token-file</c>, read through the start-file seam after
/// every other start file and before the data-directory lock. Without <c>--control</c> no file is read.
/// A refusal names the file as given and never holds the token.
/// </summary>
internal static class ControlComposition
{
    /// <summary>
    /// Reads and checks the control token.
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <param name="readFile">Reads a file's bytes, given its path as given.</param>
    /// <returns>
    /// The token, or <see langword="null"/> without <c>--control</c>; or, when the file cannot be used, the exit code
    /// (37 unreadable, 2 outside 32 to 1024 visible ASCII bytes) and the message after the <c>surl: </c> prefix.
    /// </returns>
    public static (ControlToken? Token, SurlExitCode ExitCode, string? FailureMessage) ReadToken(
        SurlCommandLine commandLine, Func<string, byte[]> readFile)
    {
        if (commandLine.ControlListenUrl is null || commandLine.ControlTokenFile is not { } path)
        {
            return (null, SurlExitCode.Ok, null);
        }

        byte[] content;
        try
        {
            content = readFile(path);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            return (null, SurlExitCode.CouldNotReadFile, $"(37) Could not read control token file {path}");
        }

        try
        {
            return (new ControlToken(FirstLine(content)), SurlExitCode.Ok, null);
        }
        catch (ArgumentException)
        {
            return (
                null,
                SurlExitCode.FailedInit,
                $"(2) Control token file {path} must hold {ControlToken.MinLength} to {ControlToken.MaxLength} visible ASCII bytes on its first line");
        }
    }

    // Latin-1, so a byte past ASCII reaches ControlToken as one character and is refused there.
    private static string FirstLine(byte[] content)
    {
        var text = Encoding.Latin1.GetString(content);
        var lineEnd = text.IndexOf('\n', StringComparison.Ordinal);
        var line = lineEnd < 0 ? text : text[..lineEnd];
        return line.EndsWith('\r') ? line[..^1] : line;
    }
}
