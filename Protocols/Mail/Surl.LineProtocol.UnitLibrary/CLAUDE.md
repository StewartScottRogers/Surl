# Surl.LineProtocol.UnitLibrary

Phase 3.

The CRLF line machinery the mail servers share (ADR-0050 decision 8), built in BL-192, and the
tunnel redaction the SOCKS server and the HTTP proxy share, for text lines (ADR-0182, amendment 20)
and for binary logins (amendments 23 and 25):

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
- `SaslLineRedactionRule` (BL-1428, ADR-0183) - an `ILineRedactionRule` for `LineBytesRedactor`:
  masks the initial response on `[tag] AUTH mech <b64>` as `****`, however many spaces and tabs
  separate those words (ADR-0183, amendments 2, 4 and 7; on a line with a tab, `"`, `'` or a bare
  CR (ADR-0182, amendment 33) nothing after `****` is written), and the client line after a server line starting with the
  continuation prefix (`334 `, `+ `); `*`, empty and unprompted lines pass unchanged. When a
  command word is followed by `AUTH` or `AUTHENTICATE`, the third and fourth words are both masked
  (amendment 5).
- `TunnelLinesRedactor` (BL-1767, ADR-0182 amendment 20) - the exchange-log redactor for the text a
  client sends through a SOCKS tunnel or an HTTP proxy `CONNECT` tunnel: a `LineBytesRedactor` over
  `TunnelLineRedactionRule`, ending a line at a bare line feed too. That rule shows every line to
  `TunnelPasswordAndCredentialHeaderLineRule` (`PASS` and `ACCT` arguments, the credentials of the
  `HttpWithheldHeaderNames.Credentials` headers after the scheme word, the whole value of the
  `ApiKeysAndSessionTokens` headers, and folded lines of either; BL-1772, amendment 33: a header
  is read at the line's start and after every bare CR, with or without blanks before its name, and
  so is a command (BL-1783 and BL-1785, amendments 34 and 36), the line masked from the earlier
  secret),
  `TunnelImapLoginLineRule` (the
  IMAP `LOGIN` password and its literals, `LOGIN` the first word (no tag) or the second, and
  everything after `LOGIN LOGIN` (ADR-0182, amendment 29); each line read with words separated by
  spaces and tabs and again by spaces only, masking from the earlier password), two
  `SaslLineRedactionRule`s (`334 `, `+ `; a bare `+` or `334` line, and one split between the
  origin's reads, count) and `TunnelRedisAuthLineRule` (BL-1773 and BL-1774, ADR-0183 amendments 8
  and 9: an inline Redis `AUTH` line with a quoted command word, a VT, FF or CR, or one argument
  that is no SASL or FTP security mechanism name is logged up to the byte after the command word,
  then `****`, and a `HELLO` line up to the byte after its `AUTH` word, then `****`), and logs what
  the one withholding from earliest in the line writes.
- `TunnelBinaryLoginRedactors` (BL-1765, ADR-0182 amendment 23) - the redactor by the byte where a
  tunnel's login may start, else none: `0x10` gives `TunnelMqttLoginRedactor` (BL-1744, amendment 28:
  the MQTT 3.1.1 or 5 `CONNECT` password and every MQTT 5 Authentication Data value, in a `CONNECT` or
  `AUTH` packet, as `00 04 ****`, the Property Length and Remaining Length rewritten to match; it frames
  every packet, holds only `CONNECT` and `AUTH` whole, and masks one it cannot walk; over
  `TunnelMqttLoginBodyMasker`, `TunnelMqttPropertiesMasker` and `TunnelMqttFields`) and `0x30` gives
  `TunnelLdapBindRedactor` (every bind's authentication as its tag, `04`, `****`; over
  `TunnelBerHeader`). Neither holds more than 64 KiB (amendment 10).
- `TunnelBinaryLoginStartFinder` (BL-1780, ADR-0182 amendment 25) - where a tunnel's binary login
  starts, for both servers: at its first byte, or at the byte after a first line starting `PROXY `
  (curl's `--haproxy-protocol`), several such lines in a row each followed. It gives the redactor
  `TunnelBinaryLoginRedactors` chooses for that byte.
- `MailUpstreamTestCaseReplies` (BL-875, ADR-0080 decision 2) - a test seam, never composed by
  the shipped `surl`: one upstream test case's `<reply>` parts and `<servercmd>` lines as the
  IMAP, POP3 and SMTP server halves play them, with a record of every command line read and
  every message uploaded. `FindData` picks `<dataNUM>` for a name whose first number is over
  10000 (NUM its remainder of 10000), else `<data>`. `MailUpstreamTestCaseServerCommands`
  (internal) reads `Testnum`, `REPLY`, `REPLYLF`, `COUNT`, `CAPA`, `AUTH`, `POSTFETCH` and
  `SLOWDOWN` (`IsSlowedDown`; BL-1137, applied by the POP3 half through `BytePacedConnection`, an
  `IConnection` decorator writing one byte at a time with a 10 ms `TimeProvider` wait after each), and
  throws `NotSupportedException` for any other word (`servercmd-unsupported:<word>`).

This library references `Surl.Protocol.Abstractions.UnitLibrary` and nothing else
(ADR-0050 decision 1, amending ADR-0002's table); protocol servers may reference it, and
the SMTP, IMAP and POP3 servers (BL-198, BL-201, BL-205), the SOCKS server (ADR-0182
amendment 9) and the HTTP proxy (amendment 16) do. It holds its own small MQTT and BER walks: it
may not reference the MQTT or LDAP server.

It reads and writes only through the `IConnection` the listener seam hands a server, with
the exchange's cancellation token, and times with `ExchangeContext.TimeProvider`. Never
construct a `Socket`, `TcpListener`, `UdpClient`, `SslStream` or `HttpListener`, and never
touch the disk.
