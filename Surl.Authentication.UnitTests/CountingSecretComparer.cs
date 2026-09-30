namespace Surl.Authentication;

/// <summary>
/// An <see cref="ISecretComparer"/> that records the length of every comparison and then
/// compares with the real <see cref="CryptographicSecretComparer"/>.
/// </summary>
internal sealed class CountingSecretComparer : ISecretComparer
{
    public List<(int LeftLength, int RightLength)> Comparisons { get; } = [];

    public bool FixedTimeEquals(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        Comparisons.Add((left.Length, right.Length));

        return CryptographicSecretComparer.Instance.FixedTimeEquals(left, right);
    }
}
