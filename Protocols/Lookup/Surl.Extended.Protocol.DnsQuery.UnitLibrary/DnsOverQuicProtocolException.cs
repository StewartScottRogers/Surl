namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// A rule of RFC 9250 a client broke on one stream, which ends the whole connection with
/// <see cref="DnsOverQuicErrorCode.ProtocolError"/> (sections 4.2 and 4.3.3).
/// </summary>
/// <param name="message">What the client did, for the exchange log.</param>
internal sealed class DnsOverQuicProtocolException(string message) : Exception(message);
