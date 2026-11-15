namespace Surl.Control;

/// <summary>One mailbox of an owner, as the read-back routes list it (ADR-0164 decision 1).</summary>
/// <param name="Name">The mailbox's name.</param>
/// <param name="MessageCount">How many messages it holds.</param>
/// <param name="UnseenCount">How many of them lack <c>\Seen</c>.</param>
/// <param name="UidValidity">Its <c>UIDVALIDITY</c>.</param>
public sealed record ControlMailbox(string Name, int MessageCount, int UnseenCount, uint UidValidity);
