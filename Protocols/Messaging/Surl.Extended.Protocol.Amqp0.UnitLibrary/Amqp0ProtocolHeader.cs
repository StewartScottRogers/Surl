using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// The protocol header a client opens with (AMQP 0-9-1 section 4.2.2; ADR-0112 decision 1):
/// <c>A M Q P 0x00 0x00 0x09 0x01</c>.
/// </summary>
public static class Amqp0ProtocolHeader
{
    /// <summary>The length of a protocol header: 8 bytes.</summary>
    public const int Length = 8;

    private static readonly byte[] SupportedBytes = "AMQP\0\0\u0009\u0001"u8.ToArray();

    /// <summary>The only header accepted, and the one a refused client is answered with.</summary>
    public static ReadOnlyMemory<byte> Supported => SupportedBytes;

    /// <summary>
    /// Reads the client's first 8 bytes before writing anything. The supported header is
    /// accepted. Any other 8 bytes are answered with <see cref="Supported"/> and the write side is
    /// completed, as section 4.2.2 says, so the caller only has to close the connection. A client
    /// that closes before 8 bytes is answered with nothing.
    /// </summary>
    /// <param name="connection">The accepted connection.</param>
    /// <param name="cancellationToken">Cuts the read off (<c>--head-timeout</c>).</param>
    /// <returns>Whether the header was accepted, refused, or never arrived whole.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> cut the read off.</exception>
    public static async ValueTask<Amqp0ProtocolHeaderOutcome> ReadAsync(IConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var header = new byte[Length];
        var filled = 0;
        while (filled < Length)
        {
            var read = await connection.ReadAsync(header.AsMemory(filled), cancellationToken);
            if (read == 0)
            {
                return Amqp0ProtocolHeaderOutcome.ConnectionClosedEarly;
            }

            filled += read;
        }

        if (header.AsSpan().SequenceEqual(SupportedBytes))
        {
            return Amqp0ProtocolHeaderOutcome.Accepted;
        }

        await connection.WriteAsync(SupportedBytes, cancellationToken);
        await connection.CompleteWritesAsync(cancellationToken);
        return Amqp0ProtocolHeaderOutcome.Refused;
    }
}
