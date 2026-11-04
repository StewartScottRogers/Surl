using System.Formats.Asn1;

namespace Surl.Kerberos.TestKdc;

/// <summary>
/// The client side of the tests, by hand: writes AS-REQs, TGS-REQs, <c>PA-ENC-TIMESTAMP</c>s and
/// AP-REQs as RFC 4120 section 5 DER, and reads the KDC's replies and errors back. Every
/// parameter is a field a test may change to make one check fail.
/// </summary>
internal static class KdcClient
{
    public const string Password = "surl-test-password";
    public const string UserName = "tester";
    public const string HttpService = "HTTP/web.surl.test";
    public const uint Nonce = 0x7EADBEEF;

    public static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    public static readonly DateTimeOffset FarTill = new(2037, 9, 13, 2, 48, 5, TimeSpan.Zero);

    public static readonly string[] TicketGrantingService = ["krbtgt", KerberosTestKdc.Realm];

    public static readonly int[] Aes = [18, 17];

    private static readonly byte[] Confounder = Convert.FromHexString("c0c1c2c3c4c5c6c7c8c9cacbcccdcecf");

    public static KerberosTestKdc NewKdc(SettableTimeProvider clock, params string[] services) =>
        new(UserName, Password, services.Length == 0 ? [HttpService] : services, clock, new SeededRandomSource());

    public static byte[] AsRequest(
        int[] encryptionTypes,
        IEnumerable<(int Type, byte[] Value)>? preAuthenticationData = null,
        string[]? clientName = null,
        string[]? serverName = null,
        bool omitClientName = false,
        bool omitServerName = false,
        DateTimeOffset? till = null) =>
        KdcRequest(10, 10, preAuthenticationData, omitClientName ? null : clientName ?? [UserName], omitServerName ? null : serverName ?? TicketGrantingService, encryptionTypes, till ?? FarTill);

    public static byte[] TgsRequest(byte[]? apRequest, string[]? serverName, int[] encryptionTypes, DateTimeOffset? till = null) =>
        KdcRequest(12, 12, apRequest is null ? [] : [(1, apRequest)], null, serverName, encryptionTypes, till ?? FarTill);

    public static byte[] KdcRequest(
        int applicationTag,
        int messageType,
        IEnumerable<(int Type, byte[] Value)>? preAuthenticationData,
        string[]? clientName,
        string[]? serverName,
        int[] encryptionTypes,
        DateTimeOffset till,
        bool omitNonce = false,
        long? extraEncryptionType = null,
        bool withSkippedFields = false)
    {
        AsnWriter writer = KerberosDerWriter.NewWriter();
        using (writer.PushSequence(KerberosDer.ApplicationTag(applicationTag)))
        using (writer.PushSequence())
        {
            KerberosDerWriter.WriteIntegerField(writer, 1, 5);
            KerberosDerWriter.WriteIntegerField(writer, 2, messageType);
            if (preAuthenticationData is not null)
            {
                using AsnWriter.Scope field = KerberosDerWriter.PushField(writer, 3);
                writer.WriteEncodedValue(KerberosKdcMessages.WriteMethodData(preAuthenticationData));
            }

            using AsnWriter.Scope bodyField = KerberosDerWriter.PushField(writer, 4);
            using AsnWriter.Scope body = writer.PushSequence();
            KerberosDerWriter.WriteFlagsField(writer, 0, 0x40810010);
            if (clientName is not null)
            {
                KerberosDerWriter.WritePrincipalNameField(writer, 1, new KerberosPrincipalName(KerberosTestKdc.Realm, clientName));
            }

            KerberosDerWriter.WriteStringField(writer, 2, KerberosTestKdc.Realm);
            if (serverName is not null)
            {
                KerberosDerWriter.WritePrincipalNameField(writer, 3, new KerberosPrincipalName(KerberosTestKdc.Realm, serverName));
            }

            if (withSkippedFields)
            {
                KerberosDerWriter.WriteTimeField(writer, 4, Now);
            }

            KerberosDerWriter.WriteTimeField(writer, 5, till);
            if (withSkippedFields)
            {
                KerberosDerWriter.WriteTimeField(writer, 6, till);
            }

            if (!omitNonce)
            {
                KerberosDerWriter.WriteIntegerField(writer, 7, Nonce);
            }

            using (KerberosDerWriter.PushField(writer, 8))
            using (writer.PushSequence())
            {
                foreach (int encryptionType in encryptionTypes)
                {
                    writer.WriteInteger(encryptionType);
                }

                if (extraEncryptionType is long extra)
                {
                    writer.WriteInteger(extra);
                }
            }

            if (withSkippedFields)
            {
                KerberosDerWriter.WriteOctetStringField(writer, 9, [0x30, 0x00]);
                KerberosDerWriter.WriteOctetStringField(writer, 10, [0x30, 0x00]);
                KerberosDerWriter.WriteOctetStringField(writer, 11, [0x30, 0x00]);
            }
        }

        return writer.Encode();
    }

    /// <summary>A <c>PA-ENC-TIMESTAMP</c>: <c>PA-ENC-TS-ENC</c> encrypted under key usage 1.</summary>
    public static (int Type, byte[] Value) EncryptedTimestamp(KerberosEncryptionType encryptionType, ReadOnlySpan<byte> key, DateTimeOffset time, int? claimedEncryptionType = null)
    {
        AsnWriter timestamp = KerberosDerWriter.NewWriter();
        using (timestamp.PushSequence())
        {
            KerberosDerWriter.WriteTimeField(timestamp, 0, time);
            KerberosDerWriter.WriteIntegerField(timestamp, 1, 123456);
        }

        byte[] cipherText = KerberosEncryptionProfile.For(encryptionType).Encrypt(key, 1, Confounder, timestamp.Encode());
        return (2, WriteEncryptedData(claimedEncryptionType ?? (int)encryptionType, cipherText));
    }

    public static byte[] WriteEncryptedData(int encryptionType, byte[] cipherText)
    {
        AsnWriter writer = KerberosDerWriter.NewWriter();
        using (writer.PushSequence())
        {
            KerberosDerWriter.WriteIntegerField(writer, 0, encryptionType);
            KerberosDerWriter.WriteOctetStringField(writer, 2, cipherText);
        }

        return writer.Encode();
    }

    /// <summary>
    /// An AP-REQ carrying <paramref name="ticket" /> and an authenticator for
    /// <paramref name="clientName" /> encrypted under the session key with
    /// <paramref name="keyUsage" /> (7 inside a TGS-REQ, 11 inside a GSS-API token).
    /// </summary>
    public static byte[] ApRequest(
        byte[] ticket,
        KerberosEncryptionType sessionKeyType,
        byte[] sessionKey,
        int keyUsage,
        string[] clientName,
        DateTimeOffset clientTime,
        (int Type, byte[] Key)? subkey = null,
        bool withGssApiChecksum = false)
    {
        AsnWriter authenticator = KerberosDerWriter.NewWriter();
        using (authenticator.PushSequence(KerberosDer.ApplicationTag(2)))
        using (authenticator.PushSequence())
        {
            KerberosDerWriter.WriteIntegerField(authenticator, 0, 5);
            KerberosDerWriter.WriteStringField(authenticator, 1, KerberosTestKdc.Realm);
            KerberosDerWriter.WritePrincipalNameField(authenticator, 2, new KerberosPrincipalName(KerberosTestKdc.Realm, clientName));
            if (withGssApiChecksum)
            {
                using AsnWriter.Scope field = KerberosDerWriter.PushField(authenticator, 3);
                using AsnWriter.Scope checksum = authenticator.PushSequence();
                KerberosDerWriter.WriteIntegerField(authenticator, 0, 0x8003);
                byte[] value = new byte[24];
                value[0] = 16;
                value[20] = 0x3E;
                KerberosDerWriter.WriteOctetStringField(authenticator, 1, value);
            }

            KerberosDerWriter.WriteIntegerField(authenticator, 4, 0);
            KerberosDerWriter.WriteTimeField(authenticator, 5, clientTime);
            if (subkey is { } key)
            {
                using AsnWriter.Scope field = KerberosDerWriter.PushField(authenticator, 6);
                using AsnWriter.Scope encryptionKey = authenticator.PushSequence();
                KerberosDerWriter.WriteIntegerField(authenticator, 0, key.Type);
                KerberosDerWriter.WriteOctetStringField(authenticator, 1, key.Key);
            }
        }

        byte[] encryptedAuthenticator = KerberosEncryptionProfile.For(sessionKeyType).Encrypt(sessionKey, keyUsage, Confounder, authenticator.Encode());
        AsnWriter writer = KerberosDerWriter.NewWriter();
        using (writer.PushSequence(KerberosDer.ApplicationTag(14)))
        using (writer.PushSequence())
        {
            KerberosDerWriter.WriteIntegerField(writer, 0, 5);
            KerberosDerWriter.WriteIntegerField(writer, 1, 14);
            KerberosDerWriter.WriteFlagsField(writer, 2, 0x20000000);
            using (KerberosDerWriter.PushField(writer, 3))
            {
                writer.WriteEncodedValue(ticket);
            }

            KerberosDerWriter.WriteEncryptedDataField(writer, 4, sessionKeyType, null, encryptedAuthenticator);
        }

        return writer.Encode();
    }

    /// <summary>Sends an AS-REQ with a good <c>PA-ENC-TIMESTAMP</c> and reads the AS-REP.</summary>
    public static (KdcReply Reply, EncKdcReplyPart Part) LogIn(KerberosTestKdc kdc, int[]? encryptionTypes = null)
    {
        encryptionTypes ??= Aes;
        KerberosEncryptionType chosen = (KerberosEncryptionType)encryptionTypes[0];
        byte[] request = AsRequest(encryptionTypes, [EncryptedTimestamp(chosen, kdc.GetUserKey(chosen).Span, Now)]);
        KdcReply reply = KdcReply.Read(kdc.Answer(request));
        return (reply, reply.Open(kdc.GetUserKey(chosen).Span, 3));
    }

    /// <summary>Logs in, then asks for a ticket to <paramref name="serverName" /> with the TGT.</summary>
    public static byte[] AskForServiceTicket(
        KerberosTestKdc kdc,
        string[] serverName,
        int[]? encryptionTypes = null,
        (int Type, byte[] Key)? subkey = null,
        DateTimeOffset? till = null)
    {
        (KdcReply asReply, EncKdcReplyPart asPart) = LogIn(kdc);
        byte[] apRequest = ApRequest(asReply.Ticket, asPart.KeyType, asPart.Key, 7, [UserName], Now, subkey);
        return kdc.Answer(TgsRequest(apRequest, serverName, encryptionTypes ?? Aes, till));
    }
}

/// <summary>An AS-REP or TGS-REP, read back.</summary>
internal sealed record KdcReply(int MessageType, IReadOnlyList<(int Type, byte[] Value)> PreAuthenticationData, KerberosPrincipalName Client, byte[] Ticket, int ReplyEncryptionType, byte[] EncryptedPart)
{
    public static KdcReply Read(byte[] bytes)
    {
        int messageType = new AsnReader(bytes, KerberosDer.Rules).PeekTag().TagValue;
        AsnReader fields = KerberosDer.ReadApplicationSequence(bytes, messageType);
        Assert.AreEqual(5, KerberosDer.ReadInt32Field(fields, 0));
        Assert.AreEqual(messageType, KerberosDer.ReadInt32Field(fields, 1));
        List<(int Type, byte[] Value)> preAuthenticationData = KerberosDer.HasField(fields, 2) ? ReadMethodData(KerberosDer.ReadField(fields, 2)) : [];
        string realm = KerberosDer.ReadStringField(fields, 3);
        KerberosPrincipalName client = KerberosDer.ReadPrincipalNameField(fields, 4, realm);
        byte[] ticket = KerberosDer.ReadField(fields, 5).ReadEncodedValue().ToArray();
        KerberosEncryptedData encryptedPart = KerberosEncryptedData.ReadField(fields, 6);
        fields.ThrowIfNotEmpty();
        return new KdcReply(messageType, preAuthenticationData, client, ticket, encryptedPart.EncryptionTypeNumber, encryptedPart.CipherText);
    }

    public static List<(int Type, byte[] Value)> ReadMethodData(AsnReader container)
    {
        AsnReader sequence = container.ReadSequence();
        List<(int Type, byte[] Value)> preAuthenticationData = [];
        while (sequence.HasData)
        {
            AsnReader paData = sequence.ReadSequence();
            preAuthenticationData.Add((KerberosDer.ReadInt32Field(paData, 1), KerberosDer.ReadOctetStringField(paData, 2)));
        }

        return preAuthenticationData;
    }

    /// <summary>Reads the ticket's <c>sname</c> and <c>enc-part</c>.</summary>
    public (KerberosPrincipalName Server, KerberosEncryptedData EncryptedPart) ReadTicket()
    {
        AsnReader fields = KerberosDer.ReadApplicationSequence(Ticket, 1);
        Assert.AreEqual(5, KerberosDer.ReadInt32Field(fields, 0));
        string realm = KerberosDer.ReadStringField(fields, 1);
        KerberosPrincipalName server = KerberosDer.ReadPrincipalNameField(fields, 2, realm);
        return (server, KerberosEncryptedData.ReadField(fields, 3));
    }

    /// <summary>Decrypts the ticket with <paramref name="serverKey" /> and reads its <c>EncTicketPart</c>.</summary>
    public KerberosTicketPart OpenTicket(ReadOnlySpan<byte> serverKey)
    {
        KerberosEncryptedData encryptedPart = ReadTicket().EncryptedPart;
        Assert.IsTrue(KerberosEncryptionProfile.For((KerberosEncryptionType)encryptedPart.EncryptionTypeNumber)
            .TryDecrypt(serverKey, 2, encryptedPart.CipherText, out byte[] plainText));
        return KerberosTicketPart.Read(plainText);
    }

    /// <summary>Decrypts the reply's <c>enc-part</c> with <paramref name="key" /> under <paramref name="keyUsage" />.</summary>
    public EncKdcReplyPart Open(ReadOnlySpan<byte> key, int keyUsage)
    {
        Assert.IsTrue(KerberosEncryptionProfile.For((KerberosEncryptionType)ReplyEncryptionType)
            .TryDecrypt(key, keyUsage, EncryptedPart, out byte[] plainText));
        return EncKdcReplyPart.Read(plainText);
    }
}

/// <summary>An <c>EncASRepPart</c> or <c>EncTGSRepPart</c>, read back.</summary>
internal sealed record EncKdcReplyPart(int ApplicationTag, KerberosEncryptionType KeyType, byte[] Key, uint Nonce, byte[] Flags, DateTimeOffset AuthTime, DateTimeOffset StartTime, DateTimeOffset EndTime, KerberosPrincipalName Server)
{
    public static EncKdcReplyPart Read(byte[] bytes)
    {
        int applicationTag = new AsnReader(bytes, KerberosDer.Rules).PeekTag().TagValue;
        AsnReader fields = KerberosDer.ReadApplicationSequence(bytes, applicationTag);
        KerberosEncryptionKey key = KerberosEncryptionKey.ReadField(fields, 0);
        KerberosDer.ReadField(fields, 1);
        uint nonce = KerberosDer.ReadOptionalUInt32Field(fields, 2)!.Value;
        byte[] flags = KerberosDer.ReadFlagsField(fields, 4);
        DateTimeOffset authTime = KerberosDer.ReadTimeField(fields, 5);
        DateTimeOffset startTime = KerberosDer.ReadTimeField(fields, 6);
        DateTimeOffset endTime = KerberosDer.ReadTimeField(fields, 7);
        string realm = KerberosDer.ReadStringField(fields, 9);
        KerberosPrincipalName server = KerberosDer.ReadPrincipalNameField(fields, 10, realm);
        fields.ThrowIfNotEmpty();
        return new EncKdcReplyPart(applicationTag, (KerberosEncryptionType)key.KeyTypeNumber, key.KeyValue, nonce, flags, authTime, startTime, endTime, server);
    }
}

/// <summary>A <c>KRB-ERROR</c>, read back.</summary>
internal sealed record KdcError(KerberosErrorCode ErrorCode, string Text, KerberosPrincipalName? Client, KerberosPrincipalName Server, byte[]? ErrorData)
{
    public static KdcError Read(byte[] bytes)
    {
        AsnReader fields = KerberosDer.ReadApplicationSequence(bytes, 30);
        Assert.AreEqual(5, KerberosDer.ReadInt32Field(fields, 0));
        Assert.AreEqual(30, KerberosDer.ReadInt32Field(fields, 1));
        KerberosDer.ReadTimeField(fields, 4);
        KerberosDer.ReadInt32Field(fields, 5);
        KerberosErrorCode errorCode = (KerberosErrorCode)KerberosDer.ReadInt32Field(fields, 6);
        KerberosPrincipalName? client = null;
        if (KerberosDer.HasField(fields, 7))
        {
            string clientRealm = KerberosDer.ReadStringField(fields, 7);
            client = KerberosDer.ReadPrincipalNameField(fields, 8, clientRealm);
        }

        string realm = KerberosDer.ReadStringField(fields, 9);
        KerberosPrincipalName server = KerberosDer.ReadPrincipalNameField(fields, 10, realm);
        string text = KerberosDer.ReadStringField(fields, 11);
        byte[]? errorData = KerberosDer.HasField(fields, 12) ? KerberosDer.ReadOctetStringField(fields, 12) : null;
        fields.ThrowIfNotEmpty();
        return new KdcError(errorCode, text, client, server, errorData);
    }
}
