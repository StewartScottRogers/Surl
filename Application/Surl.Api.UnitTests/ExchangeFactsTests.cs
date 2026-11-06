using Surl.Protocol.Abstractions;

namespace Surl.Api;

[TestClass]
public sealed class ExchangeFactsTests
{
    [TestMethod]
    public void Matches_AnEmptyQuery_MatchesEveryExchange()
    {
        var exchange = SurlEventFactsTests.SmtpExchange();

        var matches = exchange.Matches(new SurlEventQuery());

        Assert.IsTrue(matches);
    }

    [TestMethod]
    public void Matches_BySchemeListenUrlSequenceAndStart_MatchesOnlyWhatItStartedWith()
    {
        var exchange = SurlEventFactsTests.SmtpExchange();
        var startedAt = exchange.Started.At;

        Assert.IsTrue(exchange.Matches(new SurlEventQuery { Scheme = "Smtp", ListenUrl = new Uri("smtp://127.0.0.1:49731/") }));
        Assert.IsFalse(exchange.Matches(new SurlEventQuery { Scheme = "imap" }));
        Assert.IsFalse(exchange.Matches(new SurlEventQuery { ListenUrl = new Uri("smtp://127.0.0.1:25/") }));
        Assert.IsFalse(exchange.Matches(new SurlEventQuery { AfterSequence = 1 }));
        Assert.IsTrue(exchange.Matches(new SurlEventQuery { Since = startedAt, Until = startedAt.AddTicks(1) }));
        Assert.IsFalse(exchange.Matches(new SurlEventQuery { Since = startedAt.AddTicks(1) }));
    }

    [TestMethod]
    public void Matches_ByAccountKindAndStore_MatchesOnceAnEventOfTheExchangeSaysSo()
    {
        var exchange = SurlEventFactsTests.SmtpExchange();
        var byAccount = new SurlEventQuery { Account = "bob" };
        var byKind = new SurlEventQuery { Kind = SurlEventKind.StoreChanged };
        var byStore = new SurlEventQuery { Store = StoreKind.Mail };
        Assert.IsFalse(exchange.Matches(byAccount));
        Assert.IsFalse(exchange.Matches(byKind));
        Assert.IsFalse(exchange.Matches(byStore));

        exchange.Note(new LoginJudgedEvent(1, "smtp", "bob", "PLAIN", false, false));
        exchange.Note(new StoreChangedEvent(StoreKind.Mail, StoreChange.Delivered, "bob/INBOX/;UID=1", 10, 1));
        exchange.Note(new UnkeptEvent());

        Assert.IsTrue(exchange.Matches(byAccount));
        Assert.IsTrue(exchange.Matches(byKind));
        Assert.IsTrue(exchange.Matches(byStore));
        Assert.IsTrue(exchange.Matches(new SurlEventQuery { Kind = SurlEventKind.ExchangeStarted }));
        Assert.IsFalse(exchange.Matches(new SurlEventQuery { Account = "alice" }));
        Assert.IsFalse(exchange.Matches(new SurlEventQuery { Store = StoreKind.Bus }));
    }

    private sealed record UnkeptEvent : SurlEvent;
}
