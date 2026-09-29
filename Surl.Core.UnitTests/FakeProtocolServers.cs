using System.Threading.Channels;
using Surl.Protocol.Abstractions;

namespace Surl.Core;

/// <summary>
/// What a <see cref="FakeConnectionProtocolServer"/> was given for one exchange.
/// </summary>
internal sealed record ServedExchange(IConnection Connection, ExchangeContext Context);

/// <summary>
/// An <see cref="IConnectionProtocolServer"/> that records every exchange it is given and
/// answers it with the behaviour a test scripted.
/// </summary>
internal sealed class FakeConnectionProtocolServer(
    Func<IConnection, ExchangeContext, Task> serve, params string[] schemes) : IConnectionProtocolServer
{
    private readonly Channel<ServedExchange> exchanges = Channel.CreateUnbounded<ServedExchange>();

    public FakeConnectionProtocolServer(params string[] schemes)
        : this((_, _) => Task.CompletedTask, schemes)
    {
    }

    public IReadOnlyList<string> Schemes { get; } = schemes;

    /// <summary>
    /// Waits for the next exchange the server was given.
    /// </summary>
    public Task<ServedExchange> NextExchangeAsync() =>
        exchanges.Reader.ReadAsync().AsTask().WaitAsync(Patience.Timeout);

    public bool TryTakeExchange(out ServedExchange? exchange) => exchanges.Reader.TryRead(out exchange);

    public async Task ServeAsync(IConnection connection, ExchangeContext context)
    {
        exchanges.Writer.TryWrite(new ServedExchange(connection, context));

        await serve(connection, context);
    }
}

/// <summary>
/// An <see cref="IDatagramProtocolServer"/> that is never given a flow.
/// </summary>
internal sealed class FakeDatagramProtocolServer(params string[] schemes) : IDatagramProtocolServer
{
    public IReadOnlyList<string> Schemes { get; } = schemes;

    public Task ServeAsync(IDatagramFlow flow, ExchangeContext context) =>
        throw new NotSupportedException("BL-015's engine never serves a datagram flow.");
}
