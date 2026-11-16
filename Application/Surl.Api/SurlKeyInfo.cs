using System.Text;

namespace Surl.Api;

/// <summary>One live Redis key as <see cref="SurlServerKeys"/>.ListAsync lists it: its name, type and size, never its value.</summary>
/// <param name="KeyBytes">The key's bytes, as a client or a seed wrote them.</param>
/// <param name="Type">The Redis type name: <c>string</c>, <c>list</c>, <c>hash</c> or <c>set</c>.</param>
/// <param name="ByteCount">The bytes the value holds.</param>
/// <param name="ExpiresAt">When the key expires, or <see langword="null"/> when it never does.</param>
public sealed record SurlKeyInfo(ReadOnlyMemory<byte> KeyBytes, string Type, long ByteCount, DateTimeOffset? ExpiresAt)
{
    /// <summary>The key read as UTF-8; invalid bytes read as the replacement character, so use <see cref="KeyBytes"/> for exact bytes.</summary>
    public string Key => Encoding.UTF8.GetString(KeyBytes.Span);
}
