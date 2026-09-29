namespace Surl.Protocol.Telnet;

/// <summary>
/// What a negotiation from the client did to its own side of an option
/// (<see cref="TelnetOptionNegotiator.Answer"/>).
/// </summary>
internal enum TelnetClientOptionChange
{
    /// <summary>Nothing changed on the client's side, or the negotiation was about the server's side.</summary>
    None,

    /// <summary>The client now performs the option.</summary>
    TurnedOn,

    /// <summary>The client performed, or was asked to perform, the option, and now does not.</summary>
    TurnedOff,
}
