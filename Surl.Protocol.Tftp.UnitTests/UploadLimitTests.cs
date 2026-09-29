using Surl.Protocol.Abstractions;
using static Surl.Protocol.Tftp.UploadExchange;

namespace Surl.Protocol.Tftp;

[TestClass]
public sealed class UploadLimitTests
{
    [TestMethod]
    public async Task UploadOverMaxUploadBytes_AnswersError3AndLeavesNoFile()
    {
        var fileSystem = FileSystem();
        var (flow, log) = await ReplayAsync(Store(fileSystem, maxUploadBytes: 512), "write-accepted-no-options");

        var accepted = RecordedFixture.ServerDatagrams("write-accepted-no-options");
        AssertSent([accepted[0], accepted[1], RecordedFixture.ServerDatagrams("write-too-large").Single()], flow);
        Assert.IsNull(fileSystem.ReadFile(UploadPath));
        Assert.AreEqual(
            "Write of \"up.txt\" (octet): the upload grew past the limit of 512 bytes in block 2; the partial file was deleted and the transfer ended with ERROR 3.",
            log.Notes[^1]);
    }

    [TestMethod]
    public async Task UploadOfExactlyMaxUploadBytes_IsWritten()
    {
        var fileSystem = FileSystem();
        var (flow, _) = await ReplayAsync(Store(fileSystem, maxUploadBytes: 604), "write-accepted-no-options");

        AssertSent(RecordedFixture.ServerDatagrams("write-accepted-no-options"), flow);
        Assert.HasCount(604, fileSystem.ReadFile(UploadPath)!);
    }

    [TestMethod]
    public async Task TsizeOverMaxUploadBytes_AnswersError3BeforeAnyData()
    {
        var fileSystem = FileSystem();
        var (flow, log) = await ReplayAsync(Store(fileSystem), "write-too-large", ExchangeLimits.Default with { MaxUploadBytes = 603 });

        AssertSent(RecordedFixture.ServerDatagrams("write-too-large"), flow);
        Assert.AreEqual(ScriptedDatagramFlow.TransferPort, flow.Sent[0].FromPort);
        Assert.IsNull(fileSystem.ReadFile(UploadPath));
        Assert.AreEqual(
            "Write of \"up.txt\" (octet): the announced 604-byte upload is past the upload limit of 603 bytes; answered with ERROR 3.",
            log.Notes.Single());
    }

    [TestMethod]
    [DataRow(604L)]
    [DataRow(0L)]
    public async Task TsizeWithinMaxUploadBytesOrNoLimit_IsAccepted(long maxUploadBytes)
    {
        var (flow, _) = await ReplayAsync(Store(FileSystem()), "write-accepted", ExchangeLimits.Default with { MaxUploadBytes = maxUploadBytes });

        AssertSent(RecordedFixture.ServerDatagrams("write-accepted"), flow);
    }
}
