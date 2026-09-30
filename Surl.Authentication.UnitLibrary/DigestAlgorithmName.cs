namespace Surl.Authentication;

/// <summary>
/// An <c>algorithm</c> parameter's value read into its hash and whether it is the <c>-sess</c>
/// form (RFC 7616 section 3.4.2).
/// </summary>
/// <param name="Algorithm">The hash.</param>
/// <param name="IsSession">Whether the value ended in <c>-sess</c>.</param>
internal sealed record DigestAlgorithmName(DigestAlgorithm Algorithm, bool IsSession)
{
    private const string SessionSuffix = "-sess";

    private static readonly Dictionary<string, DigestAlgorithm> AlgorithmsByName =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["MD5"] = DigestAlgorithm.Md5,
            ["SHA-256"] = DigestAlgorithm.Sha256,
            ["SHA-512-256"] = DigestAlgorithm.Sha512Slash256,
        };

    /// <summary>
    /// The name each hash is offered under in a challenge.
    /// </summary>
    /// <param name="algorithm">The hash.</param>
    /// <returns><c>MD5</c>, <c>SHA-256</c> or <c>SHA-512-256</c>.</returns>
    public static string NameOf(DigestAlgorithm algorithm) =>
        AlgorithmsByName.Single(pair => pair.Value == algorithm).Key;

    /// <summary>
    /// Reads an <c>algorithm</c> value, matched case-insensitively; an absent one is <c>MD5</c>
    /// (RFC 7616 section 3.3).
    /// </summary>
    /// <param name="value">The value as received, or <see langword="null"/> when absent.</param>
    /// <returns>The algorithm, or <see langword="null"/> for any other name.</returns>
    public static DigestAlgorithmName? Parse(string? value)
    {
        var name = value ?? "MD5";
        var isSession = name.EndsWith(SessionSuffix, StringComparison.OrdinalIgnoreCase);
        var hashName = isSession ? name[..^SessionSuffix.Length] : name;

        return AlgorithmsByName.TryGetValue(hashName, out var algorithm)
            ? new DigestAlgorithmName(algorithm, isSession)
            : null;
    }
}
