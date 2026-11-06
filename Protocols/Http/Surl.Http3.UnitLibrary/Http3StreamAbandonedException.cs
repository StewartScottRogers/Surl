namespace Surl.Http3;

/// <summary>
/// A client stream ended without its <c>FIN</c>: the client reset it, or
/// the connection ended. Nothing is answered on it; the stream's work just stops.
/// </summary>
/// <param name="streamId">The stream.</param>
internal sealed class Http3StreamAbandonedException(ulong streamId)
    : Exception($"Stream {streamId} was reset by the client, or its connection ended, before it was read to its end.");
