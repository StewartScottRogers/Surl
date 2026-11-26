# ADR-0183 — `SaslLineRedactionRule` withholds SASL initial responses and continuation answers for the mail servers

- **Status:** Accepted
- **Date:** 2026-11-20
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-20, in BL-1428. No
  package, no threshold change, no download.
- **Builds on:** [ADR-0182](ADR-0182-surl-withholds-client-passwords-and-tokens-from-its-exchange-log-and-trace-dumps.md)
  decisions 1 and 6 (the `****` form and the line-rule seam) and
  [ADR-0050](ADR-0050-the-mail-store-and-the-line-machinery-the-mail-servers-share.md) (the shared mail line library).

## Decision

1. **Name and home.** `Surl.LineProtocol.SaslLineRedactionRule`, an `ILineRedactionRule` in
   `Surl.LineProtocol.UnitLibrary`. Each mail server wraps one in a `LineBytesRedactor` per exchange (BL-1429
   SMTP, BL-1430 IMAP, BL-1431 POP3), giving it the words that start SASL (`AUTH`; `AUTHENTICATE`) and the
   server's continuation prefix (`334 `; `+ `).
2. **Initial response.** On a client line `[tag] AUTH <mechanism> <initial-response>` the third token (after the
   optional IMAP tag) is replaced by `****`; the command and mechanism stay: `AUTH PLAIN ****`. A line with no
   initial response is unchanged.
3. **Continuation answers.** After the server sends a line that starts with the continuation prefix, the client's
   next line is replaced by `****`. Any other line the server sends clears the mark, and a server that asks again
   (`AUTH LOGIN`'s second prompt) marks the next line again, so the mark lasts as long as the exchange keeps
   asking. A client line with no continuation pending is logged unchanged.
4. **Every answer is redacted, not only PLAIN and LOGIN.** CRAM-MD5, SCRAM and GSSAPI answers hold no password in
   clear, but a line rule does not know the mechanism and a log loses nothing a reader needs from them. A lone `*`
   (cancel) and an empty line hold nothing and are logged as they are.
5. **Not covered.** An IMAP literal announced by `+ ` is a continuation to this rule, so its first line is
   masked; BL-1430 decides how to keep a literal's bytes out of the rule.

## Amendment 1 — IMAP literals (BL-1430)

Decided by Claude under Stewart's delegation, 2026-11-20. Settles decision 5.

1. **Where.** `Surl.Protocol.Imap.ImapLineRedactionRule`, internal, wrapped in a `LineBytesRedactor` by
   `ImapProtocolServer.CreateRedactor`. It owns one `SaslLineRedactionRule` (`AUTHENTICATE`, `+ `).
2. **Literals stay out of the SASL rule.** A command line ending in `{n}` or `{n+}` starts a literal: the next `n`
   bytes, across as many CR LF lines as they span, never reach the SASL rule, and the server's `+` that invites a
   synchronizing literal is not shown to it. The bytes after the literal on its last line are the rest of the command.
3. **`LOGIN`.** The second argument (atom, quoted string or literal) is `****`; a literal password is `****` once per
   line it spans. Every other literal, an `APPEND` message included, is logged unchanged, so message text such as
   `LOGIN a b` is never touched.
4. **Refused literal.** After a synchronizing marker, a first server line that does not start with `+` cancels the
   literal, so the next client line is read as a command.
5. **Known limit.** The line seam holds at most 64 KiB without a CR LF; a longer run inside a literal is written as `****`
   by `LineBytesRedactor` and the rule does not see it, so its literal count can then run long. Only the log is affected.

## Amendment 2 — runs of spaces (BL-1738)

Decided by Claude under Stewart's delegation, 2026-11-25. Narrows how decision 2 counts tokens.

1. **A run of spaces separates two tokens.** `AUTH PLAIN  <initial-response>`, with extra spaces before the mechanism,
   the response or the command word, is logged with the response as `****`; every space is logged as received. Upstream
   curl sends single spaces, but the log must withhold the password of any client, and of a line the server refuses.
