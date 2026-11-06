using System.Security.Cryptography;

namespace Surl.Cryptography.Srp;

/// <summary>
/// A <see cref="RandomNumberGenerator" /> that fills every request with the same fixed bytes, so a
/// test can choose the server's secret <c>b</c>.
/// </summary>
internal sealed class UnitTestFixedRandomNumberGenerator(byte[] bytes) : RandomNumberGenerator
{
    public override void GetBytes(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        bytes.AsSpan(0, data.Length).CopyTo(data);
    }
}
