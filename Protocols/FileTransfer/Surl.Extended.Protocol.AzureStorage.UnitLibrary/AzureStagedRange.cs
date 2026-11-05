namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// The bytes one append staged: where in the file they start, and how many there are.
/// </summary>
/// <param name="Position">The append's <c>position</c>.</param>
/// <param name="Length">How many bytes of its body were staged.</param>
internal sealed record AzureStagedRange(long Position, long Length);
