namespace Surl.Protocol.Abstractions;

/// <summary>
/// Thrown by <see cref="IOriginConnector"/> when a proxy's connection to an origin cannot be
/// opened (ADR-0088, decision 2).
/// </summary>
/// <param name="failure">Why the connection could not be opened.</param>
/// <param name="message">What happened, for the verbose log.</param>
public sealed class OriginConnectionException(OriginConnectionFailure failure, string message)
    : Exception(message)
{
    /// <summary>
    /// Why the connection could not be opened.
    /// </summary>
    public OriginConnectionFailure Failure { get; } = failure;
}
