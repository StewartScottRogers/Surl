using System.Security.Cryptography;

namespace Surl.TlsSrp;

/// <summary>
/// A <see cref="RandomNumberGenerator" /> that fills every request with the same fixed bytes, so a
/// test can choose a record's explicit IV.
/// </summary>
internal sealed class UnitTestFixedRandomNumberGenerator(byte[] bytes) : RandomNumberGenerator
{
    public override void GetBytes(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        bytes.AsSpan(0, data.Length).CopyTo(data);
    }
}
