using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Api;

/// <summary>
/// Hands each exchange a log that passes every call to <paramref name="first"/>'s log and then to
/// <paramref name="second"/>'s (ADR-0099, decision 7): the exchange log the options ask for, and
/// the exchange record beside it.
/// </summary>
/// <param name="first">The factory called first.</param>
/// <param name="second">The factory called second.</param>
internal sealed class FanOutExchangeLogFactory(IExchangeLogFactory first, IExchangeLogFactory second) : IExchangeLogFactory
{
    /// <inheritdoc/>
    public IExchangeLog Create(long exchangeId, EndPoint remoteEndPoint) =>
        new FanOutExchangeLog(first.Create(exchangeId, remoteEndPoint), second.Create(exchangeId, remoteEndPoint));

    /// <inheritdoc/>
    public void NoteOutsideExchange(string text)
    {
        first.NoteOutsideExchange(text);
        second.NoteOutsideExchange(text);
    }

    private sealed class FanOutExchangeLog(IExchangeLog first, IExchangeLog second) : IExchangeLog
    {
        public void BytesReceived(ReadOnlySpan<byte> bytes)
        {
            first.BytesReceived(bytes);
            second.BytesReceived(bytes);
        }

        public void BytesSent(ReadOnlySpan<byte> bytes)
        {
            first.BytesSent(bytes);
            second.BytesSent(bytes);
        }

        public void Note(string text)
        {
            first.Note(text);
            second.Note(text);
        }
    }
}
