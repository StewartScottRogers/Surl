namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>Builds the messages the codec tests write and read.</summary>
internal static class DnsTestMessages
{
    public static DnsHeader QueryHeader { get; } =
        new(0x1234, false, DnsOpcode.Query, false, false, true, false, 0, DnsResponseCode.NoError);

    public static DnsHeader ResponseHeader { get; } =
        new(0x1234, true, DnsOpcode.Query, true, false, true, false, 0, DnsResponseCode.NoError);

    public static DnsMessage Query(string name, DnsRecordType type, params DnsResourceRecord[] additionals) =>
        new(QueryHeader, [new DnsQuestion(DnsName.Parse(name), type, DnsClasses.Internet)], [], [], additionals);

    public static DnsMessage Response(string name, DnsRecordType type, params DnsResourceRecord[] answers) =>
        new(ResponseHeader, [new DnsQuestion(DnsName.Parse(name), type, DnsClasses.Internet)], answers, [], []);

    public static DnsResourceRecord Record(string owner, DnsRecordData data, uint timeToLive = 3600) =>
        new(DnsName.Parse(owner), DnsClasses.Internet, timeToLive, data);

    /// <summary>
    /// Writes <paramref name="message"/>, reads it back, checks the read message writes to the
    /// same bytes, and returns it.
    /// </summary>
    public static DnsMessage RoundTrip(DnsMessage message)
    {
        var bytes = DnsMessageWriter.Write(message);
        var outcome = DnsMessageReader.Read(bytes);

        Assert.AreEqual(DnsReadFault.None, outcome.Fault);
        Assert.IsNotNull(outcome.Message);
        CollectionAssert.AreEqual(bytes, DnsMessageWriter.Write(outcome.Message));
        return outcome.Message;
    }

    /// <summary>Returns the RDATA of the only answer of the round-tripped <paramref name="message"/>.</summary>
    public static T RoundTripAnswerData<T>(DnsMessage message)
        where T : DnsRecordData
    {
        var read = RoundTrip(message);
        Assert.HasCount(1, read.Answers);
        return (T)read.Answers[0].Data;
    }

    public static byte[] Bytes(params int[] values) => values.Select(value => (byte)value).ToArray();

    /// <summary>A header with ID 0x1234, RD set, and the four counts given.</summary>
    public static byte[] HeaderBytes(int questions, int answers = 0, int authorities = 0, int additionals = 0) =>
        Bytes(0x12, 0x34, 0x01, 0x00, 0, questions, 0, answers, 0, authorities, 0, additionals);
}
