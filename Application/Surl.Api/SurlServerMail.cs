using Surl.Composition;
using Surl.MailStore;
using Surl.Protocol.Abstractions;

namespace Surl.Api;

/// <summary>
/// A running <see cref="SurlServer"/>'s mail store, as a test seeds it (ADR-0124, decision 3): the one store
/// <c>smtp</c> delivers into and <c>imap</c> and <c>pop3</c> read. Thread-safe.
/// </summary>
public sealed class SurlServerMail
{
    private readonly ComposedStores stores;

    internal SurlServerMail(ComposedStores stores) => this.stores = stores;

    /// <summary>
    /// Delivers <paramref name="message"/> to <paramref name="address"/> as SMTP would after <c>RCPT TO:</c>, so
    /// aliases, quotas and events apply; the fault plan is not consulted. Publishes a
    /// <see cref="StoreChangedEvent"/> with <see cref="StoreChange.Seeded"/>.
    /// </summary>
    /// <param name="address">The recipient, such as <c>alice@example.com</c>.</param>
    /// <param name="message">The message as SMTP <c>DATA</c> carries it, headers first.</param>
    /// <param name="cancellationToken">Cancels saving a mail store on disk.</param>
    /// <returns>A task that completes once the message is in the store.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="address"/> is null.</exception>
    /// <exception cref="SurlSeedRefusedException">The recipient is unknown or invalid (<c>NoSuchAccount</c>,
    /// <c>InvalidAddress</c>, <c>RelayingDenied</c>), or the delivery was refused (<c>StoreFull</c> and the like).</exception>
    public async Task DeliverAsync(string address, ReadOnlyMemory<byte> message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(address);
        var result = await stores.SeedMailAsync(address, message, cancellationToken);
        if (result.Recipient != MailRecipientLookup.Deliverable)
        {
            throw new SurlSeedRefusedException(StoreKind.Mail, result.Recipient.ToString(), $"Mail for {address}");
        }

        if (result.Delivery != MailStoreOutcome.Succeeded)
        {
            throw new SurlSeedRefusedException(StoreKind.Mail, $"{result.Delivery}", $"Mail for {address}");
        }
    }
}
