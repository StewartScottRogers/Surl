using System.Text;

namespace Surl.Api;

// BL-1473: mail to an account added while the server runs reaches its INBOX, by SMTP and by Mail.DeliverAsync;
// mail to one removed again is accepted at RCPT TO and discarded (ADR-0185, decision 1; ADR-0102, no enumeration).
public sealed partial class SurlServerTests
{
    private const string AddedAccountMail = "Subject: for bob\r\n\r\nhello bob\r\n";

    private static string SmtpSendTo(string recipient) =>
        $"EHLO client.test\r\nAUTH PLAIN {Convert.ToBase64String("\0alice\0secret"u8)}\r\nMAIL FROM:<sender@example.com>\r\nRCPT TO:<{recipient}>\r\nDATA\r\n{AddedAccountMail}.\r\nQUIT\r\n";

    private static SurlServerOptions SmtpAndImapOptions()
    {
        var options = StoreOptions("smtp://127.0.0.1:0", "imap://127.0.0.1:0");
        options.MailDomains.Add("example.com");
        return options;
    }

    [TestMethod]
    public async Task AccountsAdd_ThenSmtpMailToIt_IsReadFromItsInboxOverImap()
    {
        var seams = new ApiTestSeams();
        await using var server = await SurlServer.StartAsync(SmtpAndImapOptions(), seams.Host, TestContext.CancellationToken);
        server.Accounts.Add("bob", "builder");

        var sent = await ServeAsync(seams, "smtp", SmtpSendTo("bob@example.com"));
        var read = await ServeAsync(seams, "imap", "a1 LOGIN bob builder\r\na2 SELECT INBOX\r\na3 FETCH 1 BODY[]\r\na4 LOGOUT\r\n");

        StringAssert.Contains(sent, "250 2.0.0");
        StringAssert.Contains(read, "1 EXISTS");
        StringAssert.Contains(read, "hello bob");
    }

    [TestMethod]
    public async Task AccountsAdd_ThenMailDeliverAsyncToIt_IsReadBackFromItsInbox()
    {
        var seams = new ApiTestSeams();
        await using var server = await SurlServer.StartAsync(SmtpAndImapOptions(), seams.Host, TestContext.CancellationToken);
        server.Accounts.Add("bob", "builder");

        await server.Mail.DeliverAsync("bob@example.com", Encoding.ASCII.GetBytes(AddedAccountMail), TestContext.CancellationToken);
        var messages = await server.Mail.ListMessagesAsync("bob", "INBOX", cancellationToken: TestContext.CancellationToken);
        var message = await server.Mail.ReadMessageAsync("bob", "INBOX", messages.Messages.Single().Uid, TestContext.CancellationToken);

        Assert.AreEqual(1, messages.Messages.Count);
        StringAssert.Contains(Encoding.ASCII.GetString(message.Bytes.Span), "hello bob");
    }

    [TestMethod]
    public async Task AccountsRemove_ThenSmtpMailToIt_IsAcceptedAtRcptToAndDiscarded()
    {
        var seams = new ApiTestSeams();
        await using var server = await SurlServer.StartAsync(SmtpAndImapOptions(), seams.Host, TestContext.CancellationToken);
        server.Accounts.Add("bob", "builder");
        server.Accounts.Remove("bob");

        var sent = await ServeAsync(seams, "smtp", SmtpSendTo("bob@example.com"));
        var delivery = await Assert.ThrowsExactlyAsync<SurlSeedRefusedException>(
            () => server.Mail.DeliverAsync("bob@example.com", Encoding.ASCII.GetBytes(AddedAccountMail), TestContext.CancellationToken));

        StringAssert.Contains(sent, "250 2.1.5");
        Assert.IsNotNull(delivery);
    }

    [TestMethod]
    public async Task SmtpMailOutsideEveryMailDomain_IsRefusedAtRcptTo()
    {
        var seams = new ApiTestSeams();
        await using var server = await SurlServer.StartAsync(SmtpAndImapOptions(), seams.Host, TestContext.CancellationToken);

        var sent = await ServeAsync(seams, "smtp", SmtpSendTo("bob@elsewhere.test"));

        StringAssert.Contains(sent, "550 5.7.1 Relaying denied");
    }
}
