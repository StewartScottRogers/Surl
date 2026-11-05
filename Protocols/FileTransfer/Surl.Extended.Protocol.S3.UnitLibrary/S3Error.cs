using Surl.HttpMessage;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// One row of the S3 API Reference's "List of error codes" the server sends: the code, the
/// status the table gives it, and its fixed message (ADR-0108, decision 1.1; ADR-0006, section 3).
/// </summary>
/// <param name="Code">The <c>Code</c> element's text, such as <c>NoSuchKey</c>.</param>
/// <param name="Status">The status the table gives the code.</param>
/// <param name="Message">The <c>Message</c> element's fixed text.</param>
internal sealed record S3Error(string Code, HttpStatus Status, string Message)
{
    /// <summary>
    /// <c>404 NoSuchKey</c>: no file at the key, the key is a folder, or the store cannot hold it.
    /// </summary>
    public static S3Error NoSuchKey { get; } = new("NoSuchKey", HttpStatus.NotFound, "The specified key does not exist.");

    /// <summary>
    /// <c>404 NoSuchBucket</c>: the bucket's top-level folder does not exist.
    /// </summary>
    public static S3Error NoSuchBucket { get; } = new("NoSuchBucket", HttpStatus.NotFound, "The specified bucket does not exist.");

    /// <summary>
    /// <c>501 NotImplemented</c>: every request ADR-0108 decision 1 does not serve.
    /// </summary>
    public static S3Error NotImplemented { get; } = new("NotImplemented", HttpStatus.NotImplemented, "A header you provided implies functionality that is not implemented");

    /// <summary>
    /// <c>416 InvalidRange</c>: a single range that names no byte of the object.
    /// </summary>
    public static S3Error InvalidRange { get; } = new("InvalidRange", new HttpStatus(416, "Range Not Satisfiable"), "The requested range is not satisfiable");

    /// <summary>
    /// <c>412 PreconditionFailed</c>: a failed <c>If-Match</c> or <c>If-Unmodified-Since</c>.
    /// </summary>
    public static S3Error PreconditionFailed { get; } = new("PreconditionFailed", new HttpStatus(412, "Precondition Failed"), "At least one of the preconditions you specified did not hold.");

    /// <summary>
    /// <c>400 KeyTooLongError</c>: a key longer than <see cref="S3ObjectKey.MaxKeyBytes"/> UTF-8 bytes.
    /// </summary>
    public static S3Error KeyTooLong { get; } = new("KeyTooLongError", HttpStatus.BadRequest, "Your key is too long.");

    /// <summary>
    /// <c>503 SlowDown</c>: a connection past <c>--max-connections</c> or
    /// <c>--max-connections-per-address</c> (ADR-0108, decision 7).
    /// </summary>
    public static S3Error SlowDown { get; } = new("SlowDown", HttpStatus.ServiceUnavailable, "Please reduce your request rate.");
}
