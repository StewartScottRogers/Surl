using System.Text;

namespace Surl.Api;

/// <summary>One live Redis key read back by <see cref="SurlServerKeys"/>.GetAsync.</summary>
/// <param name="Info">The key's name, type, size and expiry.</param>
/// <param name="Bytes">A copy of the string value's bytes, or <see langword="null"/> when the key holds another type, which is listed and not read.</param>
public sealed record SurlKeyContents(SurlKeyInfo Info, ReadOnlyMemory<byte>? Bytes)
{
    /// <summary>The string value read as UTF-8, or <see langword="null"/> when the key holds another type.</summary>
    public string? Text => Bytes is { } bytes ? Encoding.UTF8.GetString(bytes.Span) : null;
}
