using System.Buffers.Text;
using System.Security.Cryptography;

namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// Not random: fills each request for bytes with one byte value, 1 the first time, 2 the next, so a
/// test knows every token the server issues.
/// </summary>
internal sealed class CountingRandomNumberGenerator : RandomNumberGenerator
{
    private int count;

    public override void GetBytes(byte[] data) => GetBytes(data.AsSpan());

    public override void GetBytes(Span<byte> data) => data.Fill((byte)Interlocked.Increment(ref count));

    /// <summary>
    /// The token text the <paramref name="number"/>th draw makes: 32 bytes of that value, base64url.
    /// </summary>
    public static string TokenOf(int number) =>
        Base64Url.EncodeToString(Enumerable.Repeat((byte)number, 32).ToArray());
}
