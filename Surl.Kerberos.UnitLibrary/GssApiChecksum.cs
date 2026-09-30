using System.Buffers.Binary;

namespace Surl.Kerberos;

/// <summary>
/// The authenticator checksum of RFC 4121 section 4.1.1, type <c>0x8003</c>: <c>Lgth</c> (4
/// bytes, little-endian, 16), the channel bindings <c>Bnd</c> (16 bytes, not checked), the GSS-API
/// <c>Flags</c> (4 bytes, little-endian), then, when <c>GSS_C_DELEG_FLAG</c> is set, <c>DlgOpt</c>,
/// <c>Dlgth</c> and a delegated credential of <c>Dlgth</c> bytes, which surl skips and never
/// stores (ADR-0057 decision 4, step 5).
/// </summary>
internal static class GssApiChecksum
{
    /// <summary>The checksum type of the GSS-API authenticator checksum.</summary>
    public const int ChecksumType = 0x8003;

    private const int MinimumLength = 24;
    private const int BindingsLength = 16;
    private const int FlagsOffset = 20;
    private const int DelegationLengthOffset = 26;
    private const int DelegationOffset = 28;
    private const uint DelegationFlag = 1;

    /// <summary>Gets whether an authenticator's checksum is a well-formed GSS-API checksum.</summary>
    /// <param name="checksumType">The <c>cksumtype</c>, or <see langword="null" /> when the authenticator has none.</param>
    /// <param name="checksum">The checksum bytes.</param>
    /// <returns><see langword="true" /> when it is of type <c>0x8003</c>, at least 24 bytes, <c>Lgth</c> 16, and any delegated credential fits.</returns>
    public static bool IsWellFormed(int? checksumType, byte[] checksum)
    {
        if (checksumType != ChecksumType || checksum.Length < MinimumLength || BinaryPrimitives.ReadUInt32LittleEndian(checksum) != BindingsLength)
        {
            return false;
        }

        uint flags = BinaryPrimitives.ReadUInt32LittleEndian(checksum.AsSpan(FlagsOffset));
        return (flags & DelegationFlag) == 0 || DelegatedCredentialFits(checksum);
    }

    private static bool DelegatedCredentialFits(byte[] checksum) =>
        checksum.Length >= DelegationOffset
        && DelegationOffset + BinaryPrimitives.ReadUInt16LittleEndian(checksum.AsSpan(DelegationLengthOffset)) <= checksum.Length;
}
