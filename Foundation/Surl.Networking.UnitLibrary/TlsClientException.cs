namespace Surl.Networking;

/// <summary>
/// A secured client connection could not be made. The message names the host, port and reason only,
/// never anything the caller sent over the connection.
/// </summary>
public sealed class TlsClientException : IOException
{
    /// <summary>
    /// Creates the exception.
    /// </summary>
    /// <param name="failure">Why the connection failed.</param>
    /// <param name="message">What failed, for the operator.</param>
    /// <param name="innerException">The underlying failure, if any.</param>
    public TlsClientException(TlsClientFailure failure, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Failure = failure;
    }

    /// <summary>
    /// Why the connection failed.
    /// </summary>
    public TlsClientFailure Failure { get; }
}
