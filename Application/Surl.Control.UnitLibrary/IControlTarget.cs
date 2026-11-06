using Surl.Protocol.Abstractions;

namespace Surl.Control;

/// <summary>
/// What the loopback control endpoint drives (ADR-0124 decision 7): the instance's event stream, its fault plan and
/// its stores. <c>Surl.Composition</c> implements it over a composed instance (BL-816); the endpoint never sees a
/// protocol server.
/// </summary>
/// <remarks>
/// A member refuses a request it cannot carry out by throwing <see cref="ControlRefusalException"/>, whose message
/// is sent to the client and so never holds a token or a password. Every change a member makes is published on the
/// instance's event stream by the store or fault plan it changes, not by the endpoint.
/// </remarks>
public interface IControlTarget
{
    /// <summary>
    /// Reads the instance's events, bytes included, from a subscription made when the read starts, until
    /// <paramref name="cancellationToken"/> is cancelled. The endpoint's own exchanges are left out.
    /// </summary>
    /// <param name="cancellationToken">Ends the subscription.</param>
    /// <returns>The events, in the stream's order.</returns>
    IAsyncEnumerable<SurlEvent> ReadEventsAsync(CancellationToken cancellationToken);

    /// <summary>Adds a fault rule given as one <c>--fault</c> item, refusing a bad item with the reader's text.</summary>
    /// <param name="item">The item, as <c>--fault</c> takes it.</param>
    /// <returns>The rule's ID, such as <c>f3</c>.</returns>
    string AddFault(string item);

    /// <summary>Lists the fault plan's rules in the order they are tried.</summary>
    /// <returns>The rules.</returns>
    IReadOnlyList<ControlFault> ListFaults();

    /// <summary>Removes a fault rule.</summary>
    /// <param name="faultId">The rule's ID.</param>
    /// <returns>Whether a rule had that ID.</returns>
    bool RemoveFault(string faultId);

    /// <summary>Delivers a message to a mail recipient as a seed.</summary>
    /// <param name="recipient">The recipient address.</param>
    /// <param name="message">The message, as RFC 5322 bytes.</param>
    /// <param name="cancellationToken">Cancels the delivery.</param>
    /// <returns>The delivery.</returns>
    Task SeedMailAsync(string recipient, ReadOnlyMemory<byte> message, CancellationToken cancellationToken);

    /// <summary>Writes a file into the content store as a seed, replacing any file at its path.</summary>
    /// <param name="path">The file's path, starting with <c>/</c>.</param>
    /// <param name="content">The file's bytes.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The write.</returns>
    Task WriteFileAsync(string path, ReadOnlyMemory<byte> content, CancellationToken cancellationToken);

    /// <summary>Deletes a file from the content store.</summary>
    /// <param name="path">The file's path, starting with <c>/</c>.</param>
    /// <param name="cancellationToken">Cancels the deletion.</param>
    /// <returns>Whether a file was at that path.</returns>
    Task<bool> DeleteFileAsync(string path, CancellationToken cancellationToken);

    /// <summary>Publishes or enqueues a message on the message bus as a seed.</summary>
    /// <param name="message">The message and where it goes.</param>
    /// <param name="cancellationToken">Cancels the publication.</param>
    /// <returns>The publication.</returns>
    Task PublishMessageAsync(ControlMessage message, CancellationToken cancellationToken);

    /// <summary>Adds an account, or replaces the password of one with the same name.</summary>
    /// <param name="user">The account name.</param>
    /// <param name="password">The password.</param>
    /// <param name="cancellationToken">Cancels the addition.</param>
    /// <returns>The addition.</returns>
    Task AddAccountAsync(string user, string password, CancellationToken cancellationToken);

    /// <summary>Removes an account.</summary>
    /// <param name="user">The account name.</param>
    /// <param name="cancellationToken">Cancels the removal.</param>
    /// <returns>Whether an account had that name.</returns>
    Task<bool> RemoveAccountAsync(string user, CancellationToken cancellationToken);

    /// <summary>Returns every store to its configured state (ADR-0124 decision 3).</summary>
    /// <param name="cancellationToken">Cancels the reset.</param>
    /// <returns>The reset.</returns>
    Task ResetAsync(CancellationToken cancellationToken);

    /// <summary>Takes a snapshot of every store.</summary>
    /// <param name="cancellationToken">Cancels the snapshot.</param>
    /// <returns>The snapshot.</returns>
    Task<IControlSnapshot> SnapshotAsync(CancellationToken cancellationToken);

    /// <summary>Restores every store from a snapshot this target took.</summary>
    /// <param name="snapshot">The snapshot.</param>
    /// <param name="cancellationToken">Cancels the restore.</param>
    /// <returns>The restore.</returns>
    Task RestoreAsync(IControlSnapshot snapshot, CancellationToken cancellationToken);
}
