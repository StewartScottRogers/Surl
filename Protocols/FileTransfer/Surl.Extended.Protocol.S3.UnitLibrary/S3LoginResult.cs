using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// What <see cref="S3Login.JudgeHeadAsync"/> made of a request's head: the caller every store
/// call names and the rest of a check that binds the body, or the error refusing the request.
/// </summary>
/// <param name="Caller">The SigV4 account, or anonymous, on <c>s3</c>; <see langword="null"/> when refused.</param>
/// <param name="BodyCheck">The rest of a check that binds the body; <see langword="null"/> when the head decided.</param>
/// <param name="Refusal">The error refusing the request; <see langword="null"/> when it is served.</param>
internal sealed record S3LoginResult(ContentCaller? Caller, IHttpRequestBodyCheck? BodyCheck, S3Error? Refusal)
{
    /// <summary>
    /// A request served as <paramref name="accountName"/>, or anonymously when it is <see langword="null"/>.
    /// </summary>
    /// <param name="accountName">The account the signature logged in.</param>
    /// <param name="bodyCheck">The rest of a check that binds the body.</param>
    /// <returns>The result.</returns>
    public static S3LoginResult As(string? accountName, IHttpRequestBodyCheck? bodyCheck) =>
        new(
            accountName is null
                ? ContentCaller.Anonymous(S3ProtocolServer.Scheme)
                : ContentCaller.ForAccount(accountName, S3ProtocolServer.Scheme),
            bodyCheck,
            null);

    /// <summary>
    /// A request refused with <paramref name="error"/>.
    /// </summary>
    /// <param name="error">The error.</param>
    /// <returns>The result.</returns>
    public static S3LoginResult Refused(S3Error error) => new(null, null, error);
}
