using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Surl.Authentication;

/// <summary>
/// Stateless Digest nonces (ADR-0036): 8 bytes of issue time (the injected
/// <see cref="TimeProvider"/>'s UTC ticks, big-endian), 16 bytes from
/// <see cref="RandomNumberGenerator"/>, and the first 16 bytes of HMAC-SHA-256 over those 24
/// under a key drawn at start-up, written as 80 lower-case hex digits. A nonce is recognised
/// by its HMAC, compared in fixed time, and expires <see cref="Lifetime"/> after issue. Only
/// the <c>nc</c> of nonces that verified answers used is remembered, until they expire.
/// </summary>
internal sealed class DigestNonceBook : IDigestNonceBook
{
    /// <summary>
    /// How long a nonce is fresh (ADR-0036).
    /// </summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private const int TimeLength = 8;
    private const int SignedLength = TimeLength + 16;
    private const int NonceLength = SignedLength + 16;

    private readonly byte[] key = RandomNumberGenerator.GetBytes(32);
    private readonly TimeProvider timeProvider;
    private readonly Lock gate = new();
    private readonly Dictionary<string, (DateTimeOffset IssuedAt, uint NonceCount)> usedNonceCounts =
        new(StringComparer.Ordinal);

    public DigestNonceBook(TimeProvider timeProvider)
    {
        this.timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    public string Issue()
    {
        Span<byte> nonce = stackalloc byte[NonceLength];
        BinaryPrimitives.WriteInt64BigEndian(nonce, timeProvider.GetUtcNow().UtcTicks);
        RandomNumberGenerator.Fill(nonce[TimeLength..SignedLength]);
        HMACSHA256.HashData(key, nonce[..SignedLength]).AsSpan(0, NonceLength - SignedLength)
            .CopyTo(nonce[SignedLength..]);

        return Convert.ToHexStringLower(nonce);
    }

    /// <inheritdoc/>
    public DigestNonceState Check(string nonce)
    {
        if (!TryReadIssueTime(nonce, out var issuedAt))
        {
            return DigestNonceState.Unknown;
        }

        return timeProvider.GetUtcNow() - issuedAt > Lifetime ? DigestNonceState.Expired : DigestNonceState.Fresh;
    }

    /// <inheritdoc/>
    public bool TryRecordUse(string nonce, uint nonceCount)
    {
        var now = timeProvider.GetUtcNow();
        lock (gate)
        {
            ForgetExpiredCounts(now);

            // A nonce that expired since Check is refused here: its count may just have been purged.
            if (!TryReadIssueTime(nonce, out var issuedAt) || now - issuedAt > Lifetime || IsUsed(nonce, nonceCount))
            {
                return false;
            }

            usedNonceCounts[nonce] = (issuedAt, nonceCount);

            return true;
        }
    }

    private void ForgetExpiredCounts(DateTimeOffset now)
    {
        foreach (var expired in usedNonceCounts.Where(pair => now - pair.Value.IssuedAt > Lifetime).ToList())
        {
            usedNonceCounts.Remove(expired.Key);
        }
    }

    private bool IsUsed(string nonce, uint nonceCount) =>
        usedNonceCounts.TryGetValue(nonce, out var used) && nonceCount <= used.NonceCount;

    private bool TryReadIssueTime(string nonce, out DateTimeOffset issuedAt)
    {
        issuedAt = default;
        Span<byte> bytes = stackalloc byte[NonceLength];
        if (nonce.Length != NonceLength * 2
            || nonce.Any(char.IsAsciiLetterUpper)
            || Convert.FromHexString(nonce, bytes, out _, out _) != OperationStatus.Done
            || !CryptographicOperations.FixedTimeEquals(
                HMACSHA256.HashData(key, bytes[..SignedLength]).AsSpan(0, NonceLength - SignedLength),
                bytes[SignedLength..]))
        {
            return false;
        }

        issuedAt = new DateTimeOffset(BinaryPrimitives.ReadInt64BigEndian(bytes), TimeSpan.Zero);

        return true;
    }
}
