namespace Surl.Qpack;

/// <summary>What <see cref="QpackDecoder.DecodeFieldSection" /> made of one encoded field section.</summary>
/// <param name="FieldLines">The field lines in order, or empty when the section did not decode.</param>
/// <param name="Error">Why the section did not decode, or <see langword="null" /> when it did.</param>
public sealed record QpackFieldSectionResult(IReadOnlyList<QpackFieldLine> FieldLines, QpackDecodingError? Error)
{
    /// <summary>Gets whether the section decoded.</summary>
    public bool IsDecoded => Error is null;

    /// <summary>
    /// Gets the connection error the failure is, <see cref="QpackErrorCode.DecompressionFailed" />, or
    /// <see langword="null" /> when the section decoded or was only
    /// <see cref="QpackDecodingError.FieldSectionTooLarge" />, which its stream answers alone.
    /// </summary>
    public QpackErrorCode? ErrorCode =>
        Error is null or QpackDecodingError.FieldSectionTooLarge ? null : QpackErrorCode.DecompressionFailed;
}
