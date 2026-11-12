namespace Surl.HttpServing;

/// <summary>
/// Writes one piece of a <see cref="HttpAnswerBody.Streamed"/> body to the connection as it is produced; the
/// HTTP version's writer frames it (an HTTP/1.1 chunk, RFC 9112 section 7.1). An empty piece writes nothing.
/// </summary>
/// <param name="piece">The bytes to send now.</param>
/// <param name="cancellationToken">Cancels the write.</param>
/// <returns>A task that completes once the piece has been handed to the connection.</returns>
public delegate ValueTask HttpAnswerChunkWriter(ReadOnlyMemory<byte> piece, CancellationToken cancellationToken);
