namespace Surl.Protocol.Abstractions;

/// <summary>
/// What an <see cref="IHttpAuthenticationSession"/> judges of one HTTP request, after its head
/// has been read and found well-formed (ADR-0032, sections 4 and 6). The body is not included:
/// a method that binds it answers with a verdict carrying an <see cref="IHttpRequestBodyCheck"/>,
/// which the server asks once the body is read (ADR-0045).
/// </summary>
/// <param name="Method">The request method.</param>
/// <param name="Target">The request target as received.</param>
/// <param name="IsWrite"><see langword="true"/> for every method but <c>GET</c> and <c>HEAD</c>.</param>
/// <param name="Fields">Every header field, name and value, in the order received.</param>
/// <param name="AwsSigV4Service">
/// <see langword="null"/> for an HTTP request, judged by <c>--auth</c>'s methods. Set, the
/// request's only method is AWS Signature Version 4 with a scope for this service, as every S3
/// request's is (<c>s3</c>, ADR-0108 decision 4): <c>--auth</c> does not apply, a signature is
/// checked even under <c>--allow-anonymous</c>, <c>x-amz-content-sha256</c> is required with an
/// <c>Authorization</c> (a presigned URL's query signature is over <c>UNSIGNED-PAYLOAD</c>, decision 4.5), no
/// challenge is sent, and every refusal is <see cref="HttpAuthenticationOutcome.Forbidden"/> with
/// its <see cref="HttpAuthenticationVerdict.Refusal"/>.
/// </param>
/// <param name="AzureStorage">
/// <see langword="null"/> for every request but the Azure Storage server's. Set, the request's
/// methods are Azure's (ADR-0109 decision 3): a bearer token, Shared Key or SharedKeyLite, or a
/// shared access signature in the query; <c>--auth</c> does not apply, credentials are checked
/// even under <c>--allow-anonymous</c>, no challenge is sent, and every refusal is
/// <see cref="HttpAuthenticationOutcome.Forbidden"/> with its <see cref="HttpAuthenticationVerdict.Refusal"/>.
/// </param>
public sealed record HttpAuthenticationRequest(
    string Method,
    string Target,
    bool IsWrite,
    IReadOnlyList<KeyValuePair<string, string>> Fields,
    string? AwsSigV4Service = null,
    AzureStorageScope? AzureStorage = null);
