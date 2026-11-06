using Surl.LineProtocol;
using Surl.Protocol.Ftp;
using Surl.Protocol.Gopher;
using Surl.Protocol.Http;
using Surl.Protocol.Mqtt;
using Surl.Protocol.Tftp;

namespace Surl.Conformance;

[TestClass]
public sealed class UpstreamTestCaseServerCommandsTests
{
    [TestMethod]
    [DataRow("http", "auth_required", true)]
    [DataRow("http-ipv6", "idle", true)]
    [DataRow("https", "skip:", true)]
    [DataRow("https-mtls", "writedelay:", true)]
    [DataRow("http", "upgrade", false)]
    [DataRow("http", "skip", false)]
    [DataRow("http", "postcmd", false)]
    [DataRow("ftp", "REPLY", true)]
    [DataRow("ftp-ipv6", "COUNT", true)]
    [DataRow("ftps", "RETRSIZE", true)]
    [DataRow("ftp", "DELAY", false)]
    [DataRow("ftp", "idle", false)]
    [DataRow("smtp", "REPLY", true)]
    [DataRow("imap", "POSTFETCH", true)]
    [DataRow("pop3", "CAPA", true)]
    [DataRow("pop3", "SLOWDOWN", false)]
    [DataRow("imaps", "REPLY", false)]
    [DataRow("rtsp", "idle", false)]
    [DataRow("tftp", "writedelay:", true)]
    [DataRow("tftp", "Testnum", true)]
    [DataRow("tftp", "REPLY", false)]
    [DataRow("mqtt", "error-CONNACK", false)]
    [DataRow("mqtts", "remlen-CONNACK", false)]
    [DataRow("gopher", "Testnum", false)]
    [DataRow("dict", "Testnum", false)]
    [DataRow("telnet", "Testnum", false)]
    [DataRow("scp", "Testnum", false)]
    [DataRow("sftp", "REPLY", false)]
    [DataRow("smb", "Testnum", false)]
    public void IsSupported_TheServerHalfsTable_SaysWhichCommandsItImplements(string server, string command, bool expected)
    {
        var supported = UpstreamTestCaseServerCommands.IsSupported(server, command);

        Assert.AreEqual(expected, supported);
    }

    [TestMethod]
    public void Http_IsTheCommandsTheHttpServerHalfAccepts()
    {
        var table = UpstreamTestCaseServerCommands.Http.Order(StringComparer.Ordinal).ToArray();

        CollectionAssert.AreEqual(HttpUpstreamTestCaseReplies.SupportedServerCommands.Order(StringComparer.Ordinal).ToArray(), table);
    }

    [TestMethod]
    public void Ftp_IsTheCommandsTheFtpServerHalfAccepts()
    {
        var table = UpstreamTestCaseServerCommands.Ftp.Order(StringComparer.Ordinal).ToArray();

        CollectionAssert.AreEqual(FtpUpstreamTestCaseReplies.SupportedServerCommands.Order(StringComparer.Ordinal).ToArray(), table);
    }

    [TestMethod]
    public void Tftp_IsTheCommandsTheTftpServerHalfAccepts()
    {
        var table = UpstreamTestCaseServerCommands.Tftp.Order(StringComparer.Ordinal).ToArray();

        CollectionAssert.AreEqual(TftpUpstreamTestCaseReplies.SupportedServerCommands.Order(StringComparer.Ordinal).ToArray(), table);
    }

    [TestMethod]
    public void None_IsTheCommandsTheGopherAndMqttServerHalvesAccept()
    {
        Assert.IsEmpty(UpstreamTestCaseServerCommands.None);
        Assert.IsEmpty(GopherUpstreamTestCaseReplies.SupportedServerCommands);
        Assert.IsEmpty(MqttUpstreamTestCaseReplies.SupportedServerCommands);
    }

    [TestMethod]
    public void Mail_IsTheCommandsTheMailServerHalvesAccept()
    {
        var table = UpstreamTestCaseServerCommands.Mail.Order(StringComparer.Ordinal).ToArray();

        CollectionAssert.AreEqual(MailUpstreamTestCaseReplies.SupportedServerCommands.Order(StringComparer.Ordinal).ToArray(), table);
    }

    [TestMethod]
    public void IsSupported_NullArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamTestCaseServerCommands.IsSupported(null!, "idle"));
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamTestCaseServerCommands.IsSupported("http", null!));
    }
}
