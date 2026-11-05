namespace Surl.Protocol.Abstractions;

/// <summary>
/// A connection listener and a datagram listener bound to the same addresses and the same port
/// for one listen URL: an <c>https</c> listen URL serving HTTP/3 over QUIC on UDP beside HTTP/1.x
/// and HTTP/2 on TCP (ADR-0082, decisions 5 and 6).
/// </summary>
/// <param name="ConnectionListener">The connection listener; its listen URL carries the shared port.</param>
/// <param name="DatagramListener">The datagram listener, bound to the same port.</param>
public sealed record SharedPortListeners(IConnectionListener ConnectionListener, IDatagramListener DatagramListener);
