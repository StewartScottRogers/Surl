using System.Buffers.Binary;
using System.Text;

namespace Surl.Kerberos.TestKdc;

/// <summary>
/// Writes keys as an MIT keytab, the format <see cref="KerberosKeytab.Read" /> reads (ADR-0057
/// decision 1): the version <c>0x05 0x02</c>, then big-endian entries, each a 32-bit length and that
/// many bytes of principal (component count, realm, components, name type), timestamp, 8-bit key
/// version number, enctype, key, and the trailing 32-bit key version number.
/// </summary>
internal static class MitKeytabWriter
{
    /// <summary>Writes <paramref name="entries" /> in order.</summary>
    /// <param name="entries">The keys.</param>
    /// <param name="timestamp">When the keys were made, in seconds since 1970, written in every entry.</param>
    /// <returns>The keytab file's bytes.</returns>
    public static byte[] Write(IEnumerable<KerberosKeytabEntry> entries, uint timestamp)
    {
        List<byte> file = [0x05, 0x02];
        foreach (KerberosKeytabEntry entry in entries)
        {
            byte[] record = WriteRecord(entry, timestamp);
            AppendUInt32(file, (uint)record.Length);
            file.AddRange(record);
        }

        return [.. file];
    }

    private static byte[] WriteRecord(KerberosKeytabEntry entry, uint timestamp)
    {
        List<byte> record = [];
        AppendUInt16(record, (ushort)entry.Principal.Components.Count);
        AppendCounted(record, Encoding.UTF8.GetBytes(entry.Principal.Realm));
        foreach (string component in entry.Principal.Components)
        {
            AppendCounted(record, Encoding.UTF8.GetBytes(component));
        }

        AppendUInt32(record, (uint)(entry.Principal.Components.Count == 1 ? KerberosDerWriter.PrincipalNameType : KerberosDerWriter.ServiceInstanceNameType));
        AppendUInt32(record, timestamp);
        record.Add((byte)entry.KeyVersionNumber);
        AppendUInt16(record, (ushort)entry.EncryptionType);
        AppendCounted(record, entry.Key.ToArray());
        AppendUInt32(record, entry.KeyVersionNumber);
        return [.. record];
    }

    private static void AppendCounted(List<byte> bytes, byte[] value)
    {
        AppendUInt16(bytes, (ushort)value.Length);
        bytes.AddRange(value);
    }

    private static void AppendUInt16(List<byte> bytes, ushort value)
    {
        Span<byte> buffer = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(buffer, value);
        bytes.AddRange(buffer);
    }

    private static void AppendUInt32(List<byte> bytes, uint value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(buffer, value);
        bytes.AddRange(buffer);
    }
}
