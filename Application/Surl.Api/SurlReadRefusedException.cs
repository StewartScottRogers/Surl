namespace Surl.Api;

/// <summary>
/// A read-back was refused (ADR-0164, decisions 2 and 4): the item is past the 16 MiB bound, the limit is outside
/// 1 to 1000, or the path is one a request may not name. Never a cut-short answer. An
/// <see cref="InvalidOperationException"/>, as a seed's refusal is.
/// </summary>
public sealed class SurlReadRefusedException : InvalidOperationException
{
    internal SurlReadRefusedException(string reason)
        : base(reason)
    {
    }
}
