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
`SaveChangesAsync` writes each new message file, then the whole index, then deletes the files
of messages no longer held; a write that throws leaves the change in memory and the next save
writes what is still needed. A store made by the constructor has no files. The servers call
`SaveChangesAsync` after every change and note what it throws; `Surl.Console` loads the store
(BL-207).

Intent, not yet built: streaming a message body into its pending file as a server reads it,
refusing that delivery with a typed outcome when the file cannot be written, and reading a
message's bytes from its file on fetch rather than holding them in memory (ADR-0050
decision 7; BL-226). Until then every message's bytes are held in memory, loaded at start.

This library references `Surl.Protocol.Abstractions.UnitLibrary` and
`Surl.Content.UnitLibrary` (for `IContentFileSystem`) and nothing else (ADR-0050 decision 1,
amending ADR-0002's table); protocol servers may reference it, and the SMTP, IMAP and POP3
servers do (BL-198, BL-201, BL-205).

Never touch the disk directly: read and write only through the injected
`IContentFileSystem`, so the tests need no disk, and write nothing outside the data
directory. Never construct a `Socket`, `TcpListener`, `UdpClient`, `SslStream` or
`HttpListener`.
