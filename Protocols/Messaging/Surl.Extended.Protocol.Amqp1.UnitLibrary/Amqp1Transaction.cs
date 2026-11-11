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

    /// <summary>The transaction's number on the connection.</summary>
    public uint Id => id;

    /// <summary>The coordinator link that declared it.</summary>
    public Amqp1Link Coordinator => coordinator;

    /// <summary>The messages posted in it, in posting order.</summary>
    public IReadOnlyList<BusBatchEntry> Held => held;

    /// <summary>The bytes of the messages posted in it, as they arrived.</summary>
    public long HeldBytes { get; private set; }

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

    /// <summary>Holds a message posted in the transaction.</summary>
    public void Hold(BusBatchEntry entry, long bytes)
    {
        held.Add(entry);
        HeldBytes += bytes;
    }
}
