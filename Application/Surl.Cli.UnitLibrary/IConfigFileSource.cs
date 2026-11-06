namespace Surl.Cli;

/// <summary>
/// Reads the files <c>-K</c>/<c>--config</c> names, for <see cref="CommandLineParser"/> (ADR-0101
/// decision 1): <c>Surl.Cli</c> never touches the disk or the console, so the host supplies this
/// seam - <c>surl</c> reads the disk and standard input, a test reads text held in memory.
/// </summary>
public interface IConfigFileSource
{
    /// <summary>
    /// Reads the bytes of the config file <paramref name="path"/>, or of standard input when
    /// <paramref name="path"/> is <c>-</c>, stopping after <paramref name="maxByteCount"/> bytes.
    /// </summary>
    /// <param name="path">The path as given to <c>-K</c> or a <c>config</c> line, or <c>-</c>.</param>
    /// <param name="maxByteCount">The most bytes to read; a longer file is cut there.</param>
    /// <returns>The bytes read, or <see langword="null"/> when the file is missing, a directory or unreadable.</returns>
    byte[]? Read(string path, int maxByteCount);

    /// <summary>
    /// Names the file <paramref name="path"/> opens, so a file that includes a file already being read
    /// is refused as a cycle: its full path, in the case the platform's file system compares names by.
    /// </summary>
    /// <param name="path">The path as given; never <c>-</c>.</param>
    /// <returns>The name two paths of the same file share, compared ordinally.</returns>
    string IdentityOf(string path);
}
