using Surl.Core;

namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// <see cref="IdentifierCoapRandomSource"/>: draws within their ranges, repeated by a seed (ADR-0124 decision 6).
/// </summary>
[TestClass]
public sealed class IdentifierCoapRandomSourceTests
{
    [TestMethod]
    public void Draws_AreWithinTheirRanges()
    {
        var random = new IdentifierCoapRandomSource(new SeededIdentifierRandom(5).ForExchange(1));
        var bytes = new byte[64];

        random.Fill(bytes);
        var fraction = random.DrawFraction();
        _ = random.DrawMessageId();

        Assert.IsTrue(fraction is >= 0 and < 1);
        Assert.IsTrue(bytes.Any(value => value != 0));
    }

    [TestMethod]
    public void Draws_SameSeedAndExchange_AreTheSame()
    {
        var first = new IdentifierCoapRandomSource(new SeededIdentifierRandom(9).ForExchange(3));
        var second = new IdentifierCoapRandomSource(new SeededIdentifierRandom(9).ForExchange(3));

        Assert.AreEqual(first.DrawMessageId(), second.DrawMessageId());
        Assert.AreEqual(first.DrawFraction(), second.DrawFraction());
    }

    [TestMethod]
    public void DrawMessageId_TwoBytesBigEndian_AreTheId()
    {
        var random = new IdentifierCoapRandomSource(new FixedBytesIdentifierRandom(0x12, 0x34, 0xFF, 0xFF, 0xFF, 0xFF));

        Assert.AreEqual((ushort)0x1234, random.DrawMessageId());
        Assert.AreEqual((double)((1 << 30) - 1) / (1 << 30), random.DrawFraction());
    }

    private sealed class FixedBytesIdentifierRandom(params byte[] bytes) : Surl.Protocol.Abstractions.IIdentifierRandom
    {
        private int position;

        public void NextBytes(Span<byte> destination)
        {
            bytes.AsSpan(position, destination.Length).CopyTo(destination);
            position += destination.Length;
        }
    }
}
