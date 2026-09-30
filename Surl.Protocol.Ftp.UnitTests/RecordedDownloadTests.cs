using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Ftp.FtpTestExchange;

namespace Surl.Protocol.Ftp;

/// <summary>
/// Replays each download recorded from pinned upstream curl (Fixtures/README.md) with an
/// in-memory data connection standing in for curl's, and asserts surl answers every control
/// line with the reply the recorder fed curl, and sends curl the file's bytes from the
/// <c>REST</c> offset on the data connection.
/// </summary>
/// <remarks>
/// The passive port is the one the recorder announced, scripted on the fake listener, so the
/// <c>229</c> and <c>227</c> replies match byte for byte. An active case hands out a fake
/// connection for the address curl named in <c>EPRT</c> or <c>PORT</c>.
/// </remarks>
[TestClass]
public sealed class RecordedDownloadTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("download", 0, DisplayName = "EPSV, TYPE I, SIZE, RETR")]
    [DataRow("range-0-9", 0, DisplayName = "-r 0-9: RETR, then ABOR after curl closed the data connection")]
    [DataRow("continue-at-auto", 0, DisplayName = "-C - to stdout: no REST")]
    [DataRow("continue-at-5", 5, DisplayName = "-C 5: REST 5, RETR")]
    [DataRow("disable-epsv", 0, DisplayName = "--disable-epsv: PASV")]
    public async Task Replay_PassiveDownload_SendsTheRecordedRepliesAndTheFile(string caseName, int restartOffset)
    {
        var dataConnection = new InMemoryConnection([]);
        var dataConnections = new InMemoryDataConnections()
            .ScriptPassiveListener(new IPEndPoint(IPAddress.Loopback, AnnouncedPassivePort(caseName)), dataConnection);

        var control = await ReplayAsync(caseName, dataConnections);

        Assert.AreEqual(RecordedFixture.ReadServerReplies(caseName), Text(control.WrittenBytes));
        AssertTheFileWasSentFrom(restartOffset, dataConnection, caseName);
        Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 80), dataConnections.PassiveRequests.Single().ControlLocal);
        Assert.IsTrue(dataConnections.PassiveListeners.Single().Disposed);
        Assert.IsEmpty(dataConnections.ActiveRequests);
    }

    [TestMethod]
    [DataRow("active-eprt", DisplayName = "-P -: EPRT")]
    [DataRow("active-port", DisplayName = "-P - --disable-eprt: PORT")]
    public async Task Replay_ActiveDownload_DialsTheAddressCurlNamedAndSendsTheFile(string caseName)
    {
        var dataConnection = new InMemoryConnection([]);
        var dataConnections = new InMemoryDataConnections().ScriptActiveConnection(dataConnection);

        var control = await ReplayAsync(caseName, dataConnections);

        Assert.AreEqual(RecordedFixture.ReadServerReplies(caseName), Text(control.WrittenBytes));
        AssertTheFileWasSentFrom(0, dataConnection, caseName);
        var request = dataConnections.ActiveRequests.Single();
        Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, AnnouncedActivePort(caseName)), request.Target);
        Assert.AreEqual(control.RemoteEndPoint, request.ControlRemote);
        Assert.AreEqual(ExchangeLimits.Default.HeadTimeout, request.Timeout);
        Assert.IsEmpty(dataConnections.PassiveRequests);
    }

    [TestMethod]
    public async Task Replay_Head_AnswersMdtmSizeAndRestWithNoDataConnection()
    {
        var dataConnections = new InMemoryDataConnections();

        var control = await ReplayAsync("head", dataConnections);

        Assert.AreEqual(RecordedFixture.ReadServerReplies("head"), Text(control.WrittenBytes));
        Assert.AreEqual("0", Text(RecordedFixture.ReadBytes("head", "exitcode.txt")));
        StringAssert.Contains(Text(RecordedFixture.ReadBytes("head", "stdout.bin")), "Last-Modified: Sun, 27 Sep 2026 12:34:56 GMT");
        Assert.IsEmpty(dataConnections.PassiveRequests);
        Assert.IsEmpty(dataConnections.ActiveRequests);
    }

    private async Task<InMemoryConnection> ReplayAsync(string caseName, InMemoryDataConnections dataConnections)
    {
        var control = new InMemoryConnection([RecordedFixture.ReadRequestBytes(caseName)]);
        var context = Context(new ManualTimeProvider(), TestContext.CancellationToken, dataConnections: dataConnections);

        await Server().ServeAsync(control, context);

        Assert.IsTrue(control.WritesCompleted);
        return control;
    }

    // surl sends the file from the offset and completes the data connection; what curl wrote
    // out is those bytes, or their start when curl asked for a range and closed early.
    private static void AssertTheFileWasSentFrom(int restartOffset, InMemoryConnection dataConnection, string caseName)
    {
        var sent = Text(dataConnection.WrittenBytes);
        Assert.AreEqual(FileText[restartOffset..], sent);
        Assert.IsTrue(dataConnection.WritesCompleted);
        Assert.IsTrue(dataConnection.Disposed);
        Assert.StartsWith(Text(RecordedFixture.ReadBytes(caseName, "stdout.bin")), sent);
        Assert.AreEqual("0", Text(RecordedFixture.ReadBytes(caseName, "exitcode.txt")));
    }

    private static int AnnouncedPassivePort(string caseName)
    {
        var transcript = Encoding.ASCII.GetString(RecordedFixture.ReadBytes(caseName, "transcript.txt"));
        var extended = Regex.Match(transcript, @"^< 229 .*\(\|\|\|(\d+)\|\)", RegexOptions.Multiline);
        if (extended.Success)
        {
            return int.Parse(extended.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        var passive = Regex.Match(transcript, @"^< 227 .*,(\d+),(\d+)\)", RegexOptions.Multiline);
        return (int.Parse(passive.Groups[1].Value, CultureInfo.InvariantCulture) * 256) + int.Parse(passive.Groups[2].Value, CultureInfo.InvariantCulture);
    }

    private static int AnnouncedActivePort(string caseName)
    {
        var transcript = Encoding.ASCII.GetString(RecordedFixture.ReadBytes(caseName, "transcript.txt"));
        var extended = Regex.Match(transcript, @"^> EPRT \|1\|127\.0\.0\.1\|(\d+)\|", RegexOptions.Multiline);
        if (extended.Success)
        {
            return int.Parse(extended.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        var port = Regex.Match(transcript, @"^> PORT 127,0,0,1,(\d+),(\d+)", RegexOptions.Multiline);
        return (int.Parse(port.Groups[1].Value, CultureInfo.InvariantCulture) * 256) + int.Parse(port.Groups[2].Value, CultureInfo.InvariantCulture);
    }
}
