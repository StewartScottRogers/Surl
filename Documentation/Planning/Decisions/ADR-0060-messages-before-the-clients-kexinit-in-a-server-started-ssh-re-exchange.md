# ADR-0060 — Messages before the client's KEXINIT in a server-started SSH re-exchange are held and answered after NEWKEYS

- **Status:** Accepted
- **Date:** 2026-09-30
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-30,
  in BL-237 (FR-039).
- **Amends:** [ADR-0051](ADR-0051-the-ssh-transport-host-keys-and-user-authentication.md)
  decision 2.2, choice 5, which answered such a message `DISCONNECT` 2 while no channel existed.

## Context

The server starts a key re-exchange itself once either direction has carried 1 GiB or an hour
has passed under one set of keys (ADR-0051 decisions 2.1 and 2.2.3). RFC 4253 section 9 lets the
client go on sending until it has seen the server's `KEXINIT`; after sending its own it sends
only key exchange messages until `NEWKEYS`. RFC 4253 section 7.1 has the server itself send
nothing but key exchange messages between its `KEXINIT` and its `NEWKEYS`. A long SCP or SFTP
transfer that passes 1 GiB therefore has `CHANNEL_DATA` (and window adjustments, requests and the
like) in flight when the server's `KEXINIT` goes out, and BL-161's `DISCONNECT` 2 cut such a
transfer off.

No byte upstream curl sends or expects is in question: RFC 4253 allows the client to send in that
window, and the only choice is how the server takes what it sends there.

## Decision

1. **Held, then answered in order.** In a server-started re-exchange, until the client's
   `KEXINIT` arrives, `IGNORE`, `DEBUG` and `UNIMPLEMENTED` are skipped as before, and every
   other message but a key exchange one is held. After both `NEWKEYS` the server's message loop
   reads the held messages first, in the order they came, then the wire, and answers each exactly
   as it would have outside the re-exchange (`SshTransportHandshake.ReadMessageAsync`). Why: the
   connection layer sees every message in order, and nothing it makes the server send - data,
   `WINDOW_ADJUST`, replies - can be written between the server's `KEXINIT` and `NEWKEYS`, with
   no second writer queue: the write gate the re-exchange already holds (ADR-0051 decision 2.2)
   keeps handlers' writes out, and the loop answers the held messages only after it.
2. **A held message's `UNIMPLEMENTED` carries its own sequence number**, the one its packet had
   before `NEWKEYS`, even after strict key exchange sets the numbers back to 0 (RFC 4253 section
   11.4 names the packet being answered).
3. **A key exchange message in that window is still `DISCONNECT` 2** (`NEWKEYS`, 21, and the
   method messages 30 to 49): the client has not started its side of the exchange, so none can
   rightly arrive. Note: `The client sent SSH message <n> before its KEXINIT in the key
   re-exchange.`
4. **What is held is bounded.** `SshReExchangeLimits.HeldBytes` defaults to every open channel's
   whole window (10 channels of 2 MiB, `SshConnectionProtocol.MaxOpenChannels` and
   `SshSessionChannel.WindowBytes`) and 1 MiB more for the messages around the data - what a
   client can rightly send before it reads the server's `KEXINIT`. Held payload bytes past it are
   `DISCONNECT` 2, note `The client sent more than <n> bytes of messages before its KEXINIT in the
   key re-exchange.` Why: without it a client that never sends `KEXINIT` could make the server
   hold messages without end; with it the cost is bounded by what the windows already allow.

## Consequences

- A transfer that passes the byte or time limit goes on across the re-exchange; the tests in
  `SshReExchangeTests` prove data sent before the client's `KEXINIT` is delivered and answered
  under the new keys, and that only `KEXINIT`, the method's reply and `NEWKEYS` are written in
  between.
- A client-started re-exchange is unchanged: the client's `KEXINIT` comes first, so there is no
  window.
