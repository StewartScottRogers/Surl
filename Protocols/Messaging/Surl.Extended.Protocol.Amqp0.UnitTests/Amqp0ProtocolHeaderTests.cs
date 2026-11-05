using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Amqp0.UnitTests;

/// <summary>
/// The protocol header (AMQP 0-9-1 section 4.2.2; ADR-0112 decision 1).
/// </summary>
[TestClass]
public sealed class Amqp0ProtocolHeaderTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ReadAsync_Amqp0091Header_IsAcceptedWithNothingWritten()
    {
        var connection = new InMemoryConnection([new byte[] { (byte)'A', (byte)'M', (byte)'Q', (byte)'P', 0x00, 0x00, 0x09, 0x01 }]);

        var outcome = await Amqp0ProtocolHeader.ReadAsync(connection, TestContext.CancellationToken);

        Assert.AreEqual(Amqp0ProtocolHeaderOutcome.Accepted, outcome);
        Assert.IsEmpty(connection.WrittenBytes);
        Assert.IsFalse(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task ReadAsync_HeaderSplitAcrossReads_IsAccepted()
    {
        var connection = new InMemoryConnection(["AM"u8.ToArray(), "QP\0"u8.ToArray(), new byte[] { 0x00, 0x09, 0x01 }]);

        var outcome = await Amqp0ProtocolHeader.ReadAsync(connection, TestContext.CancellationToken);

        Assert.AreEqual(Amqp0ProtocolHeaderOutcome.Accepted, outcome);
    }

    [TestMethod]
    [DataRow(new byte[] { (byte)'A', (byte)'M', (byte)'Q', (byte)'P', 1, 1, 0, 9 }, DisplayName = "AMQP 0-9")]
    [DataRow(new byte[] { (byte)'A', (byte)'M', (byte)'Q', (byte)'P', 1, 1, 9, 1 }, DisplayName = "AMQP 0-8")]
    [DataRow(new byte[] { (byte)'A', (byte)'M', (byte)'Q', (byte)'P', 0, 1, 0, 0 }, DisplayName = "AMQP 1.0")]
    [DataRow(new byte[] { (byte)'A', (byte)'M', (byte)'Q', (byte)'P', 3, 1, 0, 0 }, DisplayName = "AMQP 1.0 SASL")]
    [DataRow(new byte[] { (byte)'G', (byte)'E', (byte)'T', (byte)' ', (byte)'/', (byte)' ', (byte)'H', (byte)'T' }, DisplayName = "HTTP")]
    public async Task ReadAsync_AnyOtherHeader_IsAnsweredWithTheSupportedHeaderAndClosed(byte[] header)
    {
        var connection = new InMemoryConnection([header]);

        var outcome = await Amqp0ProtocolHeader.ReadAsync(connection, TestContext.CancellationToken);

        Assert.AreEqual(Amqp0ProtocolHeaderOutcome.Refused, outcome);
        CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("AMQP\0\0\u0009\u0001"), connection.WrittenBytes);
        Assert.IsTrue(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task ReadAsync_ClientClosesBeforeEightBytes_WritesNothing()
    {
        var connection = new InMemoryConnection(["AMQP"u8.ToArray()]);

        var outcome = await Amqp0ProtocolHeader.ReadAsync(connection, TestContext.CancellationToken);

        Assert.AreEqual(Amqp0ProtocolHeaderOutcome.ConnectionClosedEarly, outcome);
        Assert.IsEmpty(connection.WrittenBytes);
    }

    [TestMethod]
    public async Task ReadAsync_NullConnection_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            async () => await Amqp0ProtocolHeader.ReadAsync(null!, TestContext.CancellationToken));
    }

    [TestMethod]
    public void Supported_IsAmqp0091()
    {
        CollectionAssert.AreEqual(new byte[] { 0x41, 0x4D, 0x51, 0x50, 0x00, 0x00, 0x09, 0x01 }, Amqp0ProtocolHeader.Supported.ToArray());
    }
}
