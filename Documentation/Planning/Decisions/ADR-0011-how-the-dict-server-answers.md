# ADR-0011 — How the DICT server answers

- **Status:** Accepted
- **Date:** 2026-09-28
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-28, in BL-033

## Context

BL-033 asks for a DICT server (RFC 2229) in `Surl.Protocol.Dict.UnitLibrary` that answers
every command pinned upstream curl 8.21.0 sends for its `dict://` URL forms, and asks the
plan to decide where definitions come from, the banner, and the reply to a word with no
definition.

Measured with `Record-CurlExchange.ps1 -Raw` against the pinned win-x64 build (SHA-256
`0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`), 2026-09-28:

| URL path | Lines curl sends, all at once, without waiting for the banner |
| --- | --- |
| `/d:hello` | `CLIENT libcurl 8.21.0`, `DEFINE ! hello`, `QUIT` |
| `/d:` | `CLIENT libcurl 8.21.0`, `DEFINE ! default`, `QUIT` |
| `/d:a%20b%22c:db:1` | `CLIENT libcurl 8.21.0`, `DEFINE db a\ b\"c`, `QUIT` |
| `/m:hel` | `CLIENT libcurl 8.21.0`, `MATCH ! . hel`, `QUIT` |
| `/m:hel:surl:prefix` | `CLIENT libcurl 8.21.0`, `MATCH surl prefix hel`, `QUIT` |
| `/hello` | `CLIENT libcurl 8.21.0`, `hello`, `QUIT` |
| `/show:db` | `CLIENT libcurl 8.21.0`, `show db`, `QUIT` |

curl reads nothing before it sends, prints every byte the server sends until the server
closes, and exits 0 whatever the replies say. It escapes a space, a quote or a backslash in
a word with a backslash rather than quoting the word. A bare path is sent as it stands with
each `:` turned into a space.

## Decision

1. **Definitions come from the content store.** The one database, named `surl` and
   described as `Files served by surl`, is the files directly in the served root: a file's
   name is a headword and its bytes are the definition. This is what makes
   `surl dict://…` serve something, the same content store HTTP serves, with no format of
   Surl's own to learn. `!` and `*` name the database too.
2. **Headwords are compared ordinally**, case and all, as the content store names files on
   every platform, so an answer is the same on Windows, Linux and macOS. A word that starts
   with `.` (ADR-0006 section 2 hides dot-files by default), a directory, and a name the
   content store refuses are not headwords.
3. **Strategies:** `exact` and `prefix`; `.`, the server's default, which curl sends for
   `m:`, is `prefix`.
4. **`MATCH` lists headwords.** It is the protocol's lookup, not a directory listing, so it
   is answered whatever `--list-directories` says; it lists only files, never directories
   or dot-files, as the server's database.
5. **Definition text** is the file's bytes with every line ending (LF, CRLF or a lone CR)
   sent as CRLF and every line that starts with `.` dot-stuffed (RFC 2229, section 2.4.1).
   It is streamed, not buffered. If the file shrinks while it is sent, the text ends at
   what was read; the terminating `.` line keeps the reply well formed, so nothing is
   aborted. If the file cannot be read at all once the `151` line is out (deleted, or
   access denied, in between), the connection is aborted, because no truthful end to the
   reply is left.
6. **The banner** is `220 surl DICT server <mime> <n@surl>`, with `n` the exchange id as
   the message id. It names no version (ADR-0006, section 3), and is sent as soon as the
   connection is served.
7. **Replies**, Surl's fixed text:

   | Request | Reply |
   | --- | --- |
   | `CLIENT` with text, `OPTION MIME` | `250 ok` |
   | `DEFINE` of a headword | `150 1 definitions retrieved`, `151 "word" surl "Files served by surl"`, the text, `.`, `250 ok` |
   | `DEFINE` of a word with no definition, `MATCH` of nothing | `552 no match` |
   | `MATCH` of headwords | `152 n matches found`, one `surl "headword"` line each, `.`, `250 ok` |
   | `SHOW DB`/`DATABASES`, `STRAT`/`STRATEGIES`, `INFO surl`, `SERVER` | `110`, `111`, `112`, `114` with their text, `.`, `250 ok` |
   | `HELP` | `113 help text follows`, the commands, `.`, `250 ok` |
   | `STATUS` | `210 status ok` |
   | `QUIT` | `221 bye`, then the connection closes |
   | a command RFC 2229 does not define, a blank line | `500 unknown command` |
   | `AUTH`, `SASLAUTH`, `SASLRESP` | `502 command not implemented` |
   | wrong parameter count, an unclosed quote, a trailing lone backslash, an unknown `SHOW` or `OPTION` subject | `501 syntax error, illegal parameters` |
   | an unknown database | `550 invalid database, use "SHOW DB" for list of databases` |
   | an unknown strategy | `551 invalid strategy, use "SHOW STRAT" for a list of strategies` |

   After `OPTION MIME`, each text begins with `Content-type: text/plain; charset=utf-8`,
   `Content-transfer-encoding: 8bit` and a blank line. A word from the client is echoed
   only inside a quoted string, with `"` and `\` escaped.
8. **Line limit:** ADR-0006 exists, so its `--max-line` limit applies, read from
   `ExchangeContext.Limits.MaxLineBytes` (8192 by default, line ending included, 0 for no
   limit). A longer line is answered `500 line too long` and the connection is closed
   without reading the rest.
9. **Head timeout and connection refusal** (added 2026-09-29 in BL-051, decided by Claude
   under Stewart's delegation). ADR-0006 sections 1 and 5 give the codes; the text is
   Surl's own:

   | Limit | Reply |
   | --- | --- |
   | A command line not complete within `ExchangeContext.Limits.HeadTimeout` | `420 timed out waiting for a command`, then the connection closes |
   | A connection past a connection limit, either `ConnectionRefusal` | `420 server temporarily unavailable` (RFC 2229's own text for 420), then the connection closes |

   The first line's clock starts when the connection is served, so a client that never
   sends a command is answered after the head timeout; every later line's starts at its
   first byte (for a byte already read behind the previous line, when the server turns to
   it), so the wait between commands is bounded only by `Surl.Core`'s idle timeout. Both
   limit replies, `500 line too long` and the timed-out `420`, are written within a
   one-second write deadline and then writes are completed; a peer that does not take the
   reply in time is closed all the same, never aborted. The refusal is one fixed text for
   both refusals because a client learns nothing useful from which limit it hit, and
   RFC 2229 has the one code for both. Each was fed to the pinned build first (`refused`,
   `head-timeout`, `line-too-long` in `Surl.Protocol.Dict.UnitTests/Fixtures/README.md`);
   curl exited 0 with an empty stderr each time.

Every reply for the recorded cases (`define-hello`, `match-hel`, `bare-hello`,
`define-missing`, `show-db`) was fed to the pinned build before it was pinned, and curl
exited 0 with an empty stderr (`Surl.Protocol.Dict.UnitTests/Fixtures/README.md`).

## Alternatives considered

- **A built-in reply for every word.** Rejected: a server whose answers cannot be changed
  is no use to anyone testing a DICT client, and the content store is already there.
- **A dictionary file in dictd's format.** Rejected: an index format of its own for a
  first server, where one file per headword needs nothing new.
- **Case-insensitive headwords.** Rejected: `DEFINE` would need a directory listing for
  every lookup, and the answer would differ by platform.

## Consequences

- BL-039 composes `DictProtocolServer` with the same content store as HTTP.
- BL-051 added the head timeout, the `420` refusal and their recordings (item 9).
