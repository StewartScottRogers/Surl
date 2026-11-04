namespace Surl.Kerberos.TestKdc;

/// <summary>
/// The RFC 4120 section 7.5.9 error codes the test KDC answers with, in a <c>KRB-ERROR</c>'s
/// <c>error-code</c>.
/// </summary>
public enum KerberosErrorCode
{
    /// <summary><c>KDC_ERR_C_PRINCIPAL_UNKNOWN</c>: the AS-REQ names a client the KDC does not know.</summary>
    ClientPrincipalUnknown = 6,

    /// <summary><c>KDC_ERR_S_PRINCIPAL_UNKNOWN</c>: the request names a server the KDC does not know.</summary>
    ServerPrincipalUnknown = 7,

    /// <summary><c>KDC_ERR_NEVER_VALID</c>: the requested end time is not after the ticket's start time.</summary>
    NeverValid = 11,

    /// <summary><c>KDC_ERR_ETYPE_NOSUPP</c>: none of the client's enctypes is 17, 18, 19 or 20.</summary>
    EncryptionTypeNotSupported = 14,

    /// <summary><c>KDC_ERR_PREAUTH_FAILED</c>: the <c>PA-ENC-TIMESTAMP</c> does not decrypt under the user's key.</summary>
    PreAuthenticationFailed = 24,

    /// <summary><c>KDC_ERR_PREAUTH_REQUIRED</c>: the AS-REQ carries no <c>PA-ENC-TIMESTAMP</c>.</summary>
    PreAuthenticationRequired = 25,

    /// <summary><c>KRB_AP_ERR_BAD_INTEGRITY</c>: a TGS-REQ's ticket or authenticator does not decrypt.</summary>
    BadIntegrity = 31,

    /// <summary><c>KRB_AP_ERR_TKT_EXPIRED</c>: a TGS-REQ's ticket-granting ticket has expired.</summary>
    TicketExpired = 32,

    /// <summary><c>KRB_AP_ERR_NOT_US</c>: a TGS-REQ's ticket is not a ticket-granting ticket of this KDC.</summary>
    NotUs = 35,

    /// <summary><c>KRB_AP_ERR_BADMATCH</c>: a TGS-REQ's authenticator names another client than its ticket.</summary>
    BadMatch = 36,

    /// <summary><c>KRB_AP_ERR_SKEW</c>: the <c>PA-ENC-TIMESTAMP</c> is more than five minutes off the KDC's clock.</summary>
    ClockSkew = 37,

    /// <summary><c>KRB_ERR_RESPONSE_TOO_BIG</c>: the answer does not fit one UDP datagram; the client retries over TCP.</summary>
    ResponseTooBig = 52,

    /// <summary><c>KRB_ERR_GENERIC</c>: the request is not a well-formed AS-REQ or TGS-REQ.</summary>
    Generic = 60,

    /// <summary><c>KRB_ERR_FIELD_TOOLONG</c>: the request is longer than 64 KiB.</summary>
    FieldTooLong = 61,
}
