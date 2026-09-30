using Surl.Protocol.Abstractions;
using static Surl.LineProtocol.Wire;

namespace Surl.LineProtocol;

[TestClass]
public sealed class SaslContinuationLineTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ReadSaslContinuationAsync_Base64Line_IsDecoded()
    {
        using var reader = Reader(Connection("AHVzZXIAcGFzcw==\r\n"));

        var result = await reader.ReadSaslContinuationAsync(TestContext.CancellationToken);

        Assert.AreEqual(SaslContinuationOutcome.ResponseRead, result.Outcome);
        Assert.AreEqual("\0user\0pass", Text(result.Response));
    }

    [TestMethod]
    public async Task ReadSaslContinuationAsync_EmptyLine_IsAnEmptyResponse()
    {
        using var reader = Reader(Connection("\r\n"));

        var result = await reader.ReadSaslContinuationAsync(TestContext.CancellationToken);

        Assert.AreEqual(SaslContinuationOutcome.ResponseRead, result.Outcome);
        Assert.IsEmpty(result.Response!);
    }

    [TestMethod]
    public async Task ReadSaslContinuationAsync_StarAlone_IsCancelled()
    {
        using var reader = Reader(Connection("*\r\n"));

        var result = await reader.ReadSaslContinuationAsync(TestContext.CancellationToken);

        Assert.AreEqual(SaslContinuationReadResult.NoResponse(SaslContinuationOutcome.Cancelled), result);
    }

    [TestMethod]
    [DataRow("not base64!", DisplayName = "Not base64 characters")]
    [DataRow("AHVz ", DisplayName = "A trailing space")]
    [DataRow("AH Vz", DisplayName = "An inner space")]
    [DataRow("AH\tVz", DisplayName = "A tab")]
    [DataRow("AH\rVz", DisplayName = "A bare CR")]
    [DataRow("AH\nVz", DisplayName = "A bare LF")]
    [DataRow("AHV", DisplayName = "Bad length")]
    [DataRow("**", DisplayName = "Two stars")]
    [DataRow("é", DisplayName = "Non-ASCII")]
    public void Classify_TextThatIsNotBase64_IsNotBase64(string line)
    {
        var result = SaslContinuationLine.Classify(Bytes(line));

        Assert.AreEqual(SaslContinuationReadResult.NoResponse(SaslContinuationOutcome.NotBase64), result);
    }

    [TestMethod]
    public async Task ReadSaslContinuationAsync_PeerCloses_IsClosed()
    {
        using var reader = Reader(Connection("AHV"));

        Assert.AreEqual(SaslContinuationOutcome.Closed, (await reader.ReadSaslContinuationAsync(TestContext.CancellationToken)).Outcome);
    }

    [TestMethod]
    public async Task ReadSaslContinuationAsync_LineTooLong_IsLineTooLong()
    {
        using var reader = Reader(Connection("AHVzZXIA\r\n"), ExchangeLimits.Default with { MaxLineBytes = 4 });

        Assert.AreEqual(SaslContinuationOutcome.LineTooLong, (await reader.ReadSaslContinuationAsync(TestContext.CancellationToken)).Outcome);
    }

    [TestMethod]
    public async Task ReadSaslContinuationAsync_HeadTimeout_IsHeadTimedOut()
    {
        var clock = new ManualTimeProvider();
        using var reader = Reader(OpenConnection("AHV"), clock: clock);

        var reading = reader.ReadSaslContinuationAsync(TestContext.CancellationToken).AsTask();
        clock.Advance(ExchangeLimits.Default.HeadTimeout);

        Assert.AreEqual(SaslContinuationOutcome.HeadTimedOut, (await reading).Outcome);
    }
}
