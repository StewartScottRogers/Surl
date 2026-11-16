using System.Security.Cryptography;

namespace Surl.TlsSrp;

/// <summary>A test source whose first fill is all zero bytes and every later fill is all sevens.</summary>
internal sealed class UnitTestZeroThenSevenRandomNumberGenerator : RandomNumberGenerator
{
    private bool hasFilled;

    public override void GetBytes(byte[] data) => FillWith(data);

    public override void GetBytes(Span<byte> data) => FillWith(data);

    private void FillWith(Span<byte> data)
    {
        data.Fill(hasFilled ? (byte)7 : (byte)0);
        hasFilled = true;
    }
}
