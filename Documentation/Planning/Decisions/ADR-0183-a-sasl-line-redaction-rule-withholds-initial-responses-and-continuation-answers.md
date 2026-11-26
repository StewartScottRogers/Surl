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

## Amendment 3 — IMAP LOGIN lines with no tag or with tabs (BL-1748)

Decided by Claude under Stewart's delegation, 2026-11-25. Widens which lines amendment 1 treats as `LOGIN`. The server
refuses all of these lines, but the client has already sent the password. SASL lines are unchanged.

1. **First or second word.** A line is a `LOGIN` command when its first or second word is `LOGIN`; words are separated by
   runs of spaces and tabs.
2. **Both words `LOGIN`.** The first may be the tag or the second may be the user name of a line with no tag, so both
   arguments after the second word are `****`. Losing a user name in the log is preferred to guessing which reading holds.
3. **Tabs.** When a tab is on the line that holds the password, a tab may have split one argument in two, so nothing after
   `****` is written, and a literal that line announces is `****` too.
4. **A new line is a new command.** A line that is not part of a literal never continues an earlier `LOGIN`, so the line
   after `A1 LOGIN alice` is logged as sent.

## Amendment 4 — SASL lines with tabs (BL-1749)

Decided by Claude under Stewart's delegation, 2026-11-25. Widens amendment 2, and replaces amendment 3's "SASL lines are
unchanged". No upstream curl measurement: curl sends single spaces, and only what the log writes changes.

1. **Spaces and tabs.** Runs of spaces and tabs separate the words of a client line, so
   `A1<tab>AUTHENTICATE<tab>PLAIN<tab><initial-response>` is logged with the response as `****`. Every separator before
   the mask is logged as received.
2. **Nothing after the mask.** When a tab is anywhere on a line that holds an initial response, a tab may have split the
   mechanism or the response in two, so nothing after `****` is written (as amendment 3, point 3, for `LOGIN`).

## Amendment 5 — a tag that is itself a SASL command word (BL-1754)

Decided by Claude under Stewart's delegation, 2026-11-25. Widens decision 2. No upstream curl measurement: curl's IMAP
tags are not command words, and only what the log writes changes.

1. **Two readings.** When a line's first word is one of the rule's command words and its second is `AUTH` or
   `AUTHENTICATE`, the first word may be the command (the third word is the initial response) or the tag (the fourth is).
2. **Both withheld.** The third and the fourth word are each `****`, so `AUTHENTICATE AUTHENTICATE PLAIN <response>` is
   logged as `AUTHENTICATE AUTHENTICATE **** ****`. Losing the mechanism in the log is preferred to guessing which
   reading holds (as amendment 3, point 2, for `LOGIN`). With three words the third is `****`; with a tab on the line
   nothing after the first `****` is written (amendment 4, point 2).
3. **Either second word.** The rule treats `AUTH` and `AUTHENTICATE` alike in second place, whatever its own command
   words are. The SOCKS tunnel's two SASL rules (ADR-0182, amendment 9) therefore write the same line for
   `AUTH AUTHENTICATE PLAIN <response>`; had only the rule that knows both words masked the response, the tunnel's rule
   could have logged the other's output, which holds it.

## Amendment 6 — a POP3 PASS line that starts with spaces or tabs (BL-1756)

Decided by Claude under Stewart's delegation, 2026-11-25. No upstream curl measurement: curl starts its lines with the
command word, and only what the log writes changes.

1. **Leading spaces and tabs.** The POP3 server's redaction rule finds `PASS` after any run of spaces and tabs at the
   start of the line, so `<tab>PASS<tab><password>` is logged as `<tab>PASS<tab>****`. Every byte before the mask is logged
   as received. The server refuses such a line, but the client has already sent the password.

## Amendment 7 — SASL lines with quotes (BL-1769)

Decided by Claude under Stewart's delegation, 2026-11-25. Widens amendment 4, point 2. No upstream curl measurement: curl
quotes nothing on an `AUTH` or `AUTHENTICATE` line, and only what the log writes changes.

1. **Nothing after the mask.** When a `"` or a `'` is anywhere on a line that holds an initial response, nothing after the
   first `****` is written. A SOCKS or HTTP proxy tunnel may carry Redis (ADR-0182, amendment 9), whose inline commands
   quote an argument that holds spaces, so `AUTH alice "a b"` is logged as `AUTH alice ****`.
2. **A quoted user name.** The words are still split at spaces and tabs, so in `AUTH "al ice" "a b"` the third word is
   part of the user name and the line is logged as `AUTH "al ****`. Losing the rest of the user name in the log is
   preferred to reading each dialect's quoting here; in `AUTH <user> <password>` with its arguments separated by spaces
   or tabs, the password never precedes the first mask.
3. **Not covered**, as before. A quoted command word (`"AUTH"`) is not a command word. A vertical tab, form feed or bare
   carriage return does not separate words here, though Redis reads each as white space, so a password after one may be
   logged. A quoted password given alone (`AUTH "a password"`) is split as `AUTH <user> <password>` is, so its first word
   is logged.

Rejected: reading Redis's quoting and escapes to find the password's end, which copies a parser into a rule that cannot
know the tunnel carries Redis; and a tunnel-only rule, because a quote is in no SASL mechanism name or base64 response,
so the SMTP, IMAP and POP3 servers lose nothing a conforming client sends.

## Amendment 8 — a quoted command word and VT, FF or CR separators in a tunnel (BL-1773)

Decided by Claude under Stewart's delegation, 2026-11-25. Closes amendment 7, point 3's quoted command word and VT, FF
and CR separators for tunnels. No upstream curl measurement: curl writes neither, and only what the log writes changes.

1. **A tunnel-only rule.** `TunnelRedisAuthLineRule` is the fifth rule `TunnelLineRedactionRule` shows every line to
   (ADR-0182, amendment 9). `SaslLineRedactionRule` is unchanged, so the SMTP, IMAP and POP3 logs are too.
2. **White space.** Space, tab, line feed, VT, FF and CR separate words. The first word is the command word. VT, FF and
   CR count wherever they stand, whether or not a given Redis dialect would split an argument there.
3. **Quoted command word.** The first word is `AUTH` when removing its `"` and `'` bytes leaves `AUTH`, without regard
   to case (`"AUTH"`, `'auth'`, `A"UTH"`). A first word holding both `"` and a backslash is taken for `AUTH` too,
   because an escape such as `\x41` may spell its letters.
4. **Which lines.** A line whose command word is quoted `AUTH`, and a line holding a VT, FF or CR anywhere whose command
   word is `AUTH`, quoted or not, provided something other than white space follows it. Every other line is left to the
   other rules.
5. **What is written.** The line up to and including the byte that ends the command word, then `****` and nothing more:
   `"AUTH" alice s3cret` is logged as `"AUTH" ****`. This loses the user name, and masks the one-argument form
   `AUTH<VT><password>` too. The mask starts before any `SaslLineRedactionRule` initial-response mask on the same line,
   so that rule's output is not the one logged; alone it would log `AUTH alice <VT> s3cret` as
   `AUTH alice **** s3cret`. A rule that masks from earlier in the line, such as a SASL continuation mask, still wins,
   and it writes nothing after its `****`.
6. **Still covered by amendment 7.** `AUTH "alice" s3cret` is logged as `AUTH "alice" ****`.

Not covered:

- A tunnel whose first byte does not make it text, a quote, space, tab, VT, FF or CR among them, does not reach the
  line rules (ADR-0182, amendments 9 and 16). Such a first line, one starting `"AUTH"` included, is not withheld by this
  rule; it applies once an earlier line has made the tunnel text.
- An unquoted one-argument `AUTH <password>` line with no VT, FF or CR is logged, as before.

Rejected: reading Redis's quoting to keep the user name, for amendment 7's reason.

Accepted limit: a tunnelled line of another protocol whose first word holds `"` and a backslash has the rest of the line
masked, which loses log detail only.

## Amendment 9 — one-argument AUTH and HELLO ... AUTH lines in a tunnel (BL-1774)

Decided by Claude under Stewart's delegation, 2026-11-25. Closes amendment 8's unquoted one-argument `AUTH <password>`
and amendment 7, point 3's quoted password given alone, and covers Redis's `HELLO ... AUTH`. No upstream curl
measurement: curl writes neither line, and only what the log writes changes. `SaslLineRedactionRule` is unchanged, so
the SMTP, IMAP and POP3 logs are too.

1. **Same rule.** `TunnelRedisAuthLineRule` withholds both. Amendment 8 still covers the lines it names.
2. **One argument.** On a line whose command word is `AUTH`, with one argument and nothing but white space after it, the
   line is written up to the byte that ends the command word, then `****`: `AUTH s3cret` is logged as `AUTH ****`.
3. **What one argument is.** It ends at the first white space outside quotes, so `AUTH "a password"` and `AUTH a" pass"`
   are one argument. A backslash escapes any byte between double quotes and only a single quote between single quotes, as
   in Redis. An argument whose quote is never closed runs to the end of the line.
4. **Mechanism names are not secrets.** The argument is logged as received when it is, unquoted and without regard to
   case, `ANONYMOUS`, `CRAM-MD5`, `DIGEST-MD5`, `EXTERNAL`, `GSS-SPNEGO`, `GSSAPI`, `LOGIN`, `NTLM`, `OAUTHBEARER`,
   `PLAIN`, `SCRAM-SHA-1`, `SCRAM-SHA-256`, `SCRAM-SHA-512` (each `SCRAM` name with `-PLUS` too), `XOAUTH2`, or FTP's
   `SSL`, `TLS`, `TLS-C` or `TLS-P` (RFC 4217). `AUTH PLAIN` with no initial response holds no secret.
5. **`HELLO`.** On a line whose command word is `HELLO`, quoted or not, the first later word that may be `AUTH` (its
   quotes removed, or holding `"` and a backslash) and has something after it ends what is shown: the line is written up
   to the byte that ends that word, then `****`, so `HELLO 3 AUTH alice s3cret` is logged as `HELLO 3 AUTH ****`.
   The user name is lost, for amendment 8's reason. The protocol version is not checked, so `HELLO AUTH alice s3cret`,
   which Redis refuses, is withheld too.

Not covered:

- A password of an `AUTH <password>` line that is itself one of point 4's names is logged.
- A line Redis refuses because a closing quote is followed by other bytes, such as `"AUTH"alice s3cret` or
  `HELLO 3 "AUTH"alice s3cret`, is not read as holding `AUTH`.
- Amendment 8's first limit stands: a tunnel whose first byte does not make it text, a first line starting `"HELLO"`
  included, does not reach the line rules.

Accepted limit: another protocol's tunnelled `AUTH` line with one argument that is none of point 4's names, an unlisted
SASL mechanism included, and a `HELLO` line with an `AUTH` word, are masked, which loses log detail only.

Rejected: keeping the user name of a `HELLO` line, which needs Redis's splitter in a rule that cannot know the tunnel
carries Redis; and masking every one-argument `AUTH`, which would hide the mechanism of every SASL exchange.
