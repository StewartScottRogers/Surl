using System.Net;
using Surl.Dtls;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// One established DTLS association seen as a datagram flow of CoAP messages (RFC 7252 section
/// 9.1: CoAP messages are DTLS application data, one message per record): each receive is the
/// plaintext of the next application data record, each send one record. The same
/// <see cref="CoapFlowSession"/> that serves <c>coap</c> serves it (ADR-0097 decision 0).
/// </summary>
/// <param name="transport">The UDP flow the association runs over; it stays the engine's.</param>
/// <param name="session">The established association; it stays the caller's to dispose.</param>
/// <param name="firstMessage">The plaintext of the association's first application data record.</param>
/// <param name="associationEnded">
/// Cancelled when the client ends the association, so the flow's CoAP serving stops; the caller's.
/// </param>
internal sealed class CoapDtlsFlow(
    IDatagramFlow transport, DtlsServerSession session, byte[] firstMessage, CancellationTokenSource associationEnded) : IDatagramFlow
{
    // The message layer sends from several tasks at once (separate responses, notifications), and
    // a record's sequence number is taken as it is sealed, so sends go one at a time.
    private readonly SemaphoreSlim sending = new(1, 1);

    /// <inheritdoc/>
    public EndPoint LocalEndPoint => transport.LocalEndPoint;

    /// <inheritdoc/>
    public EndPoint RemoteEndPoint => transport.RemoteEndPoint;

    /// <inheritdoc/>
    public ReadOnlyMemory<byte> FirstDatagram { get; } = firstMessage;

    /// <inheritdoc/>
    /// <remarks>
    /// Once the client has ended the association - <c>close_notify</c>, answered by the session, or a
    /// fatal alert - it cancels <c>associationEnded</c> and throws <see cref="OperationCanceledException"/>.
    /// </remarks>
    public async ValueTask<ReadOnlyMemory<byte>> ReceiveAsync(CancellationToken cancellationToken)
    {
        if (await session.ReceiveAsync(cancellationToken) is { } message)
        {
            return message;
        }

        await associationEnded.CancelAsync();
        throw new OperationCanceledException("The client ended the DTLS association.", associationEnded.Token);
    }

    /// <inheritdoc/>
    public async ValueTask SendAsync(ReadOnlyMemory<byte> datagram, CancellationToken cancellationToken)
    {
        await sending.WaitAsync(cancellationToken);
        try
        {
            await session.SendAsync(datagram, cancellationToken);
        }
        finally
        {
            sending.Release();
        }
    }

    /// <summary>Never called: a CoAP response leaves from the endpoint the request reached (RFC 7252 section 5.3.2).</summary>
    /// <param name="cancellationToken">Unused.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always.</exception>
    public ValueTask MoveToNewLocalPortAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException("A coaps association stays on its listen port.");

    /// <summary>Releases the send gate; the transport, the session and the source stay their owners'.</summary>
    /// <returns>A completed task.</returns>
    public ValueTask DisposeAsync()
    {
        sending.Dispose();
        return ValueTask.CompletedTask;
    }
}
