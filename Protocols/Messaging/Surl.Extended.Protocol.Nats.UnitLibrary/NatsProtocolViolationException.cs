namespace Surl.Extended.Protocol.Nats;

/// <summary>
/// A client did something the server answers with <c>-ERR</c> and then closes the connection
/// (ADR-0113 decision 7, "then close").
/// </summary>
/// <param name="errorText">The text the <c>-ERR</c> line carries, one of <see cref="NatsErrors"/>.</param>
/// <param name="reason">What the client did, for the exchange log only.</param>
internal sealed class NatsProtocolViolationException(string errorText, string reason) : Exception(reason)
{
    /// <summary>The text the <c>-ERR</c> line carries.</summary>
    public string ErrorText { get; } = errorText;
}
