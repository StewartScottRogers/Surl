namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// One entry of a Put Block List body: the list it names and the block ID it holds.
/// </summary>
/// <param name="Kind">Which block list the ID is looked for in.</param>
/// <param name="Id">The block ID, base64.</param>
internal sealed record AzureBlockListEntry(AzureBlockListKind Kind, string Id);
