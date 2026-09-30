using System.Numerics;
using System.Security.Cryptography;

namespace Surl.Protocol.Ssh;

/// <summary>
/// An ECDSA host key on P-256, P-384 or P-521: an <c>ecdsa-sha2-&lt;curve&gt;</c> blob
/// (<c>string</c> the key type, <c>string</c> the curve's name, <c>string Q</c>) that signs as
/// the same name with the curve's hash, the signature carried as <c>mpint r</c>,
/// <c>mpint s</c> (RFC 5656, sections 3.1 and 3.1.2).
/// </summary>
internal sealed class SshEcdsaHostKey : SshHostKey
{
    private readonly SshNistCurve curve;

    private readonly ECParameters parameters;

    /// <summary>
    /// Creates the host key from a private key.
    /// </summary>
    /// <param name="curve">The key's curve.</param>
    /// <param name="parameters">The private key.</param>
    public SshEcdsaHostKey(SshNistCurve curve, ECParameters parameters)
        : base(KeyTypeOn(curve), Blob(curve, parameters), [KeyTypeOn(curve)])
    {
        this.curve = curve;
        this.parameters = parameters;
    }

    /// <summary>
    /// The key type, and host-key algorithm, of a key on <paramref name="curve"/>.
    /// </summary>
    /// <param name="curve">The curve.</param>
    /// <returns><c>ecdsa-sha2-</c> and the curve's name.</returns>
    public static string KeyTypeOn(SshNistCurve curve) => "ecdsa-sha2-" + curve.Identifier;

    /// <inheritdoc/>
    private protected override byte[] SignRaw(string algorithm, byte[] data)
    {
        using var ecdsa = ECDsa.Create(parameters);
        var signature = ecdsa.SignData(data, curve.HashAlgorithm, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        var values = new SshWireWriter();
        values.WriteMpint(new BigInteger(signature.AsSpan(0, curve.FieldLength), isUnsigned: true, isBigEndian: true));
        values.WriteMpint(new BigInteger(signature.AsSpan(curve.FieldLength), isUnsigned: true, isBigEndian: true));

        return values.ToArray();
    }

    private static byte[] Blob(SshNistCurve curve, ECParameters parameters)
    {
        var blob = new SshWireWriter();
        blob.WriteString(KeyTypeOn(curve));
        blob.WriteString(curve.Identifier);
        blob.WriteString(curve.EncodePoint(parameters.Q));

        return blob.ToArray();
    }
}
