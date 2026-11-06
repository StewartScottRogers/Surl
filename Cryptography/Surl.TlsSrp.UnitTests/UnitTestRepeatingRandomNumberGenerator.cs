using System.Security.Cryptography;

namespace Surl.TlsSrp;

/// <summary>
/// A <see cref="RandomNumberGenerator" /> that fills every request with <c>pattern</c> repeated, as
/// <c>Run-TlsSrpRelay.cs</c> does, so a test replays a recording with the bytes its server drew.
/// </summary>
internal sealed class UnitTestRepeatingRandomNumberGenerator(byte[] pattern) : RandomNumberGenerator
{
    public override void GetBytes(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        for (var index = 0; index < data.Length; index++)
        {
            data[index] = pattern[index % pattern.Length];
        }
    }
}
