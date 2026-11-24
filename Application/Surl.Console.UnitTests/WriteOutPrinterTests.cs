using System.Net;
using Surl.Core;
using Surl.Protocol.Abstractions;

namespace Surl.Console;

/// <summary>
/// <see cref="WriteOutPrinter"/> over a real event stream and a clock the test moves (BL-1663): one
/// formatted line per exchange that both began and ended, from the client's side as curl names it.
/// </summary>
[TestClass]
public sealed class WriteOutPrinterTests
{
    private const string Format = "#%{exchange_id} %{scheme} %{listen_url} %{remote_ip}:%{remote_port} down=%{size_download} up=%{size_upload} t=%{time_total}|%{nope}\n";

    private static readonly IPEndPoint Client = new(IPAddress.Parse("10.1.2.3"), 4567);

    [TestMethod]
    public async Task Dispose_ExchangeBeganAndEnded_PrintsTheFormatOnceAndWarnsOfTheUnknownVariable()
    {
        var clock = new MovableTimeProvider();
        var events = new SurlEventStream(clock);
        var output = new StringWriter();
        var error = new StringWriter();
        var printer = new WriteOutPrinter(events, Format, output, error);

        events.Publish(new ExchangeStartedEvent(3, "http://127.0.0.1:80/", "http", Client));
        clock.Advance(TimeSpan.FromMilliseconds(250));
        events.Publish(new ExchangeEndedEvent(3, 11, 22));
        await printer.DisposeAsync();

        Assert.AreEqual("#3 http http://127.0.0.1:80/ 10.1.2.3:4567 down=22 up=11 t=0.250000|\n", output.ToString());
        Assert.AreEqual("surl: unknown --write-out variable: 'nope'" + Environment.NewLine, error.ToString());
    }

    [TestMethod]
    public async Task Dispose_ClientWithNoIpAddress_PrintsAnEmptyAddressAndPortZero()
    {
        var events = new SurlEventStream(TimeProvider.System);
        var output = new StringWriter();
        var printer = new WriteOutPrinter(events, "%{remote_ip}:%{remote_port}", output, new StringWriter());

        events.Publish(new ExchangeStartedEvent(1, "http://unix/", "http", new DnsEndPoint("peer", 9)));
        events.Publish(new ExchangeEndedEvent(1, 0, 0));
        await printer.DisposeAsync();

        Assert.AreEqual(":0", output.ToString());
    }

    [TestMethod]
    public async Task Dispose_EventsWithNoExchangeEndWithoutStartOrOtherKinds_PrintNothing()
    {
        var events = new SurlEventStream(TimeProvider.System);
        var output = new StringWriter();
        var printer = new WriteOutPrinter(events, "x", output, new StringWriter());

        events.Publish(new ExchangeStartedEvent(null, "http://a/", "http", Client));
        events.Publish(new ExchangeEndedEvent(null, 1, 1));
        events.Publish(new ExchangeEndedEvent(9, 1, 1));
        events.Publish(new UnrelatedEvent());
        await printer.DisposeAsync();

        Assert.AreEqual(string.Empty, output.ToString());
    }

    [TestMethod]
    public async Task Dispose_TwoExchanges_PrintsEachAtItsOwnEnd()
    {
        var events = new SurlEventStream(TimeProvider.System);
        var output = new StringWriter();
        var printer = new WriteOutPrinter(events, "%{exchange_id};", output, new StringWriter());

        events.Publish(new ExchangeStartedEvent(1, "http://a/", "http", Client));
        events.Publish(new ExchangeStartedEvent(2, "http://a/", "http", Client));
        events.Publish(new ExchangeEndedEvent(2, 0, 0));
        events.Publish(new ExchangeEndedEvent(1, 0, 0));
        await printer.DisposeAsync();

        Assert.AreEqual("2;1;", output.ToString());
    }

    private sealed record UnrelatedEvent : SurlEvent;

    private sealed class MovableTimeProvider : TimeProvider
    {
        private DateTimeOffset now = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);

        public void Advance(TimeSpan by) => now += by;

        public override DateTimeOffset GetUtcNow() => now;
    }
}
