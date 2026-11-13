using System.Net;
using System.Text;
using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.DnsQuery.DnsTestQueries;

namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// The DNS server half of upstream's test cases over the datagram seam (ADR-0123), from query
/// bytes written by hand from RFC 1035 section 4.1 and answers checked against RFC 1035
/// section 3.4.1 and RFC 3596 section 2.2.
/// </summary>
[TestClass]
public sealed class DnsUpstreamTestCaseServerTests
{
    private const ushort TypeA = 1;
    private const ushort TypeAaaa = 28;

    [TestMethod]
    public void Schemes_IsDns() =>
        CollectionAssert.AreEqual(new[] { "dns" }, new DnsUpstreamTestCaseServer(new DnsQueryUpstreamTestCaseReplies([])).Schemes.ToArray());

    [TestMethod]
    public async Task ServeAsync_NullArguments_Throw()
    {
        var server = new DnsUpstreamTestCaseServer(new DnsQueryUpstreamTestCaseReplies([]));
        await using var flow = new InMemoryDatagramFlow(Query("a.test", TypeA));

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(null!, Context(default)));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(flow, null!));
    }

    [TestMethod]
    public async Task ServeAsync_AQuery_AnswersTheSectionsAddressAsRfc1035Writes()
    {
        var replies = new DnsQueryUpstreamTestCaseReplies(["A: 127.0.0.1", "AAAA: [::1]"]);

        var sent = (await ServeAsync(replies, Query("examplehost.example", TypeA, id: 0x0A0B))).Single();

        // ID, flags QR+AA+RD, QDCOUNT 1, ANCOUNT 1, then the question echoed.
        CollectionAssert.AreEqual(new byte[] { 0x0A, 0x0B, 0x85, 0x00, 0x00, 0x01, 0x00, 0x01 }, sent[..8]);
        CollectionAssert.AreEqual(Query("examplehost.example", TypeA)[12..], sent[12..(12 + Query("examplehost.example", TypeA).Length - 12)]);
        // Answer: pointer to the question name, TYPE A, CLASS IN, TTL 2580, RDLENGTH 4, 127.0.0.1.
        byte[] answer = [0xC0, 0x0C, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x0A, 0x14, 0x00, 0x04, 127, 0, 0, 1];
        CollectionAssert.AreEqual(answer, sent[^16..]);
    }

    [TestMethod]
    public async Task ServeAsync_AnAaaaQuery_AnswersTheSectionsIPv6AddressAsRfc3596Writes()
    {
        var replies = new DnsQueryUpstreamTestCaseReplies(["A: 127.0.0.1", "AAAA: [::1]"]);

        var sent = (await ServeAsync(replies, Query("examplehost.example", TypeAaaa))).Single();

        byte[] answer = [0xC0, 0x0C, 0x00, 0x1C, 0x00, 0x01, 0x00, 0x00, 0x0A, 0x14, 0x00, 0x10, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1];
        CollectionAssert.AreEqual(answer, sent[^28..]);
        Assert.AreEqual((byte)1, sent[7]);
    }

    [TestMethod]
    public async Task ServeAsync_NoneAddresses_AnswerNoErrorWithNoAnswers()
    {
        var replies = new DnsQueryUpstreamTestCaseReplies(["A: none", "AAAA: none"]);

        var sent = (await ServeAsync(replies, Query("examplehost.example", TypeA))).Single();

        Assert.AreEqual(0, sent[3] & 0x0F);
        CollectionAssert.AreEqual(new byte[] { 0, 0 }, sent[6..8]);
    }

    [TestMethod]
    public async Task ServeAsync_Queries_AreLoggedInOrderAndLaterOnesAnswered()
    {
        var replies = new DnsQueryUpstreamTestCaseReplies([]);

        var sent = await ServeAsync(replies, Query("examplehost.invalid", TypeA, id: 1), Query("examplehost.invalid", TypeAaaa, id: 2));

        Assert.HasCount(2, sent);
        Assert.AreEqual(
            "QNAME examplehost.invalid QTYPE A\nQNAME examplehost.invalid QTYPE AAAA\n",
            Encoding.Latin1.GetString(replies.QueryLog));
    }

    [TestMethod]
    public async Task ServeAsync_ResponseMessageOrNoHeader_IsNeitherLoggedNorAnswered()
    {
        var replies = new DnsQueryUpstreamTestCaseReplies([]);

        var sent = await ServeAsync(replies, Query("a.test", TypeA, flags: 0x8500), [0xBE, 0xEF, 0x01]);

        Assert.IsEmpty(sent);
        Assert.IsEmpty(replies.QueryLog);
    }

    // Serves the datagrams in order, then cancels the exchange once the flow waits for more,
    // and returns what was sent.
    private static async Task<IReadOnlyList<byte[]>> ServeAsync(DnsQueryUpstreamTestCaseReplies replies, byte[] firstDatagram, params byte[][] later)
    {
        var server = new DnsUpstreamTestCaseServer(replies);
        await using var flow = new InMemoryDatagramFlow(firstDatagram, later.Select(datagram => (ReadOnlyMemory<byte>)datagram));
        using var exchange = new CancellationTokenSource();

        var serving = server.ServeAsync(flow, Context(exchange.Token));
        await exchange.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
        return flow.SentDatagrams;
    }

    private static ExchangeContext Context(CancellationToken cancellationToken) => new(
        1,
        new ListenUrl("dns", "127.0.0.1", 53).WithBoundPort(53),
        new IPEndPoint(IPAddress.Loopback, 53),
        new IPEndPoint(IPAddress.Loopback, 50000),
        new RecordingExchangeLog(),
        TimeProvider.System,
        cancellationToken);
}
