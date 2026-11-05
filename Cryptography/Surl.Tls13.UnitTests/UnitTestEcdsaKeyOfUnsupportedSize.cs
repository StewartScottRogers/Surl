using System.Security.Cryptography;

namespace Surl.Tls13;

/// <summary>An ECDSA key of 224 bits (P-224), a curve no ADR-0082 signature scheme names; it never signs.</summary>
internal sealed class UnitTestEcdsaKeyOfUnsupportedSize : ECDsa
{
    public UnitTestEcdsaKeyOfUnsupportedSize()
    {
        KeySizeValue = 224;
        LegalKeySizesValue = [new KeySizes(224, 224, 0)];
    }

    public override byte[] SignHash(byte[] hash) => throw new NotSupportedException();

    public override bool VerifyHash(byte[] hash, byte[] signature) => throw new NotSupportedException();
}
