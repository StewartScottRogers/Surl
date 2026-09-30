# Surl.MailStore.UnitLibrary

Phase 1.

The mail store the SMTP, IMAP and POP3 servers share (ADR-0050 decisions 2 to 7): SMTP
delivers into it, IMAP and POP3 serve from it. `MailboxStore` keeps a mailbox set per owner
in memory (BL-190): owners from the account names, or the one anonymous owner under
`--allow-anonymous`; `LookUpRecipient` maps an SMTP `RCPT TO` path to an owner, `ViewFor`
gives an IMAP or POP3 login its view, and `LockMaildrop` takes POP3's per-owner lock with a
view fixed at login. It is bounded by constants passed to its constructor (messages, total
message bytes, mailboxes, one message's bytes), and every refusal a peer can cause is a
`MailStoreOutcome`, never an exception.

The type is `MailboxStore`, not `MailStore` as ADR-0050 decision 6 writes it: a type named
`MailStore` inside the namespace `Surl.MailStore` would be shadowed by the namespace in every
other `Surl.*` namespace.

Persistence (BL-191, ADR-0050 decision 7): `MailboxStore.LoadAsync` reads the store from a
`MailStoreFiles` - `<state folder>/index` and one file per distinct message under
`messages/`, named by its file number in 16 lower-case hex digits - and refuses a malformed
store with `MailStoreLoadException` (the file at fault and the reason; the composition root
answers it with `CouldNotReadFile`, 37). `MailStoreIndex` is the index's byte format.
`SaveChangesAsync` writes the index, then deletes the files of messages no longer held; an
index write that throws leaves the change in memory and the next save writes it. A store made
by the constructor has no files and holds every message's bytes in memory. The servers call
`SaveChangesAsync` after every change and note what it throws; `Surl.Console` loads the store
(BL-207).

Streaming (BL-227, ADR-0050 decision 7): `CreatePendingMessage` gives a `PendingMessage`
whose `Body` stream writes straight into `messages/.pending-<guid>`; `Deliver` and `Append`
taking it rename that file to the message's number when they store it, delete it when they
refuse it, and refuse a pending file that cannot be created, written, closed or renamed with
`StorageFailed` (`PendingMessage.StorageFailure` holds the exception's message). A write to
`Body` never throws for a failing file, so a server can read the rest of the body off the
wire. With a data directory a message's bytes are read from its file on fetch
(`OpenMessage`, `FetchMessage`, `MaildropLock.OpenMessage` and `ReadMessage`), never loaded
at start; a POP3 maildrop lock pins its messages' files until it is released. The overloads
taking a whole message as bytes stay for the servers not yet streaming: `Append` routes them
through a pending message, and `Deliver` holds them in memory until the next save writes their
file (BL-191's shape, so the SMTP server's outcomes do not change under it).

This library references `Surl.Protocol.Abstractions.UnitLibrary` and
`Surl.Content.UnitLibrary` (for `IContentFileSystem`) and nothing else (ADR-0050 decision 1,
amending ADR-0002's table); protocol servers may reference it, and the SMTP, IMAP and POP3
servers do (BL-198, BL-201, BL-205).

Never touch the disk directly: read and write only through the injected
`IContentFileSystem`, so the tests need no disk, and write nothing outside the data
directory. Never construct a `Socket`, `TcpListener`, `UdpClient`, `SslStream` or
`HttpListener`.
