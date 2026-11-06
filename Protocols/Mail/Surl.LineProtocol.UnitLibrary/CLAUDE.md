# Surl.LineProtocol.UnitLibrary

Phase 3.

The CRLF line machinery the mail servers share (ADR-0050 decision 8), built in BL-192:

- `CrlfLineReader` - one buffer over one connection. `ReadLineAsync` ends a line only at
  CRLF (a bare LF or CR is part of the line), bounded by `ExchangeLimits.MaxLineBytes` with no
  byte read past it, and by the head timeout (from creation for the first line, from the first
  byte for later ones); outcomes are `CrlfLineReadOutcome` values, and pipelined bytes stay
  buffered. `ReadCountedRunAsync` copies an IMAP literal, `ReadDotStuffedBodyAsync` reads SMTP
  `DATA` unstuffed to CRLF `.` CRLF bounded by `ExchangeLimits.MaxUploadBytes`,
  `ReadSaslContinuationAsync` reads and classifies a SASL continuation, and `DiscardBuffered`
  throws away what is buffered before a `STARTTLS` or `STLS` upgrade. Counted runs and bodies
  are not under the head timeout.
- `DotUnstuffer` - the body reader's state machine (internal).
- `DotStuffedBodyWriter` - POP3 `RETR` and `TOP`: stuffs after CRLF only, adds a CRLF to a
  non-empty message that lacks one, ends with `.` CRLF; an empty message is `.` CRLF alone.
- `ReplyLineWriter` - one printable-ASCII line and CRLF; throws `ArgumentException` otherwise.
- `SaslContinuationLine` - `*` cancels, empty is an empty response, whitespace or anything
  else not base64 is `NotBase64`.
- `MailUpstreamTestCaseReplies` (BL-875, ADR-0080 decision 2) - a test seam, never composed by
  the shipped `surl`: one upstream test case's `<reply>` parts and `<servercmd>` lines as the
  IMAP, POP3 and SMTP server halves play them, with a record of every command line read and
  every message uploaded. `FindData` picks `<dataNUM>` for a name whose first number is over
  10000 (NUM its remainder of 10000), else `<data>`. `MailUpstreamTestCaseServerCommands`
  (internal) reads `Testnum`, `REPLY`, `REPLYLF`, `COUNT`, `CAPA`, `AUTH` and `POSTFETCH`, and
  throws `NotSupportedException` for any other word (`servercmd-unsupported:<word>`).

This library references `Surl.Protocol.Abstractions.UnitLibrary` and nothing else
(ADR-0050 decision 1, amending ADR-0002's table); protocol servers may reference it, and
the SMTP, IMAP and POP3 servers do (BL-198, BL-201, BL-205).

It reads and writes only through the `IConnection` the listener seam hands a server, with
the exchange's cancellation token, and times with `ExchangeContext.TimeProvider`. Never
construct a `Socket`, `TcpListener`, `UdpClient`, `SslStream` or `HttpListener`, and never
touch the disk.
