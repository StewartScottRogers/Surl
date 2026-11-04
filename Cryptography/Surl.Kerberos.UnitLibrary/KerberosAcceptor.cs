using System.Buffers.Binary;
using System.Formats.Asn1;

namespace Surl.Kerberos;

/// <summary>
/// Checks a client's Kerberos AP-REQ, framed as an RFC 1964 <c>InitialContextToken</c>, against
/// the keys of a keytab (ADR-0057 decisions 2 and 4), and gives back the security context that
/// makes the AP-REP and the RFC 4121 tokens. A refusal is a result with its reason, never an
/// exception.
/// </summary>
/// <param name="keytab">The service keys.</param>
/// <param name="replayCache">The process's replay cache, shared by every listener.</param>
/// <param name="timeProvider">The clock tickets and authenticators are checked on.</param>
/// <param name="randomSource">Where the AP-REP's sequence number and confounder come from.</param>
public sealed class KerberosAcceptor(KerberosKeytab keytab, KerberosReplayCache replayCache, TimeProvider timeProvider, IKerberosRandomSource randomSource)
{
    private const string MalformedToken = "malformed token";
    private const string IntegrityCheckFailed = "integrity check failed";
    private const int TicketKeyUsage = 2;
    private const int AuthenticatorKeyUsage = 11;

    /// <summary>Gets the clock skew allowed between surl and a client or its KDC: 300 seconds, RFC 4120's usual value.</summary>
    public static TimeSpan ClockSkew { get; } = TimeSpan.FromSeconds(300);

    /// <summary>
    /// Checks a bare Kerberos <c>InitialContextToken</c>, one that came outside SPNEGO: its
    /// mechanism must be the Kerberos OID <c>1.2.840.113554.1.2.2</c>.
    /// </summary>
    /// <param name="initialContextToken">The client's token.</param>
    /// <param name="service">The service word the listen URL's scheme answers: <c>HTTP</c>, <c>smtp</c>, <c>imap</c> or <c>pop</c>.</param>
    /// <returns>The context, or the refusal reason.</returns>
    public KerberosAcceptResult Accept(ReadOnlySpan<byte> initialContextToken, string service) =>
        AcceptToken(initialContextToken, service, acceptsMicrosoftKerberosOid: false);

    /// <summary>
    /// Checks the Kerberos <c>InitialContextToken</c> a SPNEGO <c>NegTokenInit</c> carried as its
    /// optimistic token: its mechanism may also be Microsoft's Kerberos OID
    /// <c>1.2.840.48018.1.2.2</c>.
    /// </summary>
    /// <param name="initialContextToken">The client's token.</param>
    /// <param name="service">The service word the listen URL's scheme answers: <c>HTTP</c>, <c>smtp</c>, <c>imap</c> or <c>pop</c>.</param>
    /// <returns>The context, or the refusal reason.</returns>
    public KerberosAcceptResult AcceptInsideSpnego(ReadOnlySpan<byte> initialContextToken, string service) =>
        AcceptToken(initialContextToken, service, acceptsMicrosoftKerberosOid: true);

    /// <summary>Names an enctype the way refusal reasons do.</summary>
    /// <param name="encryptionTypeNumber">The enctype number.</param>
    /// <returns>Its RFC name, or <c>enctype n</c> for one surl does not accept.</returns>
    internal static string NameEncryptionType(int encryptionTypeNumber) => encryptionTypeNumber switch
    {
        (int)KerberosEncryptionType.Aes128CtsHmacSha196 => "aes128-cts-hmac-sha1-96",
        (int)KerberosEncryptionType.Aes256CtsHmacSha196 => "aes256-cts-hmac-sha1-96",
        (int)KerberosEncryptionType.Aes128CtsHmacSha256128 => "aes128-cts-hmac-sha256-128",
        (int)KerberosEncryptionType.Aes256CtsHmacSha384192 => "aes256-cts-hmac-sha384-192",
        _ => $"enctype {encryptionTypeNumber}",
    };

    private KerberosAcceptResult AcceptToken(ReadOnlySpan<byte> initialContextToken, string service, bool acceptsMicrosoftKerberosOid)
    {
        ArgumentNullException.ThrowIfNull(service);
        try
        {
            byte[] message = GssApiToken.ReadMessage(initialContextToken, GssApiToken.ApRequestTokenId, acceptsMicrosoftKerberosOid);
            return new KerberosAcceptResult(CheckApRequest(KerberosApRequest.Read(message), service), null);
        }
        catch (KerberosRefusalException refusal)
        {
            return new KerberosAcceptResult(null, refusal.Reason);
        }
        catch (AsnContentException)
        {
            return new KerberosAcceptResult(null, MalformedToken);
        }
    }

    // ADR-0057 decision 4, steps 2 to 7; step 1, the framing, is read by the caller.
    private KerberosSecurityContext CheckApRequest(KerberosApRequest apRequest, string service)
    {
        if (apRequest.IsUseSessionKeyRequested)
        {
            throw new KerberosRefusalException("use-session-key not supported");
        }

        KerberosKeytabEntry serviceKey = FindServiceKey(apRequest, service);
        KerberosTicketPart ticket = KerberosTicketPart.Read(Decrypt(
            KerberosEncryptionProfile.For(serviceKey.EncryptionType), serviceKey.Key.Span, TicketKeyUsage, apRequest.TicketEncryptedPart));
        DateTimeOffset now = timeProvider.GetUtcNow();
        CheckTicket(ticket, now);

        KerberosEncryptionProfile sessionProfile = ticket.SessionKey.GetProfile();
        KerberosAuthenticatorPart authenticator = KerberosAuthenticatorPart.Read(Decrypt(
            sessionProfile, ticket.SessionKey.KeyValue, AuthenticatorKeyUsage, apRequest.Authenticator));
        CheckAuthenticator(authenticator, ticket, now);
        KerberosEncryptionKey contextKey = authenticator.Subkey ?? ticket.SessionKey;
        KerberosEncryptionProfile contextProfile = contextKey.GetProfile();
        RecordAuthenticator(apRequest.Authenticator, authenticator);

        return new KerberosSecurityContext(
            ticket.Client,
            apRequest.IsMutualRequired,
            new KerberosSessionKeys(sessionProfile, ticket.SessionKey.KeyValue, contextProfile, contextKey.KeyValue),
            authenticator,
            apRequest.IsMutualRequired ? DrawSequenceNumber() : authenticator.SequenceNumber,
            randomSource);
    }

    private KerberosKeytabEntry FindServiceKey(KerberosApRequest apRequest, string service)
    {
        KerberosPrincipalName serverName = apRequest.ServerName;
        KerberosEncryptedData encryptedPart = apRequest.TicketEncryptedPart;
        bool namesService = serverName.Components.Count == 2
            && string.Equals(serverName.Components[0], service, StringComparison.OrdinalIgnoreCase);
        KerberosKeytabEntry? entry = namesService
            ? keytab.FindKey(serverName, encryptedPart.EncryptionTypeNumber, encryptedPart.KeyVersionNumber)
            : null;
        return entry ?? throw new KerberosRefusalException(
            $"no key for {serverName} {NameEncryptionType(encryptedPart.EncryptionTypeNumber)} kvno {encryptedPart.KeyVersionNumber?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "any"}");
    }

    private static byte[] Decrypt(KerberosEncryptionProfile profile, ReadOnlySpan<byte> key, int keyUsage, KerberosEncryptedData encryptedData) =>
        profile.TryDecrypt(key, keyUsage, encryptedData.CipherText, out byte[] plainText)
            ? plainText
            : throw new KerberosRefusalException(IntegrityCheckFailed);

    private static void CheckTicket(KerberosTicketPart ticket, DateTimeOffset now)
    {
        if (ticket.IsInvalid)
        {
            throw new KerberosRefusalException("ticket invalid");
        }

        if (now < (ticket.StartTime ?? ticket.AuthTime) - ClockSkew)
        {
            throw new KerberosRefusalException("ticket not yet valid");
        }

        if (now > ticket.EndTime + ClockSkew)
        {
            throw new KerberosRefusalException("ticket expired");
        }
    }

    private static void CheckAuthenticator(KerberosAuthenticatorPart authenticator, KerberosTicketPart ticket, DateTimeOffset now)
    {
        if (!authenticator.Client.Equals(ticket.Client))
        {
            throw new KerberosRefusalException("authenticator client differs from ticket client");
        }

        if ((now - authenticator.PreciseClientTime).Duration() > ClockSkew)
        {
            throw new KerberosRefusalException("clock skew");
        }

        if (!GssApiChecksum.IsWellFormed(authenticator.ChecksumType, authenticator.Checksum))
        {
            throw new KerberosRefusalException("bad GSS-API checksum");
        }
    }

    private void RecordAuthenticator(KerberosEncryptedData encryptedAuthenticator, KerberosAuthenticatorPart authenticator)
    {
        KerberosReplayCacheOutcome outcome = replayCache.TryAdd(encryptedAuthenticator.CipherText, authenticator.PreciseClientTime);
        if (outcome != KerberosReplayCacheOutcome.Added)
        {
            throw new KerberosRefusalException(outcome == KerberosReplayCacheOutcome.Replayed ? "replayed authenticator" : "replay cache full");
        }
    }

    private uint DrawSequenceNumber()
    {
        Span<byte> sequenceNumber = stackalloc byte[4];
        randomSource.Fill(sequenceNumber);
        return BinaryPrimitives.ReadUInt32BigEndian(sequenceNumber);
    }
}
