namespace Surl.Protocol.Abstractions;

/// <summary>
/// A protocol server: answers the exchanges of the schemes it lists. A server implements
/// exactly one of <see cref="IConnectionProtocolServer"/> and
/// <see cref="IDatagramProtocolServer"/> (ADR-0004, section 4).
/// </summary>
public interface IProtocolServer
{
    /// <summary>
    /// Every lower-case scheme the server answers, secure variants included. The list never
    /// changes after construction.
    /// </summary>
    IReadOnlyList<string> Schemes { get; }
}
