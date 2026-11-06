using System.Reflection;
using Surl.Cli;
using Surl.Protocol.Abstractions;

namespace Surl.Mcp;

[TestClass]
public sealed class CommandLinePathMembersTests
{
    [TestMethod]
    public void Table_EverySurlCommandLineMember_IsClassifiedExactlyOnce()
    {
        var members = typeof(SurlCommandLine).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var pathMembers = CommandLinePathMembers.PathMembers.Select(member => member.MemberName).ToArray();

        var classified = pathMembers.Concat(CommandLinePathMembers.MembersWithoutPaths).Order(StringComparer.Ordinal).ToArray();

        CollectionAssert.AreEqual(members, classified, "Classify each SurlCommandLine member as a path member or not.");
    }

    [TestMethod]
    public void PathsOf_EveryPathOptionGiven_ReadsEachPathWithItsOption()
    {
        var commandLine = new SurlCommandLine
        {
            ListenUrls =
            [
                new ListenUrl("http", "127.0.0.1", 0),
                new ListenUrl("http", "localhost", 80) { UnixSocket = new UnixSocketAddress("surl.sock", isAbstract: false) },
                new ListenUrl("http", "localhost", 80) { UnixSocket = new UnixSocketAddress("abstract", isAbstract: true) },
            ],
            DataDirectory = "data",
            TraceFile = "trace.txt",
            LogFile = "log.txt",
            CertificateFile = "cert.pem",
            KeyFile = "key.pem",
            CaCertificateFile = "ca.pem",
            UserFile = "users.txt",
            KeytabFile = "surl.keytab",
            HostKeyFiles = ["host_ed25519", "host_rsa"],
            HostCertificateFiles = ["host-cert.pub"],
            AuthorizedKeys = [new CommandLineAuthorizedKeys("alice", "alice.pub")],
            MailboxFiles = ["mailboxes.csv"],
            ControlTokenFile = "control.token",
        };

        var paths = CommandLinePathMembers.PathsOf(commandLine).ToArray();

        CollectionAssert.AreEqual(
            new[]
            {
                ("a Unix domain socket listen URL", "surl.sock"),
                ("--directory", "data"),
                ("--trace", "trace.txt"),
                ("--log-file", "log.txt"),
                ("--cert", "cert.pem"),
                ("--key", "key.pem"),
                ("--cacert", "ca.pem"),
                ("--user-file", "users.txt"),
                ("--keytab", "surl.keytab"),
                ("--hostkey", "host_ed25519"),
                ("--hostkey", "host_rsa"),
                ("--hostcert", "host-cert.pub"),
                ("--authorized-keys", "alice.pub"),
                ("--mailbox-file", "mailboxes.csv"),
                ("--control-token-file", "control.token"),
            },
            paths);
    }

    [TestMethod]
    public void PathsOf_NoPathOption_ReadsNone() =>
        Assert.IsEmpty(CommandLinePathMembers.PathsOf(new SurlCommandLine { ListenUrls = [new ListenUrl("http", "127.0.0.1", 0)] }));
}
