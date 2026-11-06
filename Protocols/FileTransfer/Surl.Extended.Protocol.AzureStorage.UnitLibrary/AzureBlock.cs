namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// One block of a blob ("Put Block"): its ID as the client sent it (base64), where its bytes
/// start - in the staging's upload session for an uncommitted block, in the blob for a committed
/// one - and how many there are.
/// </summary>
/// <param name="Id">The block ID, base64.</param>
/// <param name="Offset">Where the block's first byte is.</param>
/// <param name="Size">How many bytes the block holds.</param>
internal sealed record AzureBlock(string Id, long Offset, long Size);
