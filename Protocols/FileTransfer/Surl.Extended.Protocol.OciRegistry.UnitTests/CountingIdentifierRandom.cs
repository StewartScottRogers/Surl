using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// Fills each request for random bytes with one byte value, 1 the first time, 2 the next, so a
/// test knows every token the server issues.
/// </summary>
internal sealed class CountingIdentifierRandom : IIdentifierRandom
{
    private int count;

    public void NextBytes(Span<byte> destination) => destination.Fill((byte)Interlocked.Increment(ref count));

    /// <summary>
    /// The token text the <paramref name="number"/>th draw makes: 32 bytes of that value, base64url.
    /// </summary>
    public static string TokenOf(int number) =>
        System.Buffers.Text.Base64Url.EncodeToString(Enumerable.Repeat((byte)number, 32).ToArray());
}
