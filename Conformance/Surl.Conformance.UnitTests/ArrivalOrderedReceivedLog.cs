using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Conformance;

/// <summary>
/// One log of what the server halves of an upstream test case received, in the order it arrived,
/// as runtests' single <c>server.input</c> is. Each half keeps its own bytes; this log looks at
/// every half and appends what each one added since the last look. <see cref="Wrap(IConnectionProtocolServer)"/>
/// and <see cref="Wrap(IDatagramProtocolServer)"/> make the look happen before and after every
/// read, write and disposal of a connection or flow, so a half's own log - which holds what it
/// received before its next read or reply - is copied before another half's later bytes.
/// </summary>
internal sealed class ArrivalOrderedReceivedLog(IReadOnlyList<Func<byte[]>> halves)
{
    private readonly object gate = new();
    private readonly List<byte> arrived = [];
    private readonly int[] seen = new int[halves.Count];

    /// <summary>Everything received so far, across the halves, in arrival order.</summary>
    public byte[] Bytes
    {
        get
        {
            Look();
            lock (gate)
            {
                return [.. arrived];
            }
        }
    }

    /// <summary>
    /// Appends what each half received since the last look. Growth in several halves between two
    /// looks keeps the order the halves were given in.
    /// </summary>
    public void Look()
    {
        lock (gate)
        {
            for (var index = 0; index < halves.Count; index++)
            {
                var received = halves[index]();
                if (received.Length > seen[index])
                {
                    arrived.AddRange(received.AsSpan(seen[index]));
                    seen[index] = received.Length;
                }
            }
        }
    }

    /// <summary>A server that serves each connection through a connection that looks at the log around every call.</summary>
    public IConnectionProtocolServer Wrap(IConnectionProtocolServer inner) => new LookingConnectionServer(inner, this);

    /// <summary>A server that serves each flow through a flow that looks at the log around every call.</summary>
    public IDatagramProtocolServer Wrap(IDatagramProtocolServer inner) => new LookingDatagramServer(inner, this);

    private sealed class LookingConnectionServer(IConnectionProtocolServer inner, ArrivalOrderedReceivedLog log) : IConnectionProtocolServer
    {
        public IReadOnlyList<string> Schemes => inner.Schemes;

        public Task ServeAsync(IConnection connection, ExchangeContext context) =>
            inner.ServeAsync(new LookingConnection(connection, log), context);
    }

    private sealed class LookingDatagramServer(IDatagramProtocolServer inner, ArrivalOrderedReceivedLog log) : IDatagramProtocolServer
    {
        public IReadOnlyList<string> Schemes => inner.Schemes;

        public Task ServeAsync(IDatagramFlow flow, ExchangeContext context) =>
            inner.ServeAsync(new LookingDatagramFlow(flow, log), context);
    }

    private sealed class LookingConnection(IConnection inner, ArrivalOrderedReceivedLog log) : IConnection
    {
        public EndPoint LocalEndPoint => inner.LocalEndPoint;

        public EndPoint RemoteEndPoint => inner.RemoteEndPoint;

        public TlsSession? TlsSession => inner.TlsSession;

        public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            log.Look();
            try
            {
                return await inner.ReadAsync(buffer, cancellationToken);
            }
            finally
            {
                log.Look();
            }
        }

        public async ValueTask<byte?> PeekByteAsync(TimeSpan wait, CancellationToken cancellationToken)
        {
            log.Look();
            try
            {
                return await inner.PeekByteAsync(wait, cancellationToken);
            }
            finally
            {
                log.Look();
            }
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
        {
            log.Look();
            return inner.WriteAsync(bytes, cancellationToken);
        }

        public ValueTask CompleteWritesAsync(CancellationToken cancellationToken) => inner.CompleteWritesAsync(cancellationToken);

        public void Abort()
        {
            log.Look();
            inner.Abort();
        }

        public ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken) => inner.UpgradeToTlsAsync(cancellationToken);

        public ValueTask DisposeAsync()
        {
            log.Look();
            return inner.DisposeAsync();
        }
    }

    private sealed class LookingDatagramFlow(IDatagramFlow inner, ArrivalOrderedReceivedLog log) : IDatagramFlow
    {
        public EndPoint LocalEndPoint => inner.LocalEndPoint;

        public EndPoint RemoteEndPoint => inner.RemoteEndPoint;

        public ReadOnlyMemory<byte> FirstDatagram => inner.FirstDatagram;

        public async ValueTask<ReadOnlyMemory<byte>> ReceiveAsync(CancellationToken cancellationToken)
        {
            log.Look();
            try
            {
                return await inner.ReceiveAsync(cancellationToken);
            }
            finally
            {
                log.Look();
            }
        }

        public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, CancellationToken cancellationToken)
        {
            log.Look();
            return inner.SendAsync(datagram, cancellationToken);
        }

        public ValueTask MoveToNewLocalPortAsync(CancellationToken cancellationToken) => inner.MoveToNewLocalPortAsync(cancellationToken);

        public ValueTask DisposeAsync()
        {
            log.Look();
            return inner.DisposeAsync();
        }
    }
}
