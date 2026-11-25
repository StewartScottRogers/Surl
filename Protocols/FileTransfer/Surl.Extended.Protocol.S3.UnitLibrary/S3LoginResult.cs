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
    /// The chain each <c>aws-chunked</c> chunk's signature is checked against, for a
    /// <c>STREAMING-AWS4-HMAC-SHA256-PAYLOAD</c> or <c>-TRAILER</c> body; <see langword="null"/> otherwise.
    /// </summary>
    public IHttpChunkSignatureChain? ChunkSignatures { get; init; }

    /// <summary>
    /// Tells the rest of a check that binds the body that the server answered without reading it
    /// (ADR-0045 amendment 1); nothing when the head decided. The answer the peer gets is unchanged.
    /// </summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the check has noted the abandonment.</returns>
    public ValueTask AbandonBodyCheckAsync(CancellationToken cancellationToken) =>
        BodyCheck?.AbandonAsync(cancellationToken) ?? ValueTask.CompletedTask;

    /// <summary>
    /// A request served as <paramref name="accountName"/>, or anonymously when it is <see langword="null"/>.
    /// </summary>
    /// <param name="exchange">The exchange the request arrived on.</param>
    /// <param name="accountName">The account the signature logged in.</param>
    /// <param name="bodyCheck">The rest of a check that binds the body.</param>
    /// <returns>The result.</returns>
    public static S3LoginResult As(ExchangeContext exchange, string? accountName, IHttpRequestBodyCheck? bodyCheck) =>
        new(ContentCaller.ForExchange(exchange, accountName), bodyCheck, null);

    /// <summary>
    /// A request refused with <paramref name="error"/>.
    /// </summary>
    /// <param name="error">The error.</param>
    /// <returns>The result.</returns>
    public static S3LoginResult Refused(S3Error error) => new(null, null, error);
}
