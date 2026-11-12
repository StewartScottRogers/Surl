namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// How a Long Polling <c>GET</c> is answered (TransportProtocols.md "Long Polling").
/// </summary>
/// <param name="Status"><c>200</c> with the waiting records, or without any when the poll timed out; <c>204</c> when the poll was superseded or the connection ended.</param>
/// <param name="Body">The waiting records, each ended by its separator; empty unless there are any.</param>
internal sealed record SignalRPollResult(int Status, byte[] Body);
