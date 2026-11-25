using System.Text;

namespace Surl.Protocol.Abstractions;

/// <summary>
/// One <c>name=value</c> pair a client sent in a <c>Cookie</c> header of an HTTP or HTTPS request, published before the
/// reply (ADR-0189 decision 5).
/// </summary>
/// <param name="ExchangeId">The exchange whose request carried the cookie.</param>
/// <param name="Scheme">The scheme the request arrived on (<c>http</c> or <c>https</c>).</param>
/// <param name="Name">The cookie name.</param>
/// <param name="Value">The cookie value, bounded to 256 bytes as event bytes are.</param>
public sealed record CookieReceivedEvent(long? ExchangeId, string Scheme, string Name, string Value) : SurlEvent
{
    /// <summary>The most bytes of a cookie value an event holds.</summary>
    public const int MaxValueBytes = 256;

    /// <inheritdoc />
    public override long? ExchangeId { get; } = ExchangeId;

    /// <inheritdoc />
    public override string? Scheme { get; } = Scheme;

    /// <summary>The cookie value, cut to <see cref="MaxValueBytes"/> UTF-8 bytes.</summary>
    public string Value { get; } = Bound(Value);

    /// <inheritdoc />
    public override void WriteMembers(ISurlEventMemberWriter writer)
    {
        writer.Number(nameof(ExchangeId), ExchangeId);
        writer.Text(nameof(Scheme), Scheme);
        writer.Text(nameof(Name), Name);
        writer.Text(nameof(Value), Value);
    }

    private static string Bound(string value)
    {
        if (Encoding.UTF8.GetByteCount(value) <= MaxValueBytes)
        {
            return value;
        }

        string cut = value;
        while (Encoding.UTF8.GetByteCount(cut) > MaxValueBytes)
        {
            cut = cut[..^1];
        }

        return char.IsHighSurrogate(cut[^1]) ? cut[..^1] : cut;
    }
}
