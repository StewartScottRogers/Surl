# Upstream test-case exclusions

Upstream test cases that fail against Surl only because the byte they verify is the upstream test
server's arbitrary choice - not the protocol's specification, not what curl needs (a banner's
wording, a fixed `MDTM` time) - and so are skipped as `test-server-choice`
([ADR-0080](../Planning/Decisions/ADR-0080-how-surl-plays-the-server-half-of-upstream-curls-test-cases.md),
decision 5). Anything else that fails is a Surl defect until measurement shows otherwise, and
becomes a task.

One row per case. The rows below were found by BL-879's Windows run of the mail cases
(2026-11-05). Since BL-689, `UpstreamTestCaseExclusions.Parse` reads the `Case` column (`test<N>`
or `<N>`) and every run passes the set to `UpstreamTestCaseSelectionRules.ForBuild` and `ForClient`
as `TestServerChoices`, so these cases skip `test-server-choice`. Fast tests
(`UpstreamTestCasePassRateStatementTests`) fail when a case here is also on a passing list, and
when a platform's `test-server-choice` in the pass-rate file is not this table's row count; the
upstream test-case run fails when its own `test-server-choice` count is not.

| Case | Byte verified | Upstream's choice | Surl's answer | Why a correct server need not send upstream's |
| --- | --- | --- | --- | --- |
| 802 | IMAP `SELECT`'s `UIDVALIDITY`, which curl matches against the URL's `;UIDVALIDITY=3857529045` before it sends `FETCH` | `ftpserver.pl` always answers `[UIDVALIDITY 3857529045]` | its own mailbox's `UIDVALIDITY`, so curl stops before `FETCH` and `<protocol>` differs | RFC 9051 section 2.3.1.1 lets each server choose its mailbox's `UIDVALIDITY`; the case's URL names upstream's value |
| 818 | stdout of `-X NOOP`: `* 22 EXPUNGE` | `ftpserver.pl` answers every `NOOP` with a fixed untagged `* 22 EXPUNGE` | a tagged `OK` with no untagged response, since no message was expunged | RFC 9051 section 6.1.2: `NOOP` returns pending updates, of which an empty mailbox has none; 22 is upstream's invention |
| 861 | stdout of POP3 `-X UIDL`: `1 1`, `2 2`, `3 4` | `ftpserver.pl` answers `UIDL` with a hard-coded listing, as the case's own comment says | the unique IDs of the mailbox it serves, which holds no message, so no listing line | RFC 1939's unique-id is the server's to choose; the case pins upstream's hard-coded table |
| 897 | the `-D` header file: the greeting's ASCII-art banner | `ftpserver.pl`'s IMAP greeting is curl's ASCII-art logo | `* OK surl ready` | RFC 9051 section 7.1.1: the greeting's text is free; the 24 FTP cases of BL-776 verify the same banner |
