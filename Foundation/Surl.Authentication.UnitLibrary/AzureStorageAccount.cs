namespace Surl.Authentication;

/// <summary>
/// An account as Azure Shared Key and shared access signatures check it (ADR-0109 decisions 3.2
/// and 3.3): the storage account name and the key its password's base64 text decodes to.
/// </summary>
/// <param name="AccountName">The account's name, or <see langword="null"/> for the dummy an unknown name finds.</param>
/// <param name="Key">The account key, the HMAC-SHA256 key every signature is made with.</param>
internal sealed record AzureStorageAccount(string? AccountName, byte[] Key);
