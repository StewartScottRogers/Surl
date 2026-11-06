namespace Surl.Extended.Protocol.Stomp;

/// <summary>
/// A client did something the server answers with ERROR and then closes the connection
/// (ADR-0118 decision 6: every ERROR is followed by close).
/// </summary>
/// <param name="error">The ERROR frame to write.</param>
/// <param name="reason">What the client did, for the exchange log only.</param>
internal sealed class StompProtocolViolationException(StompFrame error, string reason) : Exception(reason)
{
    /// <summary>Creates the violation answered by ERROR <paramref name="message"/>.</summary>
    /// <param name="message">One of <see cref="StompErrors"/>' texts.</param>
    /// <param name="reason">What the client did, for the exchange log only.</param>
    /// <param name="receipt">The <c>receipt</c> of the frame at fault, or <see langword="null"/>.</param>
    public StompProtocolViolationException(string message, string reason, string? receipt = null)
        : this(StompErrors.Frame(message, receipt), reason)
    {
    }

    /// <summary>The ERROR frame to write.</summary>
    public StompFrame Error { get; } = error;
}
