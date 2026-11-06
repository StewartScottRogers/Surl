using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Api;

[TestClass]
public sealed class SurlEventFactsTests
{
    private const string SmtpListenUrl = "smtp://127.0.0.1:49731/";
    private static readonly DateTimeOffset Now = new(2026, 11, 6, 9, 0, 0, TimeSpan.Zero);
    private static readonly IPEndPoint Client = new(IPAddress.Loopback, 50001);

    public static IEnumerable<object?[]> EventsAndKinds =>
    [
        [new ExchangeStartedEvent(1, SmtpListenUrl, "smtp", Client), SurlEventKind.ExchangeStarted, 1L],
        [new ExchangeBytesEvent(2, ExchangeDirection.Sent, "x"u8.ToArray()), null, 2L],
        [new ExchangeEndedEvent(3, 0, 0), SurlEventKind.ExchangeEnded, 3L],
        [new LoginJudgedEvent(4, "smtp", "bob", "PLAIN", true, false), SurlEventKind.LoginJudged, 4L],
        [new StoreChangedEvent(StoreKind.Mail, StoreChange.Delivered, "bob/INBOX", 10, 5), SurlEventKind.StoreChanged, 5L],
        [new FaultAppliedEvent("r1", FaultKind.Delay, FaultStage.Accept, 6, "smtp"), SurlEventKind.FaultApplied, 6L],
        [new HookFailedEvent("h1", HookFailure.Threw, "boom"), SurlEventKind.HookFailed, null],
        [new UnknownEvent(), null, null],
    ];

    [TestMethod]
    [DynamicData(nameof(EventsAndKinds))]
    public void KindOfAndExchangeIdOf_EachEventRecord_AreItsKindAndItsExchange(SurlEvent surlEvent, SurlEventKind? kind, long? exchangeId)
    {
        var actualKind = SurlEventFacts.KindOf(surlEvent);
        var actualExchangeId = SurlEventFacts.ExchangeIdOf(surlEvent);

        Assert.AreEqual(kind, actualKind);
        Assert.AreEqual(exchangeId, actualExchangeId);
    }

    [TestMethod]
    [DataRow(null, null, true)]
    [DataRow(0, null, true)]
    [DataRow(1, null, false)]
    [DataRow(null, 1, true)]
    [DataRow(null, 0, false)]
    public void IsWithin_SinceInclusiveAndUntilExclusive_MatchesOnlyInsideTheWindow(int? sinceSeconds, int? untilSeconds, bool isWithin)
    {
        var query = new SurlEventQuery { Since = At(sinceSeconds), Until = At(untilSeconds) };

        var actual = SurlEventFacts.IsWithin(Now, query);

        Assert.AreEqual(isWithin, actual);
    }

    [TestMethod]
    public void Matches_ByKind_MatchesOnlyThatKind()
    {
        var query = new SurlEventQuery { Kind = SurlEventKind.LoginJudged };

        Assert.IsTrue(SurlEventFacts.Matches(Stamped(Login("bob")), null, query));
        Assert.IsFalse(SurlEventFacts.Matches(Stamped(new ExchangeEndedEvent(1, 0, 0)), null, query));
    }

    [TestMethod]
    public void Matches_ByStore_MatchesOnlyAChangeOfThatStore()
    {
        var query = new SurlEventQuery { Store = StoreKind.Mail };

        Assert.IsTrue(SurlEventFacts.Matches(Stamped(MailDelivered(null)), null, query));
        Assert.IsFalse(SurlEventFacts.Matches(Stamped(new StoreChangedEvent(StoreKind.Bus, StoreChange.Published, "t", 1, null)), null, query));
        Assert.IsFalse(SurlEventFacts.Matches(Stamped(Login("bob")), null, query));
    }

    [TestMethod]
    public void Matches_BySchemeAndListenUrl_UsesALoginsOwnSchemeOrElseItsExchanges()
    {
        var exchange = SmtpExchange();
        var bySmtp = new SurlEventQuery { Scheme = "SMTP" };
        var byListenUrl = new SurlEventQuery { ListenUrl = new Uri(SmtpListenUrl) };

        Assert.IsTrue(SurlEventFacts.Matches(Stamped(Login("bob")), null, bySmtp));
        Assert.IsTrue(SurlEventFacts.Matches(Stamped(MailDelivered(1)), exchange, bySmtp));
        Assert.IsFalse(SurlEventFacts.Matches(Stamped(MailDelivered(null)), null, bySmtp));
        Assert.IsTrue(SurlEventFacts.Matches(Stamped(MailDelivered(1)), exchange, byListenUrl));
        Assert.IsFalse(SurlEventFacts.Matches(Stamped(MailDelivered(null)), null, byListenUrl));
    }

    [TestMethod]
    public void Matches_ByAccount_MatchesItsLoginAnAccountChangeOfItAndEveryEventOfAnExchangeItLoggedInTo()
    {
        var exchange = SmtpExchange();
        exchange.Note(Login("bob"));
        var query = new SurlEventQuery { Account = "bob" };

        Assert.IsTrue(SurlEventFacts.Matches(Stamped(Login("bob")), null, query));
        Assert.IsFalse(SurlEventFacts.Matches(Stamped(Login("alice")), exchange, query));
        Assert.IsTrue(SurlEventFacts.Matches(Stamped(new StoreChangedEvent(StoreKind.Accounts, StoreChange.AccountAdded, "bob", null, null)), null, query));
        Assert.IsFalse(SurlEventFacts.Matches(Stamped(new StoreChangedEvent(StoreKind.Accounts, StoreChange.AccountAdded, "carol", null, null)), null, query));
        Assert.IsTrue(SurlEventFacts.Matches(Stamped(MailDelivered(1)), exchange, query));
        Assert.IsFalse(SurlEventFacts.Matches(Stamped(MailDelivered(null)), null, query));
    }

    [TestMethod]
    public void Matches_AfterSequenceAndTime_MatchOnlyLaterEventsInTheWindow()
    {
        var surlEvent = Stamped(Login("bob")) with { Sequence = 5 };

        Assert.IsTrue(SurlEventFacts.Matches(surlEvent, null, new SurlEventQuery { AfterSequence = 4 }));
        Assert.IsFalse(SurlEventFacts.Matches(surlEvent, null, new SurlEventQuery { AfterSequence = 5 }));
        Assert.IsFalse(SurlEventFacts.Matches(surlEvent, null, new SurlEventQuery { Until = Now }));
    }

    internal static ExchangeFacts SmtpExchange() =>
        new(new ExchangeStartedEvent(1, SmtpListenUrl, "smtp", Client) { Sequence = 1, At = Now });

    private static LoginJudgedEvent Login(string account) => new(1, "smtp", account, "PLAIN", true, false);

    private static StoreChangedEvent MailDelivered(long? exchangeId) =>
        new(StoreKind.Mail, StoreChange.Delivered, "bob/INBOX/;UID=1", 10, exchangeId);

    private static SurlEvent Stamped(SurlEvent surlEvent) => surlEvent with { Sequence = 1, At = Now };

    private static DateTimeOffset? At(int? seconds) => seconds is { } offset ? Now.AddSeconds(offset) : null;

    private sealed record UnknownEvent : SurlEvent;
}
