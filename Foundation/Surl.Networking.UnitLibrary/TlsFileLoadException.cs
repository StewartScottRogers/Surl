namespace Surl.Networking;

/// <summary>
/// Thrown at startup when a <c>--cert</c>, <c>--key</c> or <c>--cacert</c> file cannot be
/// loaded (ADR-0010, section 3). It carries a <see cref="TlsFileLoadFailure"/> for the
/// composition root to map to an exit code; the message names the file, for the verbose log.
/// </summary>
public sealed class TlsFileLoadException : Exception
{
    /// <summary>
    /// Creates the exception.
    /// </summary>
    /// <param name="failure">Why the file could not be loaded.</param>
    /// <param name="message">What went wrong, naming the file.</param>
    /// <param name="innerException">The exception behind it, or <see langword="null"/>.</param>
    public TlsFileLoadException(TlsFileLoadFailure failure, string message, Exception? innerException)
        : base(message, innerException)
    {
        Failure = failure;
    }

    /// <summary>
    /// Why the file could not be loaded.
    /// </summary>
    public TlsFileLoadFailure Failure { get; }
}
