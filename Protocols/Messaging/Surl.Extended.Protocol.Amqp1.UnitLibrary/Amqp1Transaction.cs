using Surl.MessageBus;

namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// One open local transaction of a connection (Part 4 section 4.1; ADR-0146 decisions 1.2 to 1.4): its
/// id, the coordinator link that declared it, and the messages posted in it, held unpublished until a
/// commit publishes them all or none.
/// </summary>
/// <param name="id">The transaction's number on the connection; its <c>txn-id</c> is these four bytes, big-endian.</param>
/// <param name="coordinator">The coordinator link that declared it, whose end rolls it back.</param>
internal sealed class Amqp1Transaction(uint id, Amqp1Link coordinator)
{
    private readonly List<BusBatchEntry> held = [];

    // The node name strings the held messages keep, by reference: one link's messages share one.
    private readonly HashSet<string> heldNodeNames = new(ReferenceEqualityComparer.Instance);

    /// <summary>The transaction's number on the connection.</summary>
    public uint Id => id;

    /// <summary>The coordinator link that declared it.</summary>
    public Amqp1Link Coordinator => coordinator;

    /// <summary>The messages posted in it, in posting order.</summary>
    public IReadOnlyList<BusBatchEntry> Held => held;

    /// <summary>The outcomes clients stated in it for the container's deliveries on links not yet ended, in the order stated (Part 4 section 4.4.2).</summary>
    public List<Amqp1HeldOutcome> HeldOutcomes { get; } = [];

    /// <summary>The deliveries the container sent in it on links not yet ended (Part 4 section 4.4.3), in sending order.</summary>
    public List<Amqp1OutgoingDelivery> Acquired { get; } = [];

    /// <summary>The bytes of the messages posted in it, as they arrived.</summary>
    public long HeldBytes { get; private set; }

    /// <summary>The characters of the node names the messages posted in it keep, each string counted once.</summary>
    public long HeldNodeNameChars { get; private set; }

    /// <summary>The <c>txn-id</c>: <see cref="Id"/> as four bytes, big-endian.</summary>
    public byte[] TransactionId
    {
        get
        {
            var bytes = new byte[sizeof(uint)];
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(bytes, id);
            return bytes;
        }
    }

    /// <summary>The number a <c>txn-id</c> names; <see langword="null"/> when it is not four bytes.</summary>
    public static uint? IdOf(byte[]? transactionId) =>
        transactionId is { Length: sizeof(uint) } ? System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(transactionId) : null;

    /// <summary>Holds a client's outcome for a delivery, replacing any outcome held for it.</summary>
    public void HoldOutcome(Amqp1OutgoingDelivery delivery, object? outcome, bool settled)
    {
        delivery.ForgetHeldOutcome();
        delivery.Held = new Amqp1HeldOutcome(this, delivery, outcome, settled);
        HeldOutcomes.Add(delivery.Held);
    }

    /// <summary>Holds a message posted in the transaction, whose topic is its node's name.</summary>
    public void Hold(BusBatchEntry entry, long bytes)
    {
        HeldNodeNameChars += NodeNameCharsAddedBy(entry.Message.Topic);
        heldNodeNames.Add(entry.Message.Topic);
        held.Add(entry);
        HeldBytes += bytes;
    }

    /// <summary>How many characters holding a message with this node name string would add to <see cref="HeldNodeNameChars"/>: none when a held message keeps the same string already.</summary>
    public int NodeNameCharsAddedBy(string nodeName) => heldNodeNames.Contains(nodeName) ? 0 : nodeName.Length;

    /// <summary>
    /// Drops the deliveries of a link that has ended from <see cref="Acquired"/> and
    /// <see cref="HeldOutcomes"/>: neither a commit nor a rollback changes them, and keeping them would
    /// keep the link.
    /// </summary>
    public void ForgetDeliveriesOf(Amqp1Link link)
    {
        HeldOutcomes.RemoveAll(outcome => outcome.Delivery.Link == link);
        Acquired.RemoveAll(delivery => delivery.Link == link);
    }
}
