namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>Padding the response on the encrypted transports (RFC 7830, RFC 8467 section 4.1).</summary>
[TestClass]
public sealed class DnsResponseWriterTests
{
    [TestMethod]
    public void WritePaddedWithin_NullResponse_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => DnsResponseWriter.WritePaddedWithin(null!, 65535));

    [TestMethod]
    public void WritePaddedWithin_QueryWithPadding_AnswersAMultipleOf468WithTheOptLast()
    {
        var response = Answer("web.example.test", DnsTestQueries.PaddedOpt(1232));

        var written = DnsResponseWriter.WritePaddedWithin(response, 65535);

        Assert.AreEqual(0, written.Length % 468);
        var message = DnsTestQueries.ReadWhole(written);
        Assert.AreEqual(DnsRecordType.OPT, message.Additionals[^1].Type);
    }

    [TestMethod]
    public void WritePaddedWithin_PaddedLengthPastTheLimit_KeepsAnEmptyPaddingOption()
    {
        var response = Answer("web.example.test", DnsTestQueries.PaddedOpt(1232));
        var unpadded = DnsResponseWriter.WriteWithin(response, 65535).Length;

        var written = DnsResponseWriter.WritePaddedWithin(response, unpadded + 8);

        var option = DnsOptRecord.FromResourceRecord(DnsTestQueries.ReadWhole(written).Additionals[^1]).Options.Single();
        Assert.AreEqual(DnsOption.PaddingCode, option.Code);
        Assert.AreEqual(0, option.Data.Length);
    }

    [TestMethod]
    public void WritePaddedWithin_QueryWithoutPaddingOrWithoutAnOpt_IsWriteWithin()
    {
        foreach (var opt in new[] { DnsTestQueries.Opt(1232), null })
        {
            var response = Answer("web.example.test", opt);

            CollectionAssert.AreEqual(DnsResponseWriter.WriteWithin(response, 65535), DnsResponseWriter.WritePaddedWithin(response, 65535));
        }
    }

    [TestMethod]
    public void WritePaddedWithin_ResponseOptWithNoQueryOpt_IsWriteWithin()
    {
        var response = Answer("web.example.test", DnsTestQueries.PaddedOpt(1232)) with { QueryOpt = null };

        CollectionAssert.AreEqual(DnsResponseWriter.WriteWithin(response, 65535), DnsResponseWriter.WritePaddedWithin(response, 65535));
    }

    [TestMethod]
    public void WritePadded_FramesThePaddedMessage()
    {
        var response = Answer("web.example.test", DnsTestQueries.PaddedOpt(1232));

        var frame = DnsTcpResponseWriter.WritePadded(response);

        Assert.AreEqual(0, (frame.Length - 2) % 468);
    }

    private static DnsResponse Answer(string name, byte[]? opt)
    {
        var query = DnsTestQueries.Query(name, 1, additionals: opt is null ? [] : [opt]);
        return new DnsQueryAnswerer(DnsTestZones.Load(("example.test.zone", DnsTestZones.Example))).Answer(query)!;
    }
}
