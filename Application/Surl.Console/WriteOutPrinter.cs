using System.Net;
using Surl.Cli;
using Surl.Core;
using Surl.Protocol.Abstractions;

namespace Surl.Console;

/// <summary>
/// Prints the <c>-w</c> / <c>--write-out</c> format once per exchange (BL-1663): one subscriber of the
/// instance's event stream that remembers each <see cref="ExchangeStartedEvent"/> and, at its
/// <see cref="ExchangeEndedEvent"/>, expands the format and writes it to stdout. An unknown variable is
/// reported on the error stream, as upstream curl does. Disposing it prints what was already buffered.
/// </summary>
internal sealed class WriteOutPrinter : IAsyncDisposable
{
    private readonly string format;
    private readonly TextWriter output;
    private readonly TextWriter error;
    private readonly Dictionary<long, ExchangeStartedEvent> started = [];
    private readonly SurlEventSubscription subscription;
    private readonly Task pump;

    /// <summary>Subscribes to <paramref name="events"/> and starts printing.</summary>
    /// <param name="events">The instance's event stream.</param>
    /// <param name="format">The text given to <c>-w</c>.</param>
    /// <param name="output">stdout.</param>
    /// <param name="error">Where the unknown-variable warnings go.</param>
    public WriteOutPrinter(SurlEventStream events, string format, TextWriter output, TextWriter error)
    {
        this.format = format;
        this.output = output;
        this.error = error;
        subscription = events.Subscribe(new SurlEventSubscriptionOptions
        {
            Filter = static surlEvent => surlEvent is ExchangeStartedEvent or ExchangeEndedEvent,
        });
        pump = Task.Run(PumpAsync);
    }

    /// <summary>Stops the subscription and waits until every buffered exchange has been printed.</summary>
    /// <returns>A task that completes once the pump has stopped.</returns>
    public async ValueTask DisposeAsync()
    {
        await subscription.DisposeAsync();
        await pump;
    }

    private async Task PumpAsync()
    {
        await foreach (var surlEvent in subscription.ReadAllAsync(CancellationToken.None))
        {
            Apply(surlEvent);
        }
    }

    private void Apply(SurlEvent surlEvent)
    {
        if (surlEvent is ExchangeStartedEvent begun && begun.ExchangeId is { } id)
        {
            started[id] = begun;
        }
        else if (surlEvent is ExchangeEndedEvent ended && ended.ExchangeId is { } endedId
            && started.Remove(endedId, out var start))
        {
            Print(start, ended);
        }
    }


    private void Print(ExchangeStartedEvent start, ExchangeEndedEvent ended)
    {
        var id = start.ExchangeId!.Value;
        var (address, port) = start.Client is IPEndPoint ip ? (ip.Address.ToString(), ip.Port) : (string.Empty, 0);
        var result = WriteOutFormat.Expand(
            format,
            new WriteOutValues(id, start.Scheme!, start.ListenUrl, address, port, ended.SentByteCount, ended.ReceivedByteCount, ended.At - start.At));
        output.Write(result.Text);
        output.Flush();
        foreach (var name in result.UnknownVariables)
        {
            error.WriteLine($"surl: unknown --write-out variable: '{name}'");
        }
    }
}
