namespace Surl.Kerberos.TestKdc;

/// <summary>
/// Why the test KDC answers a request with a <c>KRB-ERROR</c> rather than a reply: the error code,
/// its <c>e-text</c>, and the <c>e-data</c> when the error carries some.
/// </summary>
/// <param name="errorCode">The RFC 4120 error code.</param>
/// <param name="text">The <c>e-text</c>, which never carries a key byte.</param>
/// <param name="errorData">The <c>e-data</c>, or <see langword="null" /> for none.</param>
internal sealed class KerberosKdcRefusalException(KerberosErrorCode errorCode, string text, byte[]? errorData = null) : Exception(text)
{
    /// <summary>Gets the RFC 4120 error code.</summary>
    public KerberosErrorCode ErrorCode { get; } = errorCode;

    /// <summary>Gets the <c>e-data</c>, or <see langword="null" /> for none.</summary>
    public byte[]? ErrorData { get; } = errorData;
}
