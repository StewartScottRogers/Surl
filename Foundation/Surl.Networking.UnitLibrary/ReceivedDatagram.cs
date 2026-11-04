using System.Net;

namespace Surl.Networking;

/// <summary>
/// One datagram as a socket received it: its bytes and the endpoint that sent it.
/// </summary>
/// <param name="Payload">The whole datagram.</param>
/// <param name="RemoteEndPoint">The endpoint that sent it.</param>
internal readonly record struct ReceivedDatagram(ReadOnlyMemory<byte> Payload, EndPoint RemoteEndPoint);
