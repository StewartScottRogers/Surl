using System.Buffers.Binary;
using System.Text;

namespace Surl.Authentication;

/// <summary>
/// The SSH public key types an <c>--authorized-keys</c> file may name (ADR-0051, section 6), and
/// whether a blob is a well-formed key of one (RFC 4253 section 6.6, RFC 5656 section 3.1,
/// RFC 8709 section 4): its fields are <c>string</c>s (a 32-bit big-endian length, then that many
/// bytes), the first is the key type, and nothing follows the last.
/// </summary>
internal static class SshPublicKeyBlob
{
    private const string EcdsaPrefix = "ecdsa-sha2-";

    /// <summary>
    /// <c>ssh-ed25519</c>, the three <c>ecdsa-sha2-nistp*</c>, <c>ssh-rsa</c> and <c>ssh-dss</c>.
    /// </summary>
    public static readonly IReadOnlySet<string> SupportedKeyTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "ssh-ed25519",
        "ecdsa-sha2-nistp256",
        "ecdsa-sha2-nistp384",
        "ecdsa-sha2-nistp521",
        "ssh-rsa",
        "ssh-dss",
    };

    /// <summary>
    /// Whether <paramref name="blob"/> is a well-formed public key of <paramref name="keyType"/>:
    /// an Ed25519 key of 32 bytes; an ECDSA key naming its own curve with an uncompressed point
    /// of that curve's length; an RSA key's two or a DSA key's four non-empty <c>mpint</c>s.
    /// </summary>
    /// <param name="blob">The decoded blob.</param>
    /// <param name="keyType">One of <see cref="SupportedKeyTypes"/>.</param>
    /// <returns><see langword="true"/> when the blob is that type's key and nothing more.</returns>
    public static bool IsWellFormed(ReadOnlySpan<byte> blob, string keyType)
    {
        if (!TryReadString(ref blob, out var type) || !type.SequenceEqual(Encoding.UTF8.GetBytes(keyType)))
        {
            return false;
        }

        var fieldsAreWellFormed = keyType switch
        {
            "ssh-ed25519" => HasEd25519Key(ref blob),
            "ssh-rsa" => HasNonEmptyStrings(ref blob, 2),
            "ssh-dss" => HasNonEmptyStrings(ref blob, 4),
            _ => HasEcdsaKey(ref blob, keyType[EcdsaPrefix.Length..]),
        };

        return fieldsAreWellFormed && blob.IsEmpty;
    }

    private static bool HasEd25519Key(ref ReadOnlySpan<byte> blob) =>
        TryReadString(ref blob, out var key) && key.Length == 32;

    private static bool HasNonEmptyStrings(ref ReadOnlySpan<byte> blob, int count)
    {
        for (var index = 0; index < count; index++)
        {
            if (!TryReadString(ref blob, out var value) || value.IsEmpty)
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasEcdsaKey(ref ReadOnlySpan<byte> blob, string curve) =>
        TryReadString(ref blob, out var curveName)
            && curveName.SequenceEqual(Encoding.UTF8.GetBytes(curve))
            && TryReadString(ref blob, out var point)
            && IsUncompressedPoint(point, curve);

    // An uncompressed point (SEC 1 section 2.3.3): 0x04, then X and Y of the curve's field size.
    private static bool IsUncompressedPoint(ReadOnlySpan<byte> point, string curve)
    {
        var coordinateLength = curve switch
        {
            "nistp256" => 32,
            "nistp384" => 48,
            _ => 66,
        };

        return point.Length == 1 + (2 * coordinateLength) && point[0] == 0x04;
    }

    private static bool TryReadString(ref ReadOnlySpan<byte> blob, out ReadOnlySpan<byte> value)
    {
        value = default;
        if (blob.Length < sizeof(uint))
        {
            return false;
        }

        var length = BinaryPrimitives.ReadUInt32BigEndian(blob);
        if (length > (uint)(blob.Length - sizeof(uint)))
        {
            return false;
        }

        value = blob.Slice(sizeof(uint), (int)length);
        blob = blob[(sizeof(uint) + (int)length)..];

        return true;
    }
}
