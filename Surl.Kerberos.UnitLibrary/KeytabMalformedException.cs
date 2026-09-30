namespace Surl.Kerberos;

/// <summary>
/// Thrown inside <see cref="KerberosKeytab.Read" /> when a field runs past its entry or the end
/// of the file; caught there and returned as <see cref="KerberosKeytabReadResult.MalformedOffset" />.
/// </summary>
/// <param name="offset">The offset of the field that could not be read.</param>
internal sealed class KeytabMalformedException(int offset) : Exception($"The keytab is malformed at byte {offset}.")
{
    /// <summary>Gets the offset of the field that could not be read.</summary>
    public int Offset { get; } = offset;
}
