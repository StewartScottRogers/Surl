namespace Surl.Authentication;

/// <summary>
/// The authentication methods <c>--auth</c> names (ADR-0032, section 3, as ADR-0049 section 3
/// grows it), declared in the order they are listed everywhere: challenges, warnings and logs.
/// The SASL mechanisms the mail servers check sit beside their HTTP kin.
/// </summary>
public enum AuthenticationMethod
{
    /// <summary>
    /// Negotiate, RFC 4559 (<c>--auth negotiate</c>); not in the default set.
    /// </summary>
    Negotiate,

    /// <summary>
    /// SASL <c>GSSAPI</c>, RFC 4752 (<c>--auth gssapi</c>), for the mail servers: a Kerberos
    /// ticket checked against the <c>--keytab</c> keys, so no secret is sent; not in the default
    /// set (ADR-0049, section 3; ADR-0057, decision 9).
    /// </summary>
    Gssapi,

    /// <summary>
    /// NTLM (<c>--auth ntlm</c>); not in the default set.
    /// </summary>
    Ntlm,

    /// <summary>
    /// NTLMv1 without extended session security (<c>--auth ntlmv1</c>), the only login upstream
    /// curl makes in an SMB session setup; its own word, not in the default set, so accepting
    /// HTTP's NTLMv2 never accepts it (ADR-0073, decision 3).
    /// </summary>
    NtlmV1,

    /// <summary>
    /// Digest, RFC 7616 (<c>--auth digest</c>).
    /// </summary>
    Digest,

    /// <summary>
    /// SASL <c>DIGEST-MD5</c>, RFC 2831 (<c>--auth digest-md5</c>), for the mail servers; Historic
    /// (RFC 6331), so not in the default set (ADR-0049, section 3).
    /// </summary>
    DigestMd5,

    /// <summary>
    /// SASL <c>CRAM-MD5</c>, RFC 2195 (<c>--auth cram-md5</c>), for the mail servers.
    /// </summary>
    CramMd5,

    /// <summary>
    /// POP3 <c>APOP</c>, RFC 1939 section 7 (<c>--auth apop</c>); not in the default set
    /// (ADR-0049, section 3).
    /// </summary>
    Apop,

    /// <summary>
    /// Basic, RFC 7617 (<c>--auth basic</c>): a plain-text secret.
    /// </summary>
    Basic,

    /// <summary>
    /// SASL <c>PLAIN</c>, RFC 4616 (<c>--auth plain</c>), for the mail servers: a plain-text
    /// secret (ADR-0049, section 1).
    /// </summary>
    Plain,

    /// <summary>
    /// SASL <c>LOGIN</c>, draft-murchison-sasl-login (<c>--auth login</c>), for the mail servers:
    /// a plain-text secret (ADR-0049, section 1).
    /// </summary>
    Login,

    /// <summary>
    /// Bearer, RFC 6750 (<c>--auth bearer</c>): a plain-text secret.
    /// </summary>
    Bearer,

    /// <summary>
    /// SASL <c>OAUTHBEARER</c>, RFC 7628 (<c>--auth oauthbearer</c>), for the mail servers: a
    /// bearer token, so a plain-text secret (ADR-0049, section 1).
    /// </summary>
    OAuthBearer,

    /// <summary>
    /// SASL <c>XOAUTH2</c>, Google's format (<c>--auth xoauth2</c>), for the mail servers: a
    /// bearer token, so a plain-text secret (ADR-0049, section 1).
    /// </summary>
    XOAuth2,

    /// <summary>
    /// SASL <c>EXTERNAL</c>, RFC 4422 appendix A (<c>--auth external</c>), for the mail servers:
    /// the login is the verified TLS client certificate, so no secret is sent (ADR-0049, section 4).
    /// </summary>
    External,

    /// <summary>
    /// AWS Signature Version 4 (<c>--auth aws-sigv4</c>).
    /// </summary>
    AwsSigV4,
}
