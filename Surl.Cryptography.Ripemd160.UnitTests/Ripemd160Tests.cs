using System.Text;

namespace Surl.Cryptography.Ripemd160;

/// <summary>
/// Pins <see cref="Ripemd160" /> to published vectors only (ADR-0003): the RIPEMD-160 authors'
/// list on https://homes.esat.kuleuven.be/~bosselae/ripemd160.html, and checks that a message
/// split across <see cref="Ripemd160.AppendData" /> calls hashes as the whole, and its argument
/// and disposal rules.
/// </summary>
[TestClass]
public sealed class Ripemd160Tests
{
    private const string EmptyMessageHash = "9c1185a5c5e9fc54612808977ee8f548b2258d31";

    // The authors' RIPEMD-160 page, "Test vectors": each message and its RIPEMD-160 hash.
    [TestMethod]
    [DataRow("", EmptyMessageHash)]
    [DataRow("a", "0bdc9d2d256b3ee9daae347be6f4dc835a467ffe")]
    [DataRow("abc", "8eb208f7e05d987a9b044a8e98c6b087f15a0bfc")]
    [DataRow("message digest", "5d0689ef49d2fae572b881b123a85ffa21595f36")]
    [DataRow("abcdefghijklmnopqrstuvwxyz", "f71c27109c692c1b56bbdceb5b9d2865b3708dbc")]
    [DataRow("abcdbcdecdefdefgefghfghighijhijkijkljklmklmnlmnomnopnopq", "12a053384a9c0c88e405a06c27dcf49ada62eb2b")]
    [DataRow("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789", "b0e20b6e3116640286ed3a87a5713079b21f5189")]
    public void HashData_AuthorsTestVector_GivesTheListedHash(string message, string expected)
    {
        var hash = Ripemd160.HashData(Encoding.ASCII.GetBytes(message));

        Assert.AreEqual(expected, Convert.ToHexStringLower(hash));
    }

    // The authors' RIPEMD-160 page, "Test vectors": 8 times "1234567890".
    [TestMethod]
    public void HashData_AuthorsTestVectorEightTimes1234567890_GivesTheListedHash()
    {
        var hash = new byte[Ripemd160.HashSize];

        Ripemd160.HashData(Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat("1234567890", 8))), hash);

        Assert.AreEqual("9b752e45573d4b39f4dbd3323cab82bf63326bfb", Convert.ToHexStringLower(hash));
    }

    // The authors' RIPEMD-160 page, "Test vectors": 1 million times "a", appended a thousand
    // bytes at a time and in one piece.
    [TestMethod]
    public void AppendDataAndHashData_AuthorsTestVectorOneMillionA_GiveTheListedHash()
    {
        const string expected = "52783243c1697bdbe16d37f97f68f08325dc1528";
        var thousand = Enumerable.Repeat((byte)'a', 1000).ToArray();
        var hash = new byte[Ripemd160.HashSize];
        using var ripemd160 = new Ripemd160();

        for (var count = 0; count < 1000; count++)
        {
            ripemd160.AppendData(thousand);
        }

        ripemd160.GetHashAndReset(hash);

        Assert.AreEqual(expected, Convert.ToHexStringLower(hash));
        Assert.AreEqual(expected, Convert.ToHexStringLower(Ripemd160.HashData(Enumerable.Repeat((byte)'a', 1_000_000).ToArray())));
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(7)]
    [DataRow(55)]
    [DataRow(56)]
    [DataRow(63)]
    [DataRow(64)]
    [DataRow(65)]
    public void AppendData_MessageSplitAcrossCalls_HashesAsTheWhole(int chunkLength)
    {
        var message = Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat("1234567890", 20)));
        var pieces = new byte[Ripemd160.HashSize];
        using var ripemd160 = new Ripemd160();

        for (var offset = 0; offset < message.Length; offset += chunkLength)
        {
            ripemd160.AppendData(message.AsSpan(offset, Math.Min(chunkLength, message.Length - offset)));
        }

        ripemd160.GetHashAndReset(pieces);

        Assert.AreEqual(Convert.ToHexStringLower(Ripemd160.HashData(message)), Convert.ToHexStringLower(pieces));
    }

    // The authors' RIPEMD-160 page: the empty message's hash, after "abc" was finished.
    [TestMethod]
    public void GetHashAndReset_CalledTwice_StartsAnEmptyMessage()
    {
        var hash = new byte[Ripemd160.HashSize];
        using var ripemd160 = new Ripemd160();
        ripemd160.AppendData("abc"u8);
        ripemd160.GetHashAndReset(hash);

        ripemd160.GetHashAndReset(hash);

        Assert.AreEqual(EmptyMessageHash, Convert.ToHexStringLower(hash));
    }

    [TestMethod]
    [DataRow(19)]
    [DataRow(21)]
    public void GetHashAndReset_WrongDestinationLength_Throws(int length)
    {
        using var ripemd160 = new Ripemd160();

        Assert.ThrowsExactly<ArgumentException>(() => ripemd160.GetHashAndReset(new byte[length]));
    }

    [TestMethod]
    public void AppendData_AfterDispose_Throws()
    {
        var ripemd160 = new Ripemd160();
        ripemd160.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => ripemd160.AppendData("a"u8));
    }

    [TestMethod]
    public void GetHashAndReset_AfterDispose_Throws()
    {
        var ripemd160 = new Ripemd160();
        ripemd160.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => ripemd160.GetHashAndReset(new byte[Ripemd160.HashSize]));
    }
}
