using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Api;

// BL-1481: an IMAP session with INBOX selected is told of mail delivered meanwhile, by SMTP and by Mail.DeliverAsync,
// as "* n EXISTS" ahead of the tagged answer to its next NOOP (RFC 9051, 7.3.1). The tests wait on the delivery's
// StoreChangedEvent, never on time.
public sealed partial class SurlServerTests
{
    private static readonly SurlEventQuery MailChanges = new() { Kind = SurlEventKind.StoreChanged, Store = StoreKind.Mail };

    private static SurlServerOptions SmtpAndImapRecordingOptions()
    {
        var options = SmtpAndImapOptions();
        options.RecordExchanges = true;
        return options;
    }

    private async Task<string> ImapSendAsync(LiveFakeConnection imap, string tag, string command)
    {
        var from = imap.WrittenToClient.Length;
        imap.Send($"{tag} {command}\r\n");
        return await imap.WaitForOutputAsync(tag + " OK", from, ExchangeTimeLimit, TestContext.CancellationToken);
    }

    private async Task<LiveFakeConnection> ImapSelectedInboxAsync(ApiTestSeams seams)
    {
        var imap = new LiveFakeConnection();
        seams.ListenerFactory.Accept("imap", imap);
        await ImapSendAsync(imap, "a1", "LOGIN alice secret");
        var selected = await ImapSendAsync(imap, "a2", "SELECT INBOX");
        StringAssert.Contains(selected, "* 0 EXISTS");
        return imap;
    }

    [TestMethod]
    public async Task OpenImapSession_ThenSmtpMail_IsToldExistsOnItsNextNoopAndFetchesTheMessage()
    {
        var seams = new ApiTestSeams();
        await using var server = await SurlServer.StartAsync(SmtpAndImapRecordingOptions(), seams.Host, TestContext.CancellationToken);
        var imap = await ImapSelectedInboxAsync(seams);

        await ServeAsync(seams, "smtp", SmtpSendTo("alice@example.com"));
        await server.WaitForEventAsync(MailChanges, ExchangeTimeLimit, TestContext.CancellationToken);
        var noop = await ImapSendAsync(imap, "a3", "NOOP");
        var fetch = await ImapSendAsync(imap, "a4", "FETCH 1 BODY[]");
        imap.Close();

        StringAssert.Contains(noop, "* 1 EXISTS");
        StringAssert.Contains(fetch, "hello bob");
    }

    [TestMethod]
    public async Task OpenImapSession_ThenMailDeliverAsync_IsToldExistsOnItsNextNoopAndAgainForASecondDelivery()
    {
        var seams = new ApiTestSeams();
        await using var server = await SurlServer.StartAsync(SmtpAndImapRecordingOptions(), seams.Host, TestContext.CancellationToken);
        var imap = await ImapSelectedInboxAsync(seams);

        await server.Mail.DeliverAsync("alice@example.com", Encoding.ASCII.GetBytes(AddedAccountMail), TestContext.CancellationToken);
        var first = await server.WaitForEventAsync(MailChanges, ExchangeTimeLimit, TestContext.CancellationToken);
        var firstNoop = await ImapSendAsync(imap, "a3", "NOOP");
        await server.Mail.DeliverAsync("alice@example.com", Encoding.ASCII.GetBytes(AddedAccountMail), TestContext.CancellationToken);
        await server.WaitForEventAsync(MailChanges with { AfterSequence = first.Sequence }, ExchangeTimeLimit, TestContext.CancellationToken);
        var secondNoop = await ImapSendAsync(imap, "a4", "NOOP");
        imap.Close();

        StringAssert.Contains(firstNoop, "* 1 EXISTS");
        StringAssert.Contains(secondNoop, "* 2 EXISTS");
    }
}
