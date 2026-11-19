# ADR-0179 — `kafka`, `kafkas`, `xmpp` and `xmpps` offer SCRAM when `--auth` is not given

- **Status:** Accepted
- **Date:** 2026-11-19
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-19, in
  BL-1402. No package, no threshold change, no download.
- **Amends:** [ADR-0105](ADR-0105-how-the-kafka-broker-answers-kafka-and-kafkas.md) decision 5,
  [ADR-0114](ADR-0114-how-the-xmpp-server-answers-xmpp-and-xmpps.md) decision 3 and
  [ADR-0125](ADR-0125-sasl-scram-sha-1-and-scram-sha-256-in-surl-authentication.md) decision 9 (and its
  Amendment 1). The three disagreed; this ADR is the one answer.
- **Implemented by:** BL-1403.

## Context

Surly cookbook bug report 2026-11-19, findings 2 and 4: `surl --user alice:s3cret kafka://127.0.0.1:9092/`
answers SaslHandshake with error 33 and an empty list, though `surl --aihelp kafka` says `PLAIN`,
`SCRAM-SHA-256` and `SCRAM-SHA-512` log in; the XMPP server offers only `PLAIN`, before and after STARTTLS.

The recorded decisions disagreed:

- ADR-0105 decision 5: `kafka://` offers SCRAM alone by default, `kafkas://` all three.
- ADR-0114 decision 3: `xmpp` offers `SCRAM-SHA-256`, `SCRAM-SHA-1`, `PLAIN`; its Consequences say `--user
  alice:secret` logs in "with SCRAM or PLAIN".
- ADR-0125 decision 9: SCRAM is not in `AuthenticationMethods.DefaultAccepted`; a server offers it only when
  `--auth` names it, "so no original server's offer, and so no byte upstream curl sees, changes unless `--auth`
  asks for them".

The code follows ADR-0125: `AuthenticationComposition.ComposeSettings` uses `DefaultAccepted` when `--auth` is
not given, and `AuthenticationPolicy` keeps only accepted mechanisms.

## Decision

1. **With an account and no `--auth`, the four extended schemes offer SCRAM.**

   | Scheme | TLS state | Offered (in order) |
   | --- | --- | --- |
   | `kafka` | none | `SCRAM-SHA-256`, `SCRAM-SHA-512` |
   | `kafkas` | TLS | `PLAIN`, `SCRAM-SHA-256`, `SCRAM-SHA-512` |
   | `xmpp` | before STARTTLS | none: `encryption-required` stands (ADR-0114 decision 2) |
   | `xmpp` | after STARTTLS | `SCRAM-SHA-256`, `SCRAM-SHA-1`, `PLAIN` |
   | `xmpps` | TLS | `SCRAM-SHA-256`, `SCRAM-SHA-1`, `PLAIN` |

   `ANONYMOUS` stays under `--allow-anonymous` only; `PLAIN` over a connection with no TLS stays behind
   `--allow-plaintext-auth` (ADR-0032). Order and filtering are those ADR-0105 decision 5 and ADR-0114 decision 3
   already state.

2. **Where the default lives: the composition, per scheme.** `AuthenticationPolicyFor(scheme)`
   (`ComposedProtocolServices`) returns, for `kafka`, `kafkas`, `xmpp` and `xmpps` only, a policy whose accepted
   set is `AuthenticationMethods.DefaultAccepted` plus the SCRAM methods those servers use, and only when `--auth`
   is not given. `AuthenticationMethods.DefaultAccepted` itself is unchanged. When `--auth` is given, its set
   applies to every server as before, so `--auth plain` still means PLAIN alone.

3. **The `CRAM-MD5` precedent, and why it does not need `DefaultAccepted`.** `CRAM-MD5` is in `DefaultAccepted`
   and offered over plain `smtp://`, `imap://` and `pop3://` because it never sends the password; SCRAM
   (RFC 5802) does not either, so ADR-0032's plain-text rule does not withhold it. The same reasoning would
   justify the mail servers offering SCRAM, but that would change an original server's offer, which ADR-0125's
   reason (and ADR-0003) forbids.

4. **ADR-0125's reason is kept, not overturned.** It protects the bytes upstream curl sees. Upstream curl speaks
   neither `kafka` nor `xmpp`, so a default for these schemes changes no byte it sees; the original servers'
   offer is untouched.

5. **Stand-in effect.** A test can log a Kafka or XMPP client in over plain text with only `--user`, the shape
   the other extended brokers' stand-ins use; without this, `kafka://` with an account could not be logged in to
   at all without `--auth` or `--allow-plaintext-auth`. Configuration only grows: a test relying on today's
   default saw an empty mechanism list (Kafka) or a bare `PLAIN` (XMPP) and could not have logged in with SCRAM,
   so the only change it can see is that SCRAM now works; `--auth` still narrows the offer.

6. **What `--aihelp` says (BL-1403 applies these exact sentences).**
   - `surl --aihelp kafka`: "A login is needed unless `--allow-anonymous` is given: SASL `SCRAM-SHA-256` and
     `SCRAM-SHA-512` log in as an account (`--user`, `--user-file`) on `kafka`, and `PLAIN` as well on `kafkas`;
     `PLAIN` over `kafka` needs `--allow-plaintext-auth`, and `--auth` replaces this offer. A refused login
     closes the connection."
   - `surl --aihelp xmpp`: "SASL `SCRAM-SHA-256`, `SCRAM-SHA-1` and `PLAIN` log in as an account (`--user`,
     `--user-file`) once the stream is under TLS (after STARTTLS on `xmpp`, from the first byte on `xmpps`);
     `--auth` replaces this offer, and `ANONYMOUS` needs `--allow-anonymous`."

## Alternatives considered

- **Keep SCRAM behind `--auth` (ADR-0125 as written).** Rejected: its reason does not reach schemes upstream curl
  does not speak, and it leaves `kafka://` with an account unusable, contradicting `--aihelp`, ADR-0105 and
  ADR-0114. The workaround `--auth scram-sha-256,scram-sha-512,plain` stays valid.
- **Add SCRAM to `DefaultAccepted` for every server.** Rejected: it changes the original mail servers' offer,
  against ADR-0125's reason and defaults-keep-today's-behaviour.

## Consequences

- BL-1403 implements decisions 1, 2 and 6 and their tests.
- Amendment notes in ADR-0105, ADR-0114 and ADR-0125 point here.
