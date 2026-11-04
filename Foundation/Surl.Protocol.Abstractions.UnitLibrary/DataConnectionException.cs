namespace Surl.Protocol.Abstractions;

/// <summary>
/// Thrown by <see cref="IDataConnectionOpener"/> and <see cref="IPassiveDataListener"/> when an
/// FTP data connection cannot be opened (ADR-0052, decision 9).
/// </summary>
/// <param name="failure">Why the data connection could not be opened.</param>
/// <param name="message">What happened, for the verbose log.</param>
public sealed class DataConnectionException(DataConnectionFailure failure, string message)
    : Exception(message)
{
    /// <summary>
    /// Why the data connection could not be opened.
    /// </summary>
    public DataConnectionFailure Failure { get; } = failure;
}
