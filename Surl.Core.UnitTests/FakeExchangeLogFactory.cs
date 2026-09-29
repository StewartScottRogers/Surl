using System.Collections.Concurrent;
using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Core;

/// <summary>
/// An <see cref="IExchangeLogFactory"/> that hands out one <see cref="RecordingExchangeLog"/>
/// per exchange and keeps each by its exchange ID.
/// </summary>
internal sealed class FakeExchangeLogFactory : IExchangeLogFactory
{
    private readonly ConcurrentDictionary<long, (RecordingExchangeLog Log, EndPoint RemoteEndPoint)> logs = new();

    public RecordingExchangeLog LogOf(long exchangeId) => logs[exchangeId].Log;

    public EndPoint RemoteEndPointOf(long exchangeId) => logs[exchangeId].RemoteEndPoint;

    /// <summary>
    /// When set, <see cref="Create"/> throws it, as a log whose output is gone would.
    /// </summary>
    public Exception? CreateFailure { get; set; }

    public IExchangeLog Create(long exchangeId, EndPoint remoteEndPoint)
    {
        if (CreateFailure is not null)
        {
            throw CreateFailure;
        }

        var log = new RecordingExchangeLog();

        logs[exchangeId] = (log, remoteEndPoint);

        return log;
    }
}

