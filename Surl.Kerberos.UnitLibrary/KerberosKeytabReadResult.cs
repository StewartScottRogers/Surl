namespace Surl.Kerberos;

/// <summary>
/// What <see cref="KerberosKeytab.Read" /> made of a keytab file's bytes: the keytab and the
/// entries it skipped, or the offset at which the bytes stopped being a keytab.
/// </summary>
/// <param name="Keytab">The keytab read, or <see langword="null" /> when the bytes are malformed.</param>
/// <param name="SkippedEntries">The entries skipped for their encryption type, in file order; empty when malformed.</param>
/// <param name="MalformedOffset">
/// When malformed, the offset of the first field that could not be read: 0 for a version other
/// than <c>0x0502</c>, or the start of a field that runs past its entry or the end of the bytes.
/// <see langword="null" /> when the keytab was read.
/// </param>
public sealed record KerberosKeytabReadResult(KerberosKeytab? Keytab, IReadOnlyList<KerberosKeytabSkippedEntry> SkippedEntries, int? MalformedOffset);
