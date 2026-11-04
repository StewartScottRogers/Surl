using System.Formats.Asn1;

namespace Surl.Kerberos.TestKdc;

/// <summary>
/// A hand-built Kerberos KDC for the realm <c>SURL.TEST</c>, the loopback test fixture of
/// ADR-0065 decision 1: it answers the AS exchange with <c>PA-ENC-TIMESTAMP</c>
/// pre-authentication and the TGS exchange (RFC 4120 sections 3.1 and 3.3) for one user and the
/// service principals a test names, issuing AES tickets (enctypes 17, 18, 19 and 20, in the
/// client's order), and writes the service keys as an MIT keytab for <c>surl --keytab</c>. A
/// request it cannot or will not answer gets the RFC 4120 <c>KRB-ERROR</c> for it, never an
/// exception. <see cref="KerberosTestKdcServer" /> carries it over UDP and TCP.
/// </summary>
public sealed class KerberosTestKdc
{
    /// <summary>The realm the KDC serves.</summary>
    public const string Realm = "SURL.TEST";

    /// <summary>The longest request the KDC reads, 64 KiB; a longer one is refused <see cref="KerberosErrorCode.FieldTooLong" />.</summary>
    public const int MaximumRequestLength = 65536;

    /// <summary>The key version number of every service and ticket-granting key.</summary>
    public const uint KeyVersionNumber = 1;

    private const int TicketKeyUsage = 2;
    private const int EncryptedTimestampKeyUsage = 1;
    private const int AsReplyKeyUsage = 3;
    private const int TgsAuthenticatorKeyUsage = 7;
    private const int TgsReplySessionKeyUsage = 8;
    private const int TgsReplySubkeyUsage = 9;
    private const int TgsRequestPreAuthenticationType = 1;
    private const int InitialFlagBit = 9;
    private const int PreAuthenticatedFlagBit = 10;

    private readonly TimeProvider timeProvider;
    private readonly IKerberosRandomSource randomSource;
    private readonly Dictionary<KerberosEncryptionType, byte[]> userKeys;
    private readonly Dictionary<KerberosPrincipalName, Dictionary<KerberosEncryptionType, byte[]>> serverKeys = [];

    /// <summary>
    /// Makes the KDC: the user's keys from <paramref name="password" /> by string-to-key with the
    /// default salt, and one random key per enctype for <c>krbtgt/SURL.TEST</c> and for each
    /// service principal, drawn from <paramref name="randomSource" />.
    /// </summary>
    /// <param name="userName">The one user principal's name, such as <c>tester</c> for <c>tester@SURL.TEST</c>.</param>
    /// <param name="password">The user's password.</param>
    /// <param name="servicePrincipals">The services, as their components joined by <c>/</c>, such as <c>HTTP/web.surl.test</c>.</param>
    /// <param name="timeProvider">The clock tickets and pre-authentication are issued and checked on.</param>
    /// <param name="randomSource">Where keys and confounders come from.</param>
    /// <exception cref="ArgumentException">A service principal has an empty component.</exception>
    public KerberosTestKdc(string userName, string password, IEnumerable<string> servicePrincipals, TimeProvider timeProvider, IKerberosRandomSource randomSource)
    {
        ArgumentNullException.ThrowIfNull(userName);
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(servicePrincipals);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(randomSource);
        this.timeProvider = timeProvider;
        this.randomSource = randomSource;
        UserPrincipal = new KerberosPrincipalName(Realm, [userName]);
        UserSalt = KerberosStringToKey.DefaultSalt(UserPrincipal);
        userKeys = Enum.GetValues<KerberosEncryptionType>().ToDictionary(
            encryptionType => encryptionType,
            encryptionType => KerberosStringToKey.DeriveKey(encryptionType, password, UserSalt));
        ServicePrincipals = [.. servicePrincipals.Select(ParseServicePrincipal)];
        foreach (KerberosPrincipalName server in ServicePrincipals.Prepend(TicketGrantingPrincipal))
        {
            serverKeys[server] = Enum.GetValues<KerberosEncryptionType>().ToDictionary(
                encryptionType => encryptionType,
                encryptionType => DrawKey(encryptionType));
        }
    }

    /// <summary>Gets the ticket-granting service, <c>krbtgt/SURL.TEST@SURL.TEST</c>.</summary>
    public static KerberosPrincipalName TicketGrantingPrincipal { get; } = new(Realm, ["krbtgt", Realm]);

    /// <summary>Gets the longest a ticket lives: 10 hours, MIT krb5's default.</summary>
    public static TimeSpan MaximumTicketLifetime { get; } = TimeSpan.FromHours(10);

    /// <summary>Gets the one user principal.</summary>
    public KerberosPrincipalName UserPrincipal { get; }

    /// <summary>Gets the user's salt, RFC 4120's default: the realm, then the name, as in <c>SURL.TESTtester</c>.</summary>
    public string UserSalt { get; }

    /// <summary>Gets the service principals, in the order the test named them.</summary>
    public IReadOnlyList<KerberosPrincipalName> ServicePrincipals { get; }

    /// <summary>
    /// Answers one request: an AS-REQ with an AS-REP, a TGS-REQ with a TGS-REP, and anything the
    /// KDC refuses with a <c>KRB-ERROR</c>.
    /// </summary>
    /// <param name="request">The DER of the request, without any transport framing.</param>
    /// <returns>The DER of the answer.</returns>
    public byte[] Answer(ReadOnlySpan<byte> request)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        KerberosKdcRequest? kdcRequest = null;
        try
        {
            if (request.Length > MaximumRequestLength)
            {
                throw new KerberosKdcRefusalException(KerberosErrorCode.FieldTooLong, "request longer than 64 KiB");
            }

            kdcRequest = KerberosKdcRequest.Read(request.ToArray());
            return kdcRequest.IsTicketGrantingRequest ? AnswerTicketGrantingRequest(kdcRequest, now) : AnswerAuthenticationRequest(kdcRequest, now);
        }
        catch (KerberosKdcRefusalException refusal)
        {
            return KerberosKdcMessages.WriteError(now, refusal, kdcRequest?.ClientName, kdcRequest?.ServerName ?? TicketGrantingPrincipal);
        }
        catch (KerberosRefusalException refusal)
        {
            // Surl.Kerberos refuses only a key of an enctype it does not accept: here, a TGS-REQ
            // authenticator's subkey in rc4-hmac or another non-AES enctype.
            return Refuse(KerberosErrorCode.EncryptionTypeNotSupported, refusal.Reason);
        }
        catch (AsnContentException)
        {
            return Refuse(KerberosErrorCode.Generic, "malformed request");
        }
    }

    /// <summary>Writes the service principals' keys, every enctype of each, as an MIT keytab.</summary>
    /// <returns>The keytab file's bytes, which <see cref="KerberosKeytab.Read" /> reads.</returns>
    public byte[] WriteServiceKeytab() => MitKeytabWriter.Write(
        ServicePrincipals.SelectMany(server => serverKeys[server].Select(key =>
            new KerberosKeytabEntry(server, KeyVersionNumber, key.Key, key.Value))),
        (uint)timeProvider.GetUtcNow().ToUnixTimeSeconds());

    /// <summary>Gets the key <paramref name="server" /> holds in <paramref name="encryptionType" />.</summary>
    /// <param name="server">A service principal or <see cref="TicketGrantingPrincipal" />.</param>
    /// <param name="encryptionType">The enctype.</param>
    /// <returns>The key, under which tickets to <paramref name="server" /> are encrypted.</returns>
    /// <exception cref="KeyNotFoundException">The KDC does not know <paramref name="server" />.</exception>
    public ReadOnlyMemory<byte> GetServerKey(KerberosPrincipalName server, KerberosEncryptionType encryptionType) => serverKeys[server][encryptionType];

    /// <summary>Gets the user's key in <paramref name="encryptionType" />, as the client derives it from the password.</summary>
    /// <param name="encryptionType">The enctype.</param>
    /// <returns>The key.</returns>
    public ReadOnlyMemory<byte> GetUserKey(KerberosEncryptionType encryptionType) => userKeys[encryptionType];

    /// <summary>Writes a <c>KRB-ERROR</c> for a request the transport refused before the KDC read it.</summary>
    /// <param name="errorCode">The error.</param>
    /// <param name="text">The <c>e-text</c>.</param>
    /// <returns>The DER of the error.</returns>
    internal byte[] Refuse(KerberosErrorCode errorCode, string text) =>
        KerberosKdcMessages.WriteError(timeProvider.GetUtcNow(), new KerberosKdcRefusalException(errorCode, text), null, TicketGrantingPrincipal);

    private static KerberosPrincipalName ParseServicePrincipal(string servicePrincipal)
    {
        string[] components = servicePrincipal.Split('/');
        if (components.Any(string.IsNullOrEmpty))
        {
            throw new ArgumentException($"The service principal '{servicePrincipal}' has an empty component.", nameof(servicePrincipal));
        }

        return new KerberosPrincipalName(Realm, components);
    }

    // A one-component name holding '@' is an enterprise name (RFC 6806), as Windows sends for -u tester@SURL.TEST.
    private static KerberosPrincipalName Canonicalize(KerberosPrincipalName client)
    {
        int at = client.Components[0].LastIndexOf('@');
        return client.Components.Count == 1 && at > 0
            ? new KerberosPrincipalName(client.Components[0][(at + 1)..], [client.Components[0][..at]])
            : client;
    }

    private static List<KerberosEncryptionType> AcceptedEncryptionTypes(KerberosKdcRequest request) =>
        [.. request.EncryptionTypeNumbers.Where(IsAccepted).Distinct().Cast<KerberosEncryptionType>()];

    private static bool IsAccepted(int encryptionTypeNumber) => Enum.IsDefined((KerberosEncryptionType)encryptionTypeNumber);

    private static KerberosEncryptionType RequireAccepted(int encryptionTypeNumber) =>
        IsAccepted(encryptionTypeNumber)
            ? (KerberosEncryptionType)encryptionTypeNumber
            : throw new KerberosKdcRefusalException(KerberosErrorCode.EncryptionTypeNotSupported, $"enctype {encryptionTypeNumber} not supported");

    private static KerberosEncryptionType ChooseEncryptionType(KerberosKdcRequest request) =>
        AcceptedEncryptionTypes(request) is [KerberosEncryptionType first, ..]
            ? first
            : throw new KerberosKdcRefusalException(KerberosErrorCode.EncryptionTypeNotSupported, "no requested enctype is supported");

    private static KerberosPrincipalName RequireServerName(KerberosKdcRequest request) =>
        request.ServerName ?? throw new AsnContentException("The request names no server.");

    private static byte[] Decrypt(KerberosEncryptionProfile profile, ReadOnlySpan<byte> key, int keyUsage, byte[] cipherText, KerberosErrorCode errorCode) =>
        profile.TryDecrypt(key, keyUsage, cipherText, out byte[] plainText)
            ? plainText
            : throw new KerberosKdcRefusalException(errorCode, "integrity check failed");

    private static DateTimeOffset ReadEncryptedTimestamp(byte[] plainText)
    {
        // PA-ENC-TS-ENC ::= SEQUENCE { patimestamp [0] KerberosTime, pausec [1] Microseconds OPTIONAL }
        AsnReader outer = new(plainText, KerberosDer.Rules);
        AsnReader fields = outer.ReadSequence();
        outer.ThrowIfNotEmpty();
        DateTimeOffset timestamp = KerberosDer.ReadTimeField(fields, 0);
        KerberosDer.SkipOptionalField(fields, 1);
        fields.ThrowIfNotEmpty();
        return timestamp;
    }

    private static KerberosEncryptedData ReadEncryptedData(byte[] encoded)
    {
        // EncryptedData ::= SEQUENCE { etype [0] Int32, kvno [1] UInt32 OPTIONAL, cipher [2] OCTET STRING }
        AsnReader outer = new(encoded, KerberosDer.Rules);
        AsnReader fields = outer.ReadSequence();
        outer.ThrowIfNotEmpty();
        int encryptionTypeNumber = KerberosDer.ReadInt32Field(fields, 0);
        uint? keyVersionNumber = KerberosDer.ReadOptionalUInt32Field(fields, 1);
        byte[] cipherText = KerberosDer.ReadOctetStringField(fields, 2);
        fields.ThrowIfNotEmpty();
        return new KerberosEncryptedData(encryptionTypeNumber, keyVersionNumber, cipherText);
    }

    private static byte[]? FindPreAuthenticationData(KerberosKdcRequest request, int type) =>
        request.PreAuthenticationData.FirstOrDefault(paData => paData.Type == type).Value;

    private byte[] AnswerAuthenticationRequest(KerberosKdcRequest request, DateTimeOffset now)
    {
        KerberosPrincipalName client = Canonicalize(request.ClientName ?? throw new AsnContentException("An AS-REQ names no client."));
        if (!client.Equals(UserPrincipal))
        {
            throw new KerberosKdcRefusalException(KerberosErrorCode.ClientPrincipalUnknown, $"client {client} unknown");
        }

        KerberosPrincipalName server = RequireServerName(request);
        Dictionary<KerberosEncryptionType, byte[]> keysOfServer = FindServerKeys(server);
        KerberosEncryptionType encryptionType = ChooseEncryptionType(request);
        CheckEncryptedTimestamp(request, now);
        KerberosIssuedTicket ticket = Issue(
            KerberosDerWriter.Flag(InitialFlagBit) | KerberosDerWriter.Flag(PreAuthenticatedFlagBit), encryptionType, client, server, now, now, request.Till, now + MaximumTicketLifetime);
        byte[] encryptedPart = Encrypt(
            KerberosEncryptionProfile.For(encryptionType), userKeys[encryptionType], AsReplyKeyUsage,
            KerberosKdcMessages.WriteEncKdcReplyPart(KerberosKdcMessages.EncAsReplyPartTag, ticket, request.Nonce));
        byte[] encryptionTypeInfo2 = KerberosKdcMessages.WriteEncryptionTypeInfo2([encryptionType], UserSalt);
        return KerberosKdcMessages.WriteKdcReply(
            KerberosKdcMessages.AsReplyMessageType,
            [(KerberosKdcMessages.EncryptionTypeInfo2PreAuthenticationType, encryptionTypeInfo2)],
            client,
            SealTicket(ticket, keysOfServer[encryptionType]),
            encryptionType,
            encryptedPart);
    }

    private void CheckEncryptedTimestamp(KerberosKdcRequest request, DateTimeOffset now)
    {
        byte[] encoded = FindPreAuthenticationData(request, KerberosKdcMessages.EncryptedTimestampPreAuthenticationType)
            ?? throw new KerberosKdcRefusalException(KerberosErrorCode.PreAuthenticationRequired, "preauthentication required", WritePreAuthenticationHints(request));
        KerberosEncryptedData encryptedTimestamp = ReadEncryptedData(encoded);
        KerberosEncryptionType encryptionType = RequireAccepted(encryptedTimestamp.EncryptionTypeNumber);
        byte[] plainText = Decrypt(
            KerberosEncryptionProfile.For(encryptionType), userKeys[encryptionType], EncryptedTimestampKeyUsage, encryptedTimestamp.CipherText, KerberosErrorCode.PreAuthenticationFailed);
        if ((now - ReadEncryptedTimestamp(plainText)).Duration() > KerberosAcceptor.ClockSkew)
        {
            throw new KerberosKdcRefusalException(KerberosErrorCode.ClockSkew, "clock skew too great");
        }
    }

    // METHOD-DATA: PA-ETYPE-INFO2 naming the salt for each enctype the client asked for, then an empty PA-ENC-TIMESTAMP.
    private byte[] WritePreAuthenticationHints(KerberosKdcRequest request) => KerberosKdcMessages.WriteMethodData(
    [
        (KerberosKdcMessages.EncryptionTypeInfo2PreAuthenticationType, KerberosKdcMessages.WriteEncryptionTypeInfo2(AcceptedEncryptionTypes(request), UserSalt)),
        (KerberosKdcMessages.EncryptedTimestampPreAuthenticationType, []),
    ]);

    private byte[] AnswerTicketGrantingRequest(KerberosKdcRequest request, DateTimeOffset now)
    {
        byte[] apRequestBytes = FindPreAuthenticationData(request, TgsRequestPreAuthenticationType)
            ?? throw new AsnContentException("A TGS-REQ carries no PA-TGS-REQ.");
        KerberosApRequest apRequest = KerberosApRequest.Read(apRequestBytes);
        KerberosTicketPart ticketGrantingTicket = OpenTicketGrantingTicket(apRequest, now);
        KerberosAuthenticatorPart authenticator = OpenAuthenticator(apRequest, ticketGrantingTicket);
        KerberosPrincipalName server = RequireServerName(request);
        Dictionary<KerberosEncryptionType, byte[]> keysOfServer = FindServerKeys(server);
        KerberosEncryptionType encryptionType = ChooseEncryptionType(request);
        KerberosIssuedTicket ticket = Issue(
            KerberosDerWriter.Flag(PreAuthenticatedFlagBit), encryptionType, ticketGrantingTicket.Client, server, ticketGrantingTicket.AuthTime, now, request.Till, ticketGrantingTicket.EndTime);
        KerberosEncryptionKey replyKey = authenticator.Subkey ?? ticketGrantingTicket.SessionKey;
        KerberosEncryptionProfile replyProfile = replyKey.GetProfile();
        byte[] encryptedPart = Encrypt(
            replyProfile, replyKey.KeyValue, authenticator.Subkey is null ? TgsReplySessionKeyUsage : TgsReplySubkeyUsage,
            KerberosKdcMessages.WriteEncKdcReplyPart(KerberosKdcMessages.EncTgsReplyPartTag, ticket, request.Nonce));
        return KerberosKdcMessages.WriteKdcReply(
            KerberosKdcMessages.TgsReplyMessageType, [], ticketGrantingTicket.Client, SealTicket(ticket, keysOfServer[encryptionType]), replyProfile.EncryptionType, encryptedPart);
    }

    private KerberosTicketPart OpenTicketGrantingTicket(KerberosApRequest apRequest, DateTimeOffset now)
    {
        if (!apRequest.ServerName.Equals(TicketGrantingPrincipal))
        {
            throw new KerberosKdcRefusalException(KerberosErrorCode.NotUs, $"ticket is for {apRequest.ServerName}, not the ticket-granting service");
        }

        KerberosEncryptionType encryptionType = RequireAccepted(apRequest.TicketEncryptedPart.EncryptionTypeNumber);
        KerberosTicketPart ticket = KerberosTicketPart.Read(Decrypt(
            KerberosEncryptionProfile.For(encryptionType), serverKeys[TicketGrantingPrincipal][encryptionType], TicketKeyUsage, apRequest.TicketEncryptedPart.CipherText, KerberosErrorCode.BadIntegrity));
        if (now > ticket.EndTime)
        {
            throw new KerberosKdcRefusalException(KerberosErrorCode.TicketExpired, "ticket-granting ticket expired");
        }

        return ticket;
    }

    // A fixture's simplification: the authenticator's checksum over req-body and its ctime are not
    // checked, only that it decrypts under the TGT's session key and names the TGT's client.
    private static KerberosAuthenticatorPart OpenAuthenticator(KerberosApRequest apRequest, KerberosTicketPart ticketGrantingTicket)
    {
        KerberosAuthenticatorPart authenticator = KerberosAuthenticatorPart.Read(Decrypt(
            ticketGrantingTicket.SessionKey.GetProfile(), ticketGrantingTicket.SessionKey.KeyValue, TgsAuthenticatorKeyUsage, apRequest.Authenticator.CipherText, KerberosErrorCode.BadIntegrity));
        if (!authenticator.Client.Equals(ticketGrantingTicket.Client))
        {
            throw new KerberosKdcRefusalException(KerberosErrorCode.BadMatch, "authenticator client differs from ticket client");
        }

        return authenticator;
    }

    private Dictionary<KerberosEncryptionType, byte[]> FindServerKeys(KerberosPrincipalName server) =>
        serverKeys.TryGetValue(server, out Dictionary<KerberosEncryptionType, byte[]>? keys)
            ? keys
            : throw new KerberosKdcRefusalException(KerberosErrorCode.ServerPrincipalUnknown, $"server {server} unknown");

    // The ticket ends at the requested time or at the latest end the KDC allows, whichever is
    // earlier; a till of 19700101000000Z asks for the latest (RFC 4120 section 5.4.1), and one
    // not after the start is refused. No PAC is put in its authorization-data: surl's acceptor
    // never reads one.
    private KerberosIssuedTicket Issue(
        uint flags,
        KerberosEncryptionType encryptionType,
        KerberosPrincipalName client,
        KerberosPrincipalName server,
        DateTimeOffset authTime,
        DateTimeOffset startTime,
        DateTimeOffset till,
        DateTimeOffset latestEnd)
    {
        DateTimeOffset requestedEnd = till == DateTimeOffset.UnixEpoch ? latestEnd : till;
        DateTimeOffset endTime = KerberosDerWriter.WholeSeconds(requestedEnd < latestEnd ? requestedEnd : latestEnd);
        DateTimeOffset wholeStartTime = KerberosDerWriter.WholeSeconds(startTime);
        if (endTime <= wholeStartTime)
        {
            throw new KerberosKdcRefusalException(KerberosErrorCode.NeverValid, "requested end time is not after the start time");
        }

        return new KerberosIssuedTicket(
            flags, encryptionType, DrawKey(encryptionType), client, server, KerberosDerWriter.WholeSeconds(authTime), wholeStartTime, endTime);
    }

    private byte[] SealTicket(KerberosIssuedTicket ticket, byte[] serverKey) => KerberosKdcMessages.WriteTicket(
        ticket.Server,
        ticket.SessionKeyType,
        KeyVersionNumber,
        Encrypt(KerberosEncryptionProfile.For(ticket.SessionKeyType), serverKey, TicketKeyUsage, KerberosKdcMessages.WriteEncTicketPart(ticket)));

    private byte[] Encrypt(KerberosEncryptionProfile profile, ReadOnlySpan<byte> key, int keyUsage, byte[] plainText)
    {
        Span<byte> confounder = stackalloc byte[KerberosEncryptionProfile.ConfounderLength];
        randomSource.Fill(confounder);
        return profile.Encrypt(key, keyUsage, confounder, plainText);
    }

    private byte[] DrawKey(KerberosEncryptionType encryptionType)
    {
        byte[] key = new byte[KerberosEncryptionProfile.For(encryptionType).KeyLength];
        randomSource.Fill(key);
        return key;
    }
}
