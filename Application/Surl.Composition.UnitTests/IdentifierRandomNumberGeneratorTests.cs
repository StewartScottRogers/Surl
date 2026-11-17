using Surl.Core;

namespace Surl.Composition;

[TestClass]
public sealed class IdentifierRandomNumberGeneratorTests
{
    [TestMethod]
    public void GetBytes_Span_FillsItFromTheSeededSource()
    {
        var expected = new byte[16];
        new SeededIdentifierRandom(7).ForExchange(1).NextBytes(expected);
        using var generator = new IdentifierRandomNumberGenerator(new SeededIdentifierRandom(7).ForExchange(1));
        var actual = new byte[16];

        generator.GetBytes(actual.AsSpan());

        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void GetBytes_Array_FillsItFromTheSeededSource()
    {
        var expected = new byte[16];
        new SeededIdentifierRandom(7).ForExchange(1).NextBytes(expected);
        using var generator = new IdentifierRandomNumberGenerator(new SeededIdentifierRandom(7).ForExchange(1));
        var actual = new byte[16];

        generator.GetBytes(actual);

        CollectionAssert.AreEqual(expected, actual);
    }
}
