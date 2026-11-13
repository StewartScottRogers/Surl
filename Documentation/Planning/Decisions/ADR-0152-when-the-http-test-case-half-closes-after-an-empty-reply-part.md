# ADR-0152 — When the HTTP test-case half closes after an empty reply part

- **Status:** Accepted
- **Date:** 2026-11-12
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-12, in BL-1071.
- **Amends:** [ADR-0080](ADR-0080-how-surl-plays-the-server-half-of-upstream-curls-test-cases.md) decision 2 (the HTTP half's connection rule).

## Context

Upstream test 1297 (`CONNECT` to a peer that returns nothing) timed out for 60 s: its origin, the HTTP half,
read `GET ... HTTP/1.1`, found the case's `<data>` part empty and sent nothing - and, since BL-1078, held the
connection open, so curl, waiting through the proxy's tunnel, never saw it close and never exited 52. BL-683 had
closed after an empty part, which broke test 1548 (telnet, empty `<data>`, expects curl to still be waiting at
`-m 1` and exit 28), so BL-1078 reverted it.

Upstream's `sws.c` (tag `curl-8_21_0`, `sws_send_doc`) closes after a zero-byte reply
(`if(strstr(buffer, "swsclose") || !count || req->close)` clears persistence). So the close is right for 1297;
1548's wait is a property of curl's telnet client, whose stdin probe is a raw `HTTP/1.0` request.

## Decision

After an empty reply part the HTTP half sends nothing and closes the connection when the request is HTTP/1.1 or
later, and holds it open when it is HTTP/1.0. A missing part still closes, as before. Measured: 1297 and 1548 both
pass on the Windows pin.

## Consequences

The rule is a discriminator, not upstream's, which closes for both; it stands until a measurement shows why curl's
telnet does not see the close on 1548, at which point the HTTP/1.0 exception can go.
