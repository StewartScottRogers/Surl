namespace Surl.Protocol.Abstractions;

/// <summary>
/// What a <see cref="StoreChangedEvent"/> says happened to its store.
/// </summary>
public enum StoreChange
{
    /// <summary>
    /// A file or object was written.
    /// </summary>
    Written,

    /// <summary>
    /// A file, object, message or mailbox was deleted.
    /// </summary>
    Deleted,

    /// <summary>
    /// A mail message was delivered to a mailbox.
    /// </summary>
    Delivered,

    /// <summary>
    /// A message was published to a topic.
    /// </summary>
    Published,

    /// <summary>
    /// A message was put on a queue.
    /// </summary>
    Enqueued,

    /// <summary>
    /// A delivered message was acknowledged.
    /// </summary>
    Acknowledged,

    /// <summary>
    /// An account was added.
    /// </summary>
    AccountAdded,

    /// <summary>
    /// An account was removed.
    /// </summary>
    AccountRemoved,

    /// <summary>
    /// A test seeded the store through the path a protocol uses.
    /// </summary>
    Seeded,

    /// <summary>
    /// The store was returned to its configured state.
    /// </summary>
    Reset,

    /// <summary>
    /// The store's contents were replaced with a snapshot's.
    /// </summary>
    Restored,
}
