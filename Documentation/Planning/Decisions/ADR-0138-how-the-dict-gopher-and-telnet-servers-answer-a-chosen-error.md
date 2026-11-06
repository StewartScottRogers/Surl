# ADR-0138 — How the DICT, Gopher and TELNET servers answer a chosen error

- **Status:** Accepted
- **Date:** 2026-11-05
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-05, in BL-798.
- **Builds on:** [ADR-0124](ADR-0124-surl-is-a-stand-in-for-tests.md) decision 4 (the fault model, the `error`
  kind and its reply table), [ADR-0011](ADR-0011-how-the-dict-server-answers.md) (DICT),
  [ADR-0012](ADR-0012-how-the-gopher-server-answers-a-selector.md) (Gopher), [ADR-0025](ADR-0025-how-the-telnet-server-answers.md) (TELNET), and [ADR-0135](ADR-0135-how-the-ssh-and-smb-servers-answer-a-chosen-error.md)
  (the same question for SSH and SMB, whose decisions 4 to 6 this one follows).
- **Acted on by:** BL-798.

## Context

ADR-0124 decision 4 gives DICT, Gopher and TELNET their `error` reply shapes - `dict` "a status 500 to 599 and
text", `gopher` "text, sent as a Gopher error item (`3`)", `telnet` "one line of text, sent and then the
connection closed" - and says the command is DICT's (`DEFINE`, `MATCH`) and none for the other two, the path
"the word or database" for DICT and "the selector" for Gopher. It does not say where each server asks the
plan, which word a DICT command's path is, how a Gopher error item is framed, or when TELNET asks at all.

## Decision

1. **DICT** (`DictCommandResponder`, `DictCommandFaults`). The plan is asked once per command line whose
   command Surl recognizes, after the line is split and before the command's own checks and the content store,
   with scheme `dict`, the command in capitals and a path: the word of a `DEFINE` with three words or a `MATCH`
   with four, the database of a `SHOW INFO` with three, otherwise none. A line that does not split, a blank
   line or an unknown command asks nothing. DICT has no login, so the point names no account and a rule scoped
   by `account` never matches. A chosen error is the reply as given and CRLF in place of the command's own
   reply; the connection stays open for the next command, as for any other DICT error status.
2. **Gopher** (`GopherProtocolServer`, `GopherSelectorFaults`). The plan is asked once per connection, after
   the selector line is read and before the content store, with the listen URL's scheme (`gopher` or
   `gophers`) and the path the selector maps to as a request path (`/a` for selector `/a` or `a`, the text
   after a TAB dropped). No command and no account. A connection on which no selector is read asks nothing.
   A chosen error is the menu `3<reply>\t\terror.host\t1\r\n.\r\n` - the same item shape as the server's own
   error menu, with the reply as its display string, UTF-8 encoded - and then writes are completed.
3. **TELNET** (`TelnetProtocolServer`, `TelnetSession`, `TelnetLineFaults`). TELNET has no error reply, so
   ADR-0124's planning default stands: the plan is asked once per line of data the client completes, `quit`
   included, before the line is echoed, with scheme `telnet` and nothing else - no command, path or account,
   so a rule scoped by any of those never matches a TELNET line. A line after `quit` is never read, so it asks
   nothing. A chosen error is the reply, UTF-8 encoded, and CRLF in place of the line's echo, after which the
   session is over and the connection closed; any negotiation answers already due go first. Lines count from
   1 per session.
4. **What fits.** DICT reads three digits 500 to 599, one space and non-empty text; Gopher non-empty text with
   no TAB, CR or LF (a TAB would split the item, a line break end it); TELNET non-empty text with no CR or LF.
   A reply that does not fit - a `Surl.Api` hook can hand one over unchecked, and `Surl.Cli`'s check of
   `gopher` and `telnet` replies is only "non-empty" - is noted in the exchange log and not applied: the
   command is answered as without the rule and no event is published (ADR-0135 decision 4).
5. **Only the `error` kind.** As ADR-0135 decision 5: a `delay` or `drop` rule given `stage=command` that
   matches is noted as not applied and the command goes on; applying them here is BL-912, after BL-901 adds the seams.
6. **Events and the point.** Each application publishes one `FaultAppliedEvent` (stage `Command`, the
   exchange's ID) and notes the answer. Its target is DICT's path, or the command when there is none;
   Gopher's path; TELNET's `line <n>` - never the line's text, which may hold anything a person typed. The
   `FaultPoint` carries the exchange ID, the listen URL formatted as the serving engine formats it (bound port
   included), the scheme, and the path and command where decisions 1 to 3 name them. Each library has its own
   `FormatListenUrl`, because protocol servers share no code but the horizontal libraries (BL-912 replaces them with BL-901's shared one in
   `Surl.Protocol.Abstractions`).

## What upstream curl does with these answers

Measured with pinned upstream curl 8.21.0 (win-x64) and `Record-CurlExchange.ps1`, each fed exactly the bytes
the server writes for the case; the recordings are the fixtures the tests replay:

| Case | Fixture | curl | Exit |
| --- | --- | --- | --- |
| `dict://…/d:word:*`, `DEFINE * word` answered `554 try later` | `Surl.Protocol.Dict.UnitTests/Fixtures/fault-define` | prints `554 try later` among the replies | 0 |
| `gopher://…/0/a`, selector `/a` answered with `3try later` | `Surl.Protocol.Gopher.UnitTests/Fixtures/fault-selector` | prints the error menu | 0 |
| `telnet://…` with input `hello`, `quit`, line 1 answered `try later` and closed | `Surl.Protocol.Telnet.UnitTests/Fixtures/fault-line` | prints the banner and `try later` | 0 |

None of the three protocols carries an error to curl's exit code: curl's DICT and Gopher handlers print what
the server sends, and TELNET is a byte stream. A test sees the fault in curl's output and on the event stream,
not in its exit code.

## Consequences

A test can make one DICT lookup, one Gopher selector or one TELNET session fail on purpose and see it on the
event stream; no store changes. A TELNET rule cannot pick one line by content, only by `times` and order -
the protocol gives the plan nothing else to match. The command-stage delay and drop are BL-912.
