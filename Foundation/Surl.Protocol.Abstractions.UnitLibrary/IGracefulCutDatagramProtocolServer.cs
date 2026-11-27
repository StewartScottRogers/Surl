namespace Surl.Protocol.Abstractions;

/// <summary>
/// A datagram protocol server whose connections have a graceful close of their own, which is HTTP/3 over
/// QUIC. For a <see cref="FaultKind.Drop"/> rule with <see cref="FaultRule.AfterBytes"/> and
/// <see cref="FaultClose.Graceful"/>, the serving engine hands its <see cref="IDatagramProtocolServer.ServeAsync"/>
/// an <see cref="IGracefullyCutDatagramFlow"/>, and the server closes the connection with no error once that
/// flow is cut (ADR-0188, amendment 5). A datagram server that does not implement this interface only stops
/// being heard after the count.
/// </summary>
public interface IGracefulCutDatagramProtocolServer : IDatagramProtocolServer;
