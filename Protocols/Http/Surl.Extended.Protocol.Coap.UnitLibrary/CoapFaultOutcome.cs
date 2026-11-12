namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// What the fault plan decided for one request (<see cref="CoapRequestFaults.ApplyAsync"/>): answer
/// it as usual, answer it with a chosen error response, or answer it with nothing at all.
/// </summary>
/// <param name="ChosenError">The chosen error response, or <see langword="null"/> for none.</param>
/// <param name="IsDropped">Whether a <c>drop</c> rule ended the request unanswered: no datagram is sent for it.</param>
internal readonly record struct CoapFaultOutcome(CoapResponse? ChosenError, bool IsDropped)
{
    /// <summary>The request is answered as usual.</summary>
    public static CoapFaultOutcome AnsweredAsUsual { get; } = new(null, false);

    /// <summary>The request is answered with nothing.</summary>
    public static CoapFaultOutcome Dropped { get; } = new(null, true);
}
