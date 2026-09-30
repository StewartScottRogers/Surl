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

Intent, not yet built: persistence under the data directory's `.surl/mail` folder with
`--directory`, loaded once at start (BL-191). `ChangeCount` rises with every change that
alters the store, which is where that work writes the index.

This library references `Surl.Protocol.Abstractions.UnitLibrary` and
`Surl.Content.UnitLibrary` (for `IContentFileSystem`) and nothing else (ADR-0050 decision 1,
amending ADR-0002's table); protocol servers may reference it, and the SMTP, IMAP and POP3
servers do (BL-198, BL-201, BL-205).

Never touch the disk directly: read and write only through the injected
`IContentFileSystem`, so the tests need no disk, and write nothing outside the data
directory. Never construct a `Socket`, `TcpListener`, `UdpClient`, `SslStream` or
`HttpListener`.
