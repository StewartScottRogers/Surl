namespace Surl.Protocol.Abstractions;

/// <summary>
/// A datagram protocol server whose connections have a graceful close of their own, which is QUIC's: HTTP/3,
/// DNS over QUIC and WebTransport. For a <see cref="FaultKind.Drop"/> rule with
/// <see cref="FaultRule.AfterBytes"/> and <see cref="FaultClose.Graceful"/> on a listen URL
/// <see cref="ClosesCutFlowsGracefully"/> answers <see langword="true"/> for, the serving engine hands its
/// <see cref="IDatagramProtocolServer.ServeAsync"/> an <see cref="IGracefullyCutDatagramFlow"/>, and the server
/// closes the connection with no error once that flow is cut (ADR-0188, amendments 5 and 6). A datagram server
/// that does not implement this interface only stops being heard after the count.
/// </summary>
public interface IGracefulCutDatagramProtocolServer : IDatagramProtocolServer
{
    /// <summary>
    /// Whether the flows of <paramref name="listenUrl"/> carry connections this server can close gracefully. A
    /// server whose every flow does keeps this default, <see langword="true"/>; one that also serves a datagram
    /// protocol with no close, such as <c>dns</c> over plain UDP beside DNS over QUIC, answers
    /// <see langword="false"/> for that listen URL, whose cut flows then only stop being heard.
    /// </summary>
    /// <param name="listenUrl">The listen URL the flow was accepted on.</param>
    /// <returns>Whether a cut flow of that listen URL is closed gracefully.</returns>
    bool ClosesCutFlowsGracefully(ListenUrl listenUrl) => true;
}
