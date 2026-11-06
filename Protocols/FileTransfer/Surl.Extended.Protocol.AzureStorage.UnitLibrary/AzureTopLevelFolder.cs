namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// One container or filesystem a listing names (<see cref="AzureTopLevelFolders"/>).
/// </summary>
/// <param name="Name">The container's name, decoded.</param>
/// <param name="LastModifiedUtc">Its folder's last write time, the only time the store keeps for a folder.</param>
internal sealed record AzureTopLevelFolder(string Name, DateTimeOffset LastModifiedUtc);
