# Surl.LineProtocol.UnitLibrary

Phase 1.

The CRLF line machinery the mail servers share (ADR-0050 decision 8). It is empty until
BL-192 lands; what follows is the intent. It will hold a command-line reader over one
connection (a line ends only at CRLF, bounded by `ExchangeLimits.MaxLineBytes` and the head
timeout, outcomes as values, pipelining kept, counted runs for IMAP literals, and a discard
of what is buffered before a `STARTTLS` or `STLS` upgrade), a dot-stuffed body reader for
SMTP `DATA` bounded by `ExchangeLimits.MaxUploadBytes`, a dot-stuffing body writer for POP3
`RETR` and `TOP`, a reply-line writer that refuses a CR, an LF or a non-printable byte, and a
SASL continuation reader.

This library references `Surl.Protocol.Abstractions.UnitLibrary` and nothing else
(ADR-0050 decision 1, amending ADR-0002's table); protocol servers may reference it, and
the SMTP, IMAP and POP3 servers do (BL-198, BL-201, BL-205).

It reads and writes only through the `IConnection` the listener seam hands a server, with
the exchange's cancellation token, and times with `ExchangeContext.TimeProvider`. Never
construct a `Socket`, `TcpListener`, `UdpClient`, `SslStream` or `HttpListener`, and never
touch the disk.
