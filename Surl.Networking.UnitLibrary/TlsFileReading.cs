using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Text;

namespace Surl.Networking;

/// <summary>
/// Reading the bytes of a TLS option file and finding what it holds: the file itself, its PEM
/// blocks, and whether it is one DER value.
/// </summary>
internal static class TlsFileReading
{
    /// <summary>The PEM label of a certificate.</summary>
    public const string CertificateLabel = "CERTIFICATE";

    /// <summary>
    /// Reads every byte of <paramref name="path"/>.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <param name="failure">The failure a file that cannot be read is.</param>
    /// <param name="option">The option that named the file, for the message.</param>
    /// <returns>The file's bytes.</returns>
    /// <exception cref="TlsFileLoadException">The file cannot be read.</exception>
    public static byte[] ReadAllBytes(string path, TlsFileLoadFailure failure, string option)
    {
        try
        {
            return File.ReadAllBytes(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            throw new TlsFileLoadException(failure, $"Cannot read the {option} file '{path}'.", exception);
        }
    }

    /// <summary>
    /// Every PEM block in <paramref name="fileBytes"/>, in file order.
    /// </summary>
    /// <param name="fileBytes">The file's bytes.</param>
    /// <returns>The blocks; empty when the file holds none.</returns>
    public static IReadOnlyList<PemBlock> PemBlocksIn(byte[] fileBytes)
    {
        var blocks = new List<PemBlock>();
        ReadOnlySpan<char> remaining = Encoding.UTF8.GetString(fileBytes);

        while (PemEncoding.TryFind(remaining, out var fields))
        {
            blocks.Add(new PemBlock(
                remaining[fields.Label].ToString(),
                Convert.FromBase64String(remaining[fields.Base64Data].ToString())));
            remaining = remaining[fields.Location.End..];
        }

        return blocks;
    }

    /// <summary>
    /// Whether <paramref name="fileBytes"/> is exactly one BER or DER encoded value.
    /// </summary>
    /// <param name="fileBytes">The file's bytes.</param>
    /// <returns><see langword="true"/> for one value and nothing after it.</returns>
    public static bool IsOneDerValue(byte[] fileBytes)
    {
        try
        {
            var reader = new AsnReader(fileBytes, AsnEncodingRules.BER);
            reader.ReadEncodedValue();

            return !reader.HasData;
        }
        catch (AsnContentException)
        {
            return false;
        }
    }
}
