namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// What is wrong with a <c>multipart/related</c> body (ADR-0148, decision 7).
/// </summary>
internal enum GcsMultipartRelatedProblem
{
    /// <summary>
    /// The body breaks the grammar: bytes before the first delimiter, a third part, a close after one part, or part headers past the limit.
    /// <c>400 badRequest</c>.
    /// </summary>
    Malformed,

    /// <summary>
    /// The metadata part is past <see cref="GcsMultipartRelatedSink.MaxMetadataBytes"/>. <c>400 invalid</c>.
    /// </summary>
    MetadataTooLarge,

    /// <summary>
    /// Opening the second part's destination was refused; the refusal is the answer.
    /// </summary>
    Refused,
}
