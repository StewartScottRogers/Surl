using Surl.Cli;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

[TestClass]
public sealed class SchemeLoginCheckTests
{
    public TestContext TestContext { get; set; } = null!;

    private static readonly ProtocolServerRegistration[] Registrations =
    [
        new(["imap"], _ => null!),
        new(["gopher", "gophers"], _ => null!) { MakesLogin = false },
    ];

    [TestMethod]
    public void OriginalRegistrations_MarkExactlyGopherTftpDictAndTelnetAsMakingNoLogin()
    {
        string[] schemes = [.. ProtocolServerRegistrations.Original.Where(r => !r.MakesLogin).SelectMany(r => r.Schemes).Order(StringComparer.Ordinal)];

        CollectionAssert.AreEqual(new[] { "dict", "gopher", "gophers", "telnet", "tftp" }, schemes);
    }

    [TestMethod]
    public void FindRefusal_AMailboxNamingASchemeWithNoLogin_NamesItLowerCased()
    {
        var refusal = SchemeLoginCheck.FindRefusal([["imap"], ["IMAP", "Gopher"]], new SurlCommandLine(), Registrations);

        Assert.AreEqual("(2) scheme gopher has no login", refusal);
    }

    [TestMethod]
    public void FindRefusal_UserLdapNamingASchemeWithNoLogin_IsRefused()
    {
        var commandLine = new SurlCommandLine { UserLdap = new ConfiguredLdapAccountSource("ldap://h/dc=x", null, null, null, 10, ["gophers"], false) };

        Assert.AreEqual("(2) scheme gophers has no login", SchemeLoginCheck.FindRefusal([], commandLine, Registrations));
    }

    [TestMethod]
    public void FindRefusal_LoginSchemesAndUnregisteredSchemesAndNoLists_AreLeftAlone()
    {
        Assert.IsNull(SchemeLoginCheck.FindRefusal([["imap", "nosuch"], null], new SurlCommandLine(), Registrations));
    }

    [TestMethod]
    public async Task ComposeAsync_AMailboxNamingGopher_FailsWithFailedInitBeforeAnyListenerBinds()
    {
        var commandLine = MailboxCompositionTests.WithMail(
            StartTestSeams.Parse("imap://127.0.0.1:0"), ["example.com"], [new ConfiguredMailbox("ann@example.com", "secret", ["gopher"])]);

        var start = await SurlServerComposition.ComposeAsync(commandLine, new StartTestSeams().Seams, TestContext.CancellationToken);

        Assert.IsNull(start.Serving);
        Assert.AreEqual(SurlExitCode.FailedInit, start.Failure!.ExitCode);
        CollectionAssert.AreEqual(new[] { "(2) scheme gopher has no login" }, start.Failure.Lines.ToArray());
    }

    [TestMethod]
    public async Task Start_MailboxFileNamingGopher_IsRefusedWithFileAndLineBeforeListening()
    {
        var commandLine = MailboxCompositionTests.WithMail(
            StartTestSeams.Parse("smtp://127.0.0.1:0"), ["example.com"], [], ["f.csv"]);
        var seams = new StartTestSeams().Seams with
        {
            ReadStartFile = _ => System.Text.Encoding.UTF8.GetBytes("address,password,protocols\nann@example.com,annpass,gopher\n"),
        };

        var start = await SurlServerComposition.ComposeAsync(commandLine, seams, TestContext.CancellationToken);

        Assert.IsNull(start.Serving);
        Assert.AreEqual(SurlExitCode.FailedInit, start.Failure!.ExitCode);
        CollectionAssert.AreEqual(new[] { "(2) Mailbox file f.csv, line 2: scheme gopher has no login" }, start.Failure.Lines.ToArray());
    }

    [TestMethod]
    public void SchemesWithoutLogin_NullRegistrations_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => SchemeLoginCheck.SchemesWithoutLogin(null!));
    }

    [TestMethod]
    public async Task ComposeAsync_AMailboxNamingImapAndFtp_Starts()
    {
        var commandLine = MailboxCompositionTests.WithMail(
            StartTestSeams.Parse("imap://127.0.0.1:0"), ["example.com"], [new ConfiguredMailbox("ann@example.com", "secret", ["imap", "ftp"])]);

        var start = await SurlServerComposition.ComposeAsync(commandLine, new StartTestSeams().Seams, TestContext.CancellationToken);
        await using var serving = start.Serving!;

        Assert.IsNull(start.Failure);
    }
}
