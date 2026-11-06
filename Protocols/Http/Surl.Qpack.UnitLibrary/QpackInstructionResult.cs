namespace Surl.Qpack;

/// <summary>
/// What became of bytes received on an encoder or decoder stream. An instruction that ends mid-way
/// waits for the next bytes, so a partial instruction is accepted.
/// </summary>
/// <param name="Error">Why an instruction was refused, or <see langword="null" /> when all were accepted.</param>
/// <param name="ErrorCode">The connection error the refusal is, or <see langword="null" />.</param>
public sealed record QpackInstructionResult(QpackDecodingError? Error, QpackErrorCode? ErrorCode)
{
    /// <summary>Gets the result of bytes whose instructions were all accepted.</summary>
    public static QpackInstructionResult Accepted { get; } = new(null, null);

    /// <summary>Gets whether every complete instruction was accepted.</summary>
    public bool IsAccepted => Error is null;
}
