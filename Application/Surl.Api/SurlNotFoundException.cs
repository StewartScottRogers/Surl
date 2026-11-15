namespace Surl.Api;

/// <summary>
/// A read-back asked for something a store does not hold (ADR-0164, decision 5): an owner, mailbox or message
/// UID, a file or folder, a queue or a topic log that is not there. An <see cref="InvalidOperationException"/>,
/// as a seed's refusal is. A retained message that is absent is not this exception: the read gives <see langword="null"/>.
/// </summary>
public sealed class SurlNotFoundException : InvalidOperationException
{
    internal SurlNotFoundException(string reason)
        : base(reason)
    {
    }
}
