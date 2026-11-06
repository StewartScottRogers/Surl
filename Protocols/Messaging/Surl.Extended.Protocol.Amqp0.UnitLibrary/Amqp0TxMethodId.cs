namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// The method IDs of the XML's <c>tx</c> class (class ID <see cref="ClassId"/>; ADR-0112 decision 6).
/// </summary>
public static class Amqp0TxMethodId
{
    /// <summary>The <c>tx</c> class: 90.</summary>
    public const ushort ClassId = 90;

    /// <summary><c>tx.select</c> (90.10), client to server.</summary>
    public const ushort Select = 10;

    /// <summary><c>tx.select-ok</c> (90.11), server to client.</summary>
    public const ushort SelectOk = 11;

    /// <summary><c>tx.commit</c> (90.20), client to server.</summary>
    public const ushort Commit = 20;

    /// <summary><c>tx.commit-ok</c> (90.21), server to client.</summary>
    public const ushort CommitOk = 21;

    /// <summary><c>tx.rollback</c> (90.30), client to server.</summary>
    public const ushort Rollback = 30;

    /// <summary><c>tx.rollback-ok</c> (90.31), server to client.</summary>
    public const ushort RollbackOk = 31;
}
