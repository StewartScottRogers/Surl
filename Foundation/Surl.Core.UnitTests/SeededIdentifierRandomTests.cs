using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Surl.Core;

[TestClass]
public sealed class SeededIdentifierRandomTests
{
    [TestMethod]
    public void ForExchange_SameSeedAndExchangeId_GivesTheSameBytesWhateverElseWasDrawn()
    {
        var quiet = new SeededIdentifierRandom(42);
        var busy = new SeededIdentifierRandom(42);
        for (var exchangeId = 1L; exchangeId <= 6; exchangeId++)
        {
            busy.ForExchange(exchangeId).NextBytes(new byte[100]);
        }

        var quietBytes = Draw(quiet.ForExchange(7), 100);
        var busyBytes = Draw(busy.ForExchange(7), 100);

        CollectionAssert.AreEqual(quietBytes, busyBytes);
    }

    [TestMethod]
    public void ForExchange_DifferentSeedsOrExchangeIds_GiveDifferentBytes()
    {
        var random = new SeededIdentifierRandom(42);

        var exchange7 = Draw(random.ForExchange(7), 32);
        var exchange8 = Draw(random.ForExchange(8), 32);
        var otherSeed = Draw(new SeededIdentifierRandom(43).ForExchange(7), 32);

        CollectionAssert.AreNotEqual(exchange7, exchange8);
        CollectionAssert.AreNotEqual(exchange7, otherSeed);
    }

    [TestMethod]
    public void ForExchange_DrawnInPiecesAcrossBlocks_GivesTheSameBytesAsDrawnAtOnce()
    {
        var random = new SeededIdentifierRandom(5);
        var pieces = random.ForExchange(1);
        var drawnInPieces = new byte[70];

        pieces.NextBytes(drawnInPieces.AsSpan(0, 3));
        pieces.NextBytes(drawnInPieces.AsSpan(3, 0));
        pieces.NextBytes(drawnInPieces.AsSpan(3, 40));
        pieces.NextBytes(drawnInPieces.AsSpan(43));

        CollectionAssert.AreEqual(Draw(random.ForExchange(1), 70), drawnInPieces);
    }

    [TestMethod]
    public void ForExchange_FirstBlock_IsSha256OfLabelSeedScopeAndCounter()
    {
        var expectedInput = new List<byte>(Encoding.ASCII.GetBytes("exchange")) { 0 };
        expectedInput.AddRange(BigEndian(9L));
        expectedInput.AddRange(BigEndian(sizeof(long)));
        expectedInput.AddRange(BigEndian(3L));
        expectedInput.AddRange(BigEndian(0L));

        var bytes = Draw(new SeededIdentifierRandom(9).ForExchange(3), 32);

        CollectionAssert.AreEqual(SHA256.HashData(expectedInput.ToArray()), bytes);
    }

    [TestMethod]
    public void ForServer_SameSeedAndName_GivesTheSameBytes()
    {
        var first = Draw(new SeededIdentifierRandom(42).ForServer("nats"), 50);
        var second = Draw(new SeededIdentifierRandom(42).ForServer("nats"), 50);

        CollectionAssert.AreEqual(first, second);
    }

    [TestMethod]
    public void ForServer_DifferentNamesOrAnExchange_GiveDifferentBytes()
    {
        var random = new SeededIdentifierRandom(42);

        var nats = Draw(random.ForServer("nats"), 32);
        var amqp = Draw(random.ForServer("amqp"), 32);
        var exchange = Draw(random.ForExchange(0), 32);

        CollectionAssert.AreNotEqual(nats, amqp);
        CollectionAssert.AreNotEqual(nats, exchange);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void ForServer_NullOrEmptyName_Throws(string? name)
    {
        var random = new SeededIdentifierRandom(1);

        Assert.Throws<ArgumentException>(() => random.ForServer(name!));
    }

    [TestMethod]
    [DataRow(0L)]
    [DataRow(long.MaxValue)]
    public void Constructor_SeedInRange_KeepsTheSeed(long seed)
    {
        var random = new SeededIdentifierRandom(seed);

        Assert.AreEqual(seed, random.Seed);
    }

    [TestMethod]
    public void Constructor_NegativeSeed_ThrowsArgumentOutOfRange()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new SeededIdentifierRandom(-1));
    }

    private static byte[] Draw(Surl.Protocol.Abstractions.IIdentifierRandom source, int count)
    {
        var bytes = new byte[count];
        source.NextBytes(bytes);
        return bytes;
    }

    private static byte[] BigEndian(long value)
    {
        var bytes = new byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(bytes, value);
        return bytes;
    }

    private static byte[] BigEndian(int value)
    {
        var bytes = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(bytes, value);
        return bytes;
    }
}
