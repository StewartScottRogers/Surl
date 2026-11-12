namespace Surl.Cli;

/// <summary>
/// The checks ADR-0101 decision 5 makes on <c>--share</c>, <c>--share-access</c> and <c>--bucket</c> once the
/// whole command line, every config file included, is read. Each item comes with the option name it was
/// written with (<c>--share</c>, or <c>share</c> on a config-file line), the name a refusal starts with. In
/// order: a share name given twice, a bucket name given twice, a <c>--share-access</c> share given twice
/// (row 14); a bucket whose name is also a share's, refused at the bucket, then a <c>--share-access</c>
/// naming no <c>--share</c> (row 15). Names compare ordinally.
/// </summary>
internal static class ConfiguredContentConflicts
{
    /// <summary>A bucket named as a share is (ADR-0101 decision 5 row 15: one top-level folder, ADR-0100 decision 2.1).</summary>
    /// <param name="name">The bucket's name.</param>
    /// <returns><c>bucket &lt;name&gt; is also a --share</c>.</returns>
    public static string BucketIsAlsoAShare(string name) => $"bucket {name} is also a --share";

    /// <summary>A <c>--share-access</c> whose share no <c>--share</c> gives (ADR-0101 decision 5 row 15).</summary>
    /// <param name="name">The share's name as the <c>--share-access</c> wrote it.</param>
    /// <returns><c>share &lt;name&gt; names no --share</c>.</returns>
    public static string NamesNoShare(string name) => $"share {name} names no --share";

    /// <summary>One refusal: the option name as written and the reason after <c>option &lt;name&gt;: </c>.</summary>
    /// <param name="OptionName">The option name the refused item was written with.</param>
    /// <param name="Reason">The refusal reason, which never holds a path.</param>
    public sealed record Refusal(string OptionName, string Reason);

    /// <summary>Finds the first refusal, or none.</summary>
    /// <param name="shares">Each <c>--share</c> with its option name, in configuration order.</param>
    /// <param name="shareAccess">Each <c>--share-access</c> with its option name, in configuration order.</param>
    /// <param name="buckets">Each <c>--bucket</c> with its option name, in configuration order.</param>
    /// <returns>The first refusal, or <see langword="null"/>.</returns>
    public static Refusal? Find(
        IReadOnlyList<(string OptionName, ConfiguredShare Share)> shares,
        IReadOnlyList<(string OptionName, ConfiguredShareAccess Access)> shareAccess,
        IReadOnlyList<(string OptionName, ConfiguredBucket Bucket)> buckets)
    {
        ArgumentNullException.ThrowIfNull(shares);
        ArgumentNullException.ThrowIfNull(shareAccess);
        ArgumentNullException.ThrowIfNull(buckets);
        Func<string, string> shareGivenTwice = ConfiguredShareReader.ShareGivenTwice;
        var shareNames = shares.Select(share => share.Share.Name).ToHashSet(StringComparer.Ordinal);
        return FindRepeat(shares.Select(share => (share.OptionName, share.Share.Name)), shareGivenTwice)
            ?? FindRepeat(buckets.Select(bucket => (bucket.OptionName, bucket.Bucket.Name)), ConfiguredBucketReader.BucketGivenTwice)
            ?? FindRepeat(shareAccess.Select(access => (access.OptionName, access.Access.Share)), shareGivenTwice)
            ?? FindFirst(buckets.Select(bucket => (bucket.OptionName, bucket.Bucket.Name)), shareNames.Contains, BucketIsAlsoAShare)
            ?? FindFirst(shareAccess.Select(access => (access.OptionName, access.Access.Share)), name => !shareNames.Contains(name), NamesNoShare);
    }

    private static Refusal? FindRepeat(IEnumerable<(string OptionName, string Name)> named, Func<string, string> givenTwice)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        return FindFirst(named, name => !seen.Add(name), givenTwice);
    }

    private static Refusal? FindFirst(
        IEnumerable<(string OptionName, string Name)> named, Func<string, bool> isRefused, Func<string, string> reason)
    {
        foreach (var (optionName, name) in named)
        {
            if (isRefused(name))
            {
                return new(optionName, reason(name));
            }
        }

        return null;
    }
}
