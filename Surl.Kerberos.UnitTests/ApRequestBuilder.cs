using System.Formats.Asn1;
using System.Text;

namespace Surl.Kerberos;

/// <summary>
/// Makes a client's Kerberos <c>InitialContextToken</c> by hand, as ADR-0057 decision 11 has it:
/// the <c>EncTicketPart</c>, the <c>Authenticator</c> and the <c>AP-REQ</c> written as RFC 4120
/// section 5 DER, encrypted with the vector-checked profiles under a fixed service key, a fixed
/// session key and a fixed confounder, and framed as RFC 1964 section 1.1 frames it. Every
/// property is a field a test may change to make one check fail.
/// </summary>
internal sealed record ApRequestBuilder
{
    public const string KerberosOid = "1.2.840.113554.1.2.2";
    public const string MicrosoftKerberosOid = "1.2.840.48018.1.2.2";
    public const string Realm = "EXAMPLE.COM";
    public const string Host = "web01.example.com";

    /// <summary>The time the tests' clock starts at, and the authenticator's <c>ctime</c> by default.</summary>
    public static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    public static readonly byte[] Confounder = Convert.FromHexString("c0c1c2c3c4c5c6c7c8c9cacbcccdcecf");

    public KerberosEncryptionType TicketEncryptionType { get; init; } = KerberosEncryptionType.Aes256CtsHmacSha196;

    /// <summary>The <c>enc-part.etype</c> the ticket claims; the ticket's enctype when <see langword="null" />.</summary>
    public int? TicketEncryptionTypeNumber { get; init; }

    public uint? TicketKeyVersionNumber { get; init; } = 3;

    /// <summary>The key the ticket is encrypted under; the keytab's key when <see langword="null" />.</summary>
    public byte[]? TicketKey { get; init; }

    public string ServerRealm { get; init; } = Realm;

    public string[] ServerName { get; init; } = ["HTTP", Host];

    public int ProtocolVersion { get; init; } = 5;

    public int MessageType { get; init; } = 14;

    public int TicketVersion { get; init; } = 5;

    public bool UseSessionKey { get; init; }

    public bool MutualRequired { get; init; } = true;

    public bool TicketInvalid { get; init; }

    /// <summary>The session key's enctype; the ticket's when <see langword="null" />.</summary>
    public int? SessionKeyTypeNumber { get; init; }

    /// <summary>The session key the ticket carries; <see cref="SessionKey" /> when <see langword="null" />, which still encrypts the authenticator.</summary>
    public byte[]? TicketSessionKeyValue { get; init; }

    public string ClientRealm { get; init; } = Realm;

    public string[] ClientName { get; init; } = ["user"];

    public DateTimeOffset AuthTime { get; init; } = Now.AddHours(-1);

    public DateTimeOffset? StartTime { get; init; } = Now.AddHours(-1);

    public DateTimeOffset EndTime { get; init; } = Now.AddHours(9);

    /// <summary>Whether the ticket carries <c>renew-till</c>, <c>caddr</c> and <c>authorization-data</c>, none of which surl checks.</summary>
    public bool TicketHasUncheckedFields { get; init; }

    public bool TamperTicket { get; init; }

    public int AuthenticatorVersion { get; init; } = 5;

    /// <summary>The authenticator's client realm; the ticket's when <see langword="null" />.</summary>
    public string? AuthenticatorClientRealm { get; init; }

    /// <summary>The authenticator's client name; the ticket's when <see langword="null" />.</summary>
    public string[]? AuthenticatorClientName { get; init; }

    /// <summary>The authenticator's <c>cksum.cksumtype</c>; no <c>cksum</c> at all when <see langword="null" />.</summary>
    public int? ChecksumType { get; init; } = 0x8003;

    public byte[] Checksum { get; init; } = GssApiChecksumBytes(flags: 0x3E);

    public DateTimeOffset ClientTime { get; init; } = Now;

    public int ClientMicroseconds { get; init; } = 123456;

    /// <summary>The authenticator's subkey enctype; no subkey when <see langword="null" />.</summary>
    public int? SubkeyTypeNumber { get; init; }

    public long? SequenceNumber { get; init; } = 0x01020304;

    public bool AuthenticatorHasAuthorizationData { get; init; }

    public string Oid { get; init; } = KerberosOid;

    public byte[] TrailingBytes { get; init; } = [];

    public KerberosEncryptionType SessionKeyType => (KerberosEncryptionType)(SessionKeyTypeNumber ?? (int)TicketEncryptionType);

    public byte[] SessionKey => KeyOf((int)SessionKeyType, 0x10);

    public byte[] Subkey => KeyOf(SubkeyTypeNumber!.Value, 0x50);

    /// <summary>The key the keytab holds for <paramref name="encryptionType" />.</summary>
    public static byte[] ServiceKeyOf(KerberosEncryptionType encryptionType) => KeyOf((int)encryptionType, 0x30);

    /// <summary>
    /// A keytab holding <c>HTTP/web01.example.com@EXAMPLE.COM</c> at key version 3 in all four
    /// enctypes.
    /// </summary>
    public static KerberosKeytab Keytab(string service = "HTTP") => new(Enum.GetValues<KerberosEncryptionType>().Select(encryptionType =>
        new KerberosKeytabEntry(new KerberosPrincipalName(Realm, [service, Host]), 3, encryptionType, ServiceKeyOf(encryptionType))));

    /// <summary>An RFC 4121 section 4.1.1 checksum with no delegation: <c>Lgth</c> 16, zero bindings, then the flags.</summary>
    public static byte[] GssApiChecksumBytes(uint flags)
    {
        byte[] checksum = new byte[24];
        checksum[0] = 16;
        BitConverter.TryWriteBytes(checksum.AsSpan(20), flags);
        return checksum;
    }

    /// <summary>Writes a <c>GeneralString</c>, which <see cref="AsnWriter" /> has no method for.</summary>
    public static void WriteGeneralString(AsnWriter writer, string value)
    {
        byte[] content = Encoding.UTF8.GetBytes(value);
        writer.WriteEncodedValue([0x1B, (byte)content.Length, .. content]);
    }

    /// <summary>Frames <paramref name="message" /> as <c>[APPLICATION 0]</c>, <paramref name="oid" /> and <paramref name="tokenId" />.</summary>
    public static byte[] Frame(string oid, byte[] tokenId, byte[] message)
    {
        AsnWriter oidWriter = new(AsnEncodingRules.DER);
        oidWriter.WriteObjectIdentifier(oid);
        byte[] content = [.. oidWriter.Encode(), .. tokenId, .. message];
        byte[] length = content.Length < 0x80
            ? [(byte)content.Length]
            : content.Length < 0x100 ? [0x81, (byte)content.Length] : [0x82, (byte)(content.Length >> 8), (byte)content.Length];
        return [0x60, .. length, .. content];
    }

    public byte[] Build()
    {
        byte[] ticketCipherText = KerberosEncryptionProfile.For(TicketEncryptionType)
            .Encrypt(TicketKey ?? ServiceKeyOf(TicketEncryptionType), 2, Confounder, WriteEncTicketPart());
        if (TamperTicket)
        {
            ticketCipherText[20] ^= 0x01;
        }

        byte[] authenticatorCipherText = KerberosEncryptionProfile.For(Enum.IsDefined(SessionKeyType) ? SessionKeyType : TicketEncryptionType).Encrypt(Enum.IsDefined(SessionKeyType) ? SessionKey : ServiceKeyOf(TicketEncryptionType), 11, Confounder, WriteAuthenticator());
        return [.. Frame(Oid, [0x01, 0x00], WriteApRequest(ticketCipherText, authenticatorCipherText)), .. TrailingBytes];
    }

    // A key of the enctype's length, or 16 bytes for an enctype surl does not accept.
    private static byte[] KeyOf(int encryptionTypeNumber, byte seed)
    {
        KerberosEncryptionType encryptionType = (KerberosEncryptionType)encryptionTypeNumber;
        byte[] key = new byte[Enum.IsDefined(encryptionType) ? KerberosEncryptionProfile.For(encryptionType).KeyLength : 16];
        for (int index = 0; index < key.Length; index++)
        {
            key[index] = (byte)(seed + index);
        }

        return key;
    }

    private static void WriteField(AsnWriter writer, int number, Action write)
    {
        using (writer.PushSequence(new Asn1Tag(TagClass.ContextSpecific, number, isConstructed: true)))
        {
            write();
        }
    }

    private static void WritePrincipalName(AsnWriter writer, int number, string[] components)
    {
        WriteField(writer, number, () =>
        {
            using (writer.PushSequence())
            {
                WriteField(writer, 0, () => writer.WriteInteger(1));
                WriteField(writer, 1, () =>
                {
                    using (writer.PushSequence())
                    {
                        foreach (string component in components)
                        {
                            WriteGeneralString(writer, component);
                        }
                    }
                });
            }
        });
    }

    private static void WriteEncryptionKey(AsnWriter writer, int number, int keyType, byte[] key)
    {
        WriteField(writer, number, () =>
        {
            using (writer.PushSequence())
            {
                WriteField(writer, 0, () => writer.WriteInteger(keyType));
                WriteField(writer, 1, () => writer.WriteOctetString(key));
            }
        });
    }

    private static void WriteEncryptedData(AsnWriter writer, int number, int encryptionType, uint? keyVersionNumber, byte[] cipherText)
    {
        WriteField(writer, number, () =>
        {
            using (writer.PushSequence())
            {
                WriteField(writer, 0, () => writer.WriteInteger(encryptionType));
                if (keyVersionNumber is { } kvno)
                {
                    WriteField(writer, 1, () => writer.WriteInteger(kvno));
                }

                WriteField(writer, 2, () => writer.WriteOctetString(cipherText));
            }
        });
    }

    private static void WriteTime(AsnWriter writer, int number, DateTimeOffset time) =>
        WriteField(writer, number, () => writer.WriteGeneralizedTime(time, omitFractionalSeconds: true));

    private byte[] WriteEncTicketPart()
    {
        AsnWriter writer = new(AsnEncodingRules.DER);
        using (writer.PushSequence(new Asn1Tag(TagClass.Application, 3, isConstructed: true)))
        using (writer.PushSequence())
        {
            WriteField(writer, 0, () => writer.WriteBitString([(byte)(TicketInvalid ? 0x41 : 0x40), 0x00, 0x00, 0x00]));
            WriteEncryptionKey(writer, 1, (int)SessionKeyType, TicketSessionKeyValue ?? SessionKey);
            WriteField(writer, 2, () => WriteGeneralString(writer, ClientRealm));
            WritePrincipalName(writer, 3, ClientName);
            WriteField(writer, 4, () =>
            {
                using (writer.PushSequence())
                {
                    WriteField(writer, 0, () => writer.WriteInteger(1));
                    WriteField(writer, 1, () => writer.WriteOctetString([]));
                }
            });
            WriteTime(writer, 5, AuthTime);
            if (StartTime is { } startTime)
            {
                WriteTime(writer, 6, startTime);
            }

            WriteTime(writer, 7, EndTime);
            if (TicketHasUncheckedFields)
            {
                WriteTime(writer, 8, EndTime.AddDays(7));
                WriteField(writer, 9, () =>
                {
                    using (writer.PushSequence())
                    {
                    }
                });
                WriteField(writer, 10, () =>
                {
                    using (writer.PushSequence())
                    {
                    }
                });
            }
        }

        return writer.Encode();
    }

    private byte[] WriteAuthenticator()
    {
        AsnWriter writer = new(AsnEncodingRules.DER);
        using (writer.PushSequence(new Asn1Tag(TagClass.Application, 2, isConstructed: true)))
        using (writer.PushSequence())
        {
            WriteField(writer, 0, () => writer.WriteInteger(AuthenticatorVersion));
            WriteField(writer, 1, () => WriteGeneralString(writer, AuthenticatorClientRealm ?? ClientRealm));
            WritePrincipalName(writer, 2, AuthenticatorClientName ?? ClientName);
            if (ChecksumType is { } checksumType)
            {
                WriteField(writer, 3, () =>
                {
                    using (writer.PushSequence())
                    {
                        WriteField(writer, 0, () => writer.WriteInteger(checksumType));
                        WriteField(writer, 1, () => writer.WriteOctetString(Checksum));
                    }
                });
            }

            WriteField(writer, 4, () => writer.WriteInteger(ClientMicroseconds));
            WriteTime(writer, 5, ClientTime);
            if (SubkeyTypeNumber is { } subkeyType)
            {
                WriteEncryptionKey(writer, 6, subkeyType, Subkey);
            }

            if (SequenceNumber is { } sequenceNumber)
            {
                WriteField(writer, 7, () => writer.WriteInteger(sequenceNumber));
            }

            if (AuthenticatorHasAuthorizationData)
            {
                WriteField(writer, 8, () =>
                {
                    using (writer.PushSequence())
                    {
                    }
                });
            }
        }

        return writer.Encode();
    }

    private byte[] WriteApRequest(byte[] ticketCipherText, byte[] authenticatorCipherText)
    {
        AsnWriter writer = new(AsnEncodingRules.DER);
        using (writer.PushSequence(new Asn1Tag(TagClass.Application, 14, isConstructed: true)))
        using (writer.PushSequence())
        {
            WriteField(writer, 0, () => writer.WriteInteger(ProtocolVersion));
            WriteField(writer, 1, () => writer.WriteInteger(MessageType));
            byte apOptions = (byte)((UseSessionKey ? 0x40 : 0) | (MutualRequired ? 0x20 : 0));
            WriteField(writer, 2, () => writer.WriteBitString([apOptions, 0x00, 0x00, 0x00]));
            WriteField(writer, 3, () =>
            {
                using (writer.PushSequence(new Asn1Tag(TagClass.Application, 1, isConstructed: true)))
                using (writer.PushSequence())
                {
                    WriteField(writer, 0, () => writer.WriteInteger(TicketVersion));
                    WriteField(writer, 1, () => WriteGeneralString(writer, ServerRealm));
                    WritePrincipalName(writer, 2, ServerName);
                    WriteEncryptedData(writer, 3, TicketEncryptionTypeNumber ?? (int)TicketEncryptionType, TicketKeyVersionNumber, ticketCipherText);
                }
            });
            WriteEncryptedData(writer, 4, (int)SessionKeyType, null, authenticatorCipherText);
        }

        return writer.Encode();
    }
}
