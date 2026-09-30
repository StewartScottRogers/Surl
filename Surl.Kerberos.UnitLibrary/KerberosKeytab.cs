using System.Text;

namespace Surl.Kerberos;

/// <summary>
/// The service keys surl holds, read from an MIT-format keytab file (ADR-0057 decision 1): the
/// format <c>ktutil</c>, <c>kadmin ktadd</c>, Heimdal's <c>ktutil</c> and Windows'
/// <c>ktpass /out</c> write.
/// </summary>
/// <param name="entries">The keys, in file order.</param>
public sealed class KerberosKeytab(IEnumerable<KerberosKeytabEntry> entries)
{
    private const ushort Version0502 = 0x0502;

    /// <summary>Gets the keys, in file order.</summary>
    public IReadOnlyList<KerberosKeytabEntry> Entries { get; } = [.. entries];

    /// <summary>
    /// Reads a keytab file's bytes: the version <c>0x05 0x02</c>, then big-endian entries, each a
    /// signed 32-bit length and that many bytes of principal (component count, realm,
    /// components, name type), timestamp, 8-bit key version number, enctype and key, and an
    /// optional trailing 32-bit key version number that overrides the 8-bit one when it is not 0.
    /// A negative length marks a deleted entry, which is skipped; a zero length ends the file, as
    /// MIT krb5 reads it. An entry whose enctype surl does not accept is skipped and reported.
    /// </summary>
    /// <param name="bytes">The file's bytes.</param>
    /// <returns>The keytab and its skipped entries, or the offset at which the bytes are malformed.</returns>
    public static KerberosKeytabReadResult Read(ReadOnlySpan<byte> bytes)
    {
        KeytabByteReader reader = new(bytes.ToArray(), 0, bytes.Length);
        List<KerberosKeytabEntry> entries = [];
        List<KerberosKeytabSkippedEntry> skippedEntries = [];
        try
        {
            if (reader.ReadUInt16() != Version0502)
            {
                return new KerberosKeytabReadResult(null, [], 0);
            }

            while (reader.HasData && ReadRecord(reader, entries, skippedEntries))
            {
            }
        }
        catch (KeytabMalformedException exception)
        {
            return new KerberosKeytabReadResult(null, [], exception.Offset);
        }

        return new KerberosKeytabReadResult(new KerberosKeytab(entries), skippedEntries, null);
    }

    /// <summary>
    /// Finds the key for <paramref name="serverName" /> in the encryption type numbered
    /// <paramref name="encryptionTypeNumber" /> (ADR-0057 decisions 2 and 4): the realm and the
    /// first component, the service, compare case-insensitively (ordinal), the rest ordinally;
    /// the key version number must be <paramref name="keyVersionNumber" />, or, when that is
    /// absent, the highest the keytab holds for that principal and encryption type.
    /// </summary>
    /// <param name="serverName">The ticket's server principal.</param>
    /// <param name="encryptionTypeNumber">The ticket's <c>enc-part.etype</c>.</param>
    /// <param name="keyVersionNumber">The ticket's <c>enc-part.kvno</c>, or <see langword="null" /> when absent.</param>
    /// <returns>The entry, or <see langword="null" /> when the keytab holds none.</returns>
    internal KerberosKeytabEntry? FindKey(KerberosPrincipalName serverName, int encryptionTypeNumber, uint? keyVersionNumber)
    {
        IEnumerable<KerberosKeytabEntry> candidates = Entries.Where(entry =>
            (int)entry.EncryptionType == encryptionTypeNumber && NamesSamePrincipal(entry.Principal, serverName));
        return keyVersionNumber is null
            ? candidates.MaxBy(entry => entry.KeyVersionNumber)
            : candidates.FirstOrDefault(entry => entry.KeyVersionNumber == keyVersionNumber);
    }

    private static bool NamesSamePrincipal(KerberosPrincipalName held, KerberosPrincipalName asked)
    {
        if (!string.Equals(held.Realm, asked.Realm, StringComparison.OrdinalIgnoreCase) || held.Components.Count != asked.Components.Count)
        {
            return false;
        }

        for (int index = 0; index < held.Components.Count; index++)
        {
            StringComparison comparison = index == 0 ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!string.Equals(held.Components[index], asked.Components[index], comparison))
            {
                return false;
            }
        }

        return true;
    }

    // Reads one length-prefixed record; returns false at the zero length that ends the file.
    private static bool ReadRecord(KeytabByteReader reader, List<KerberosKeytabEntry> entries, List<KerberosKeytabSkippedEntry> skippedEntries)
    {
        int lengthOffset = reader.Offset;
        int length = reader.ReadInt32();
        if (length == 0)
        {
            return false;
        }

        KeytabByteReader record = reader.ReadRecord(lengthOffset, Math.Abs((long)length));
        if (length > 0)
        {
            ReadEntry(record, entries, skippedEntries);
        }

        return true;
    }

    private static void ReadEntry(KeytabByteReader record, List<KerberosKeytabEntry> entries, List<KerberosKeytabSkippedEntry> skippedEntries)
    {
        KerberosPrincipalName principal = ReadPrincipal(record);
        record.ReadUInt32();
        uint keyVersionNumber = record.ReadByte();
        int encryptionTypeNumber = record.ReadUInt16();
        int keyOffset = record.Offset;
        byte[] key = record.ReadCountedBytes();
        if (record.RemainingLength >= 4)
        {
            uint longKeyVersionNumber = record.ReadUInt32();
            keyVersionNumber = longKeyVersionNumber == 0 ? keyVersionNumber : longKeyVersionNumber;
        }

        if (!Enum.IsDefined((KerberosEncryptionType)encryptionTypeNumber))
        {
            skippedEntries.Add(new KerberosKeytabSkippedEntry(principal, encryptionTypeNumber));
            return;
        }

        KerberosEncryptionType encryptionType = (KerberosEncryptionType)encryptionTypeNumber;
        if (key.Length != KerberosEncryptionProfile.For(encryptionType).KeyLength)
        {
            throw new KeytabMalformedException(keyOffset);
        }

        entries.Add(new KerberosKeytabEntry(principal, keyVersionNumber, encryptionType, key));
    }

    // Component count, realm, components, then the name type, which surl does not use.
    private static KerberosPrincipalName ReadPrincipal(KeytabByteReader record)
    {
        int countOffset = record.Offset;
        int componentCount = record.ReadUInt16();
        if (componentCount == 0)
        {
            throw new KeytabMalformedException(countOffset);
        }

        string realm = Encoding.UTF8.GetString(record.ReadCountedBytes());
        string[] components = new string[componentCount];
        for (int index = 0; index < componentCount; index++)
        {
            components[index] = Encoding.UTF8.GetString(record.ReadCountedBytes());
        }

        record.ReadUInt32();
        return new KerberosPrincipalName(realm, components);
    }
}
