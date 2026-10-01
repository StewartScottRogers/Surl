using System.Buffers.Binary;
using Surl.Protocol.Abstractions;

namespace Surl.Kerberos.TestKdc;

/// <summary>
/// Carries a <see cref="KerberosTestKdc" /> over the transports RFC 4120 section 7.2 names: one
/// request per UDP datagram, answered in one datagram, and one request per TCP connection, both
/// framed by a four-byte big-endian length. The transports are injected (ADR-0065 decision 1), so
/// the fixture never opens a socket; the loopback listeners come from <c>Surl.Networking</c>.
/// </summary>
/// <param name="kdc">The KDC that answers each request.</param>
public sealed class KerberosTestKdcServer(KerberosTestKdc kdc)
{
    /// <summary>
    /// The longest answer sent in one datagram, 4096 bytes, MIT krb5's KDC default; a longer one is
    /// replaced by <see cref="KerberosErrorCode.ResponseTooBig" />, on which the client retries over TCP.
    /// </summary>
    public const int MaximumDatagramAnswerLength = 4096;

    private const int LengthPrefixLength = 4;

    /// <summary>Answers the one request a UDP flow's first datagram carries.</summary>
    /// <param name="flow">The flow; the caller disposes it.</param>
    /// <param name="cancellationToken">Cuts the send off.</param>
    /// <returns>A task that completes once the answer is sent.</returns>
    public async Task ServeDatagramAsync(IDatagramFlow flow, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(flow);
        byte[] answer = kdc.Answer(flow.FirstDatagram.Span);
        if (answer.Length > MaximumDatagramAnswerLength)
        {
            answer = kdc.Refuse(KerberosErrorCode.ResponseTooBig, "response too big for UDP, retry with TCP");
        }

        await flow.SendAsync(answer, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Answers the one length-framed request a TCP connection carries, then half-closes it. A
    /// declared length over <see cref="KerberosTestKdc.MaximumRequestLength" /> is answered
    /// <see cref="KerberosErrorCode.FieldTooLong" /> without reading the request; a connection that
    /// ends before the whole request arrives is left unanswered.
    /// </summary>
    /// <param name="connection">The connection; the caller disposes it.</param>
    /// <param name="cancellationToken">Cuts the reads and writes off.</param>
    /// <returns>A task that completes once the answer is sent, or the connection ended.</returns>
    public async Task ServeConnectionAsync(IConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        byte[] lengthPrefix = new byte[LengthPrefixLength];
        if (!await ReadExactlyAsync(connection, lengthPrefix, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        uint requestLength = BinaryPrimitives.ReadUInt32BigEndian(lengthPrefix);
        byte[]? answer = requestLength > KerberosTestKdc.MaximumRequestLength
            ? kdc.Refuse(KerberosErrorCode.FieldTooLong, "request longer than 64 KiB")
            : await ReadAndAnswerAsync(connection, (int)requestLength, cancellationToken).ConfigureAwait(false);
        if (answer is null)
        {
            return;
        }

        byte[] framedAnswer = new byte[LengthPrefixLength + answer.Length];
        BinaryPrimitives.WriteUInt32BigEndian(framedAnswer, (uint)answer.Length);
        answer.CopyTo(framedAnswer, LengthPrefixLength);
        await connection.WriteAsync(framedAnswer, cancellationToken).ConfigureAwait(false);
        await connection.CompleteWritesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Serves every flow <paramref name="datagramListener" /> opens and every connection
    /// <paramref name="connectionListener" /> accepts, each on its own, disposing each once it is
    /// answered, until <paramref name="cancellationToken" /> is cancelled. A flow or connection that
    /// fails or is cut off is dropped; the listeners carry on.
    /// </summary>
    /// <param name="datagramListener">The UDP listener, such as one bound to <c>127.0.0.1:88</c>.</param>
    /// <param name="connectionListener">The TCP listener, such as one bound to <c>127.0.0.1:88</c>.</param>
    /// <param name="cancellationToken">Stops accepting.</param>
    /// <returns>A task that ends cancelled once <paramref name="cancellationToken" /> is.</returns>
    public Task ServeListenersAsync(IDatagramListener datagramListener, IConnectionListener connectionListener, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(datagramListener);
        ArgumentNullException.ThrowIfNull(connectionListener);
        return Task.WhenAll(AcceptFlowsAsync(datagramListener, cancellationToken), AcceptConnectionsAsync(connectionListener, cancellationToken));
    }

    private static async Task<bool> ReadExactlyAsync(IConnection connection, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        int filled = 0;
        while (filled < buffer.Length)
        {
            int read = await connection.ReadAsync(buffer[filled..], cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return false;
            }

            filled += read;
        }

        return true;
    }

    private static async Task ServeThenDisposeAsync(Task serving, IAsyncDisposable transport)
    {
        // A client that went away or a cancelled serve is dropped, its failure observed so it
        // never surfaces later; the next request is unaffected.
        await Task.WhenAny(serving).ConfigureAwait(false);
        _ = serving.Exception;
        await transport.DisposeAsync().ConfigureAwait(false);
    }

    private async Task<byte[]?> ReadAndAnswerAsync(IConnection connection, int requestLength, CancellationToken cancellationToken)
    {
        byte[] request = new byte[requestLength];
        return await ReadExactlyAsync(connection, request, cancellationToken).ConfigureAwait(false) ? kdc.Answer(request) : null;
    }

    private async Task AcceptFlowsAsync(IDatagramListener listener, CancellationToken cancellationToken)
    {
        while (true)
        {
            IDatagramFlow flow = await listener.AcceptFlowAsync(cancellationToken).ConfigureAwait(false);
            _ = ServeThenDisposeAsync(ServeDatagramAsync(flow, cancellationToken), flow);
        }
    }

    private async Task AcceptConnectionsAsync(IConnectionListener listener, CancellationToken cancellationToken)
    {
        while (true)
        {
            IConnection connection = await listener.AcceptAsync(cancellationToken).ConfigureAwait(false);
            _ = ServeThenDisposeAsync(ServeConnectionAsync(connection, cancellationToken), connection);
        }
    }
}
