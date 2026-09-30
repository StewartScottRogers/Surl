namespace Surl.Authentication;

/// <summary>
/// The HTTP authentication methods <c>--auth</c> names (ADR-0032, section 3), declared in the
/// order they are listed everywhere: challenges, warnings and logs.
/// </summary>
public enum AuthenticationMethod
{
    /// <summary>
    /// Negotiate, RFC 4559 (<c>--auth negotiate</c>); not in the default set.
    /// </summary>
    Negotiate,

    /// <summary>
    /// NTLM (<c>--auth ntlm</c>); not in the default set.
    /// </summary>
    Ntlm,

    /// <summary>
    /// Digest, RFC 7616 (<c>--auth digest</c>).
    /// </summary>
    Digest,

    /// <summary>
    /// Basic, RFC 7617 (<c>--auth basic</c>): a plain-text secret.
    /// </summary>
    Basic,

    /// <summary>
    /// Bearer, RFC 6750 (<c>--auth bearer</c>): a plain-text secret.
    /// </summary>
    Bearer,

    /// <summary>
    /// AWS Signature Version 4 (<c>--auth aws-sigv4</c>).
    /// </summary>
    AwsSigV4,
}
