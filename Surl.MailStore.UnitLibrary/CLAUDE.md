# Surl.MailStore.UnitLibrary

Phase 1.

The mail store the SMTP, IMAP and POP3 servers share (ADR-0050 decisions 2 to 7): SMTP
delivers into it, IMAP and POP3 serve from it. It is empty until BL-190 and BL-191 land;
what follows is the intent. It will keep a mailbox set per owner, bounded by constants passed
to its constructor (messages, total message bytes, mailboxes), in memory without
`--directory` and persisted under the data directory's `.surl/` service-state folder with
it, loaded once at start.

This library references `Surl.Protocol.Abstractions.UnitLibrary` and
`Surl.Content.UnitLibrary` (for `IContentFileSystem`) and nothing else (ADR-0050 decision 1,
amending ADR-0002's table); protocol servers may reference it, and the SMTP, IMAP and POP3
servers do (BL-198, BL-201, BL-205).

Never touch the disk directly: read and write only through the injected
`IContentFileSystem`, so the tests need no disk, and write nothing outside the data
directory. Never construct a `Socket`, `TcpListener`, `UdpClient`, `SslStream` or
`HttpListener`.
