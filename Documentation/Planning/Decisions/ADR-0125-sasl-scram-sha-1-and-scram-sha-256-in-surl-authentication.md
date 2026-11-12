# ADR-0125 — SASL `SCRAM-SHA-1` and `SCRAM-SHA-256` in `Surl.Authentication`

- **Status:** Accepted
- **Date:** 2026-11-05
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-04,
  in BL-416, which built it. Recorded by BL-574: `Documentation/Planning/Decisions` was in BL-358's
  `touches` while BL-416 ran, so BL-416 could not write this ADR itself.
- **Amends:** [ADR-0049](ADR-0049-the-mail-servers-sasl-and-apop-logins.md) section 2: the SASL
  offer order gains `SCRAM-SHA-256` and `SCRAM-SHA-1` after `GSSAPI` and before `DIGEST-MD5`
  (decision 9).
- **Details:** [ADR-0084](ADR-0084-the-libraries-the-extended-servers-share-for-http-1-1-websocket-and-dtls.md)
  decision 4, which puts SCRAM in `Surl.Authentication` behind `ISaslAuthenticationPolicy` for the
  extended servers (XMPP first, then Kafka) and leaves its design to BL-416.

## Context

ADR-0084 decision 4 says the extended servers that log in with SCRAM - Xmpp (RFC 6120 section 6)
and Kafka (`SaslAuthenticate`) - reach it through `ISaslAuthenticationPolicy`, with the mechanism
in `Surl.Authentication` so neither server needs anything of its own. BL-416 built it:
`ScramSaslExchange` (one class over `ScramHash.Sha1` and `ScramHash.Sha256`),
`ScramClientFirstMessage`, `ScramClientFinalMessage`, `ScramCredentials`, `SaslPrep` and
`AccountBook.FindScramCredentials`. No contract in `Surl.Protocol.Abstractions` changed.

The specifications are RFC 5802 (SCRAM, and `SCRAM-SHA-1`), RFC 7677 (`SCRAM-SHA-256`) and
RFC 4013 (SASLprep). Upstream curl's SASL client can log in with neither mechanism, so there is no
upstream exchange to measure and no curl byte is pinned here: the RFCs are the judge, and
`ScramSaslMechanismTests` replays RFC 5802 section 5's and RFC 7677 section 3's examples byte for
byte.

## Decision

1. **No `-PLUS` mechanism, so no channel binding.** Only `SCRAM-SHA-1` and `SCRAM-SHA-256` are
   offered. In the `client-first-message`'s GS2 header, `n` (client does not support binding) and
   `y` (client supports it, thinks the server does not) are accepted; `p=` is refused
   `e=channel-binding-not-supported`, and a mandatory extension `m=` is refused
   `e=extensions-not-supported` (RFC 5802 sections 6 and 7). The final message's `c=` must be the
   GS2 header again, base64-encoded, or it is refused `e=channel-bindings-dont-match`.
   *Why:* channel binding needs the TLS `tls-unique` or `tls-exporter` value carried from the
   transport into the policy, which `ISaslAuthenticationPolicy` does not carry today; RFC 5802
   makes `-PLUS` optional, and a client that wants it sees it is not offered. A later task may add
   it with its own ADR.
2. **The server nonce** is the client's nonce followed by base64 of 18 bytes from the policy's
   `ISaslNonceSource` (`ScramSaslExchange.ServerNonceLength`), the source the other SASL
   challenges already use. The exchange's internal constructor takes a nonce factory so the RFCs'
   fixed nonces replay. *Why:* one injected source keeps tests deterministic (root `CLAUDE.md`,
   "Deterministic") and 18 bytes is 144 bits, well past RFC 5802's requirement that the nonce be
   unpredictable, with a base64 length free of padding.
3. **Salt and iterations per account.** When the `AccountBook` is built, each named account gets,
   per `ScramHash`, a random 16-byte salt (`ScramCredentials.SaltLength`) and 4096 iterations
   (`ScramCredentials.DefaultIterationCount`); only `StoredKey` and `ServerKey` are kept, never the
   salted password. *Why:* 4096 is RFC 5802 and RFC 7677's stated minimum that clients accept and
   what common servers use; deriving at start-up means no login pays PBKDF2 more than once per
   account, and keeping only the two keys is what RFC 5802 section 3 means the server to store.
   The salt source is injectable (the internal constructor) so the RFCs' salts replay.
4. **An unknown user is answered like a known one.** A name with no account gets a dummy
   `ScramCredentials`: its salt is HMAC-SHA256 of the mechanism and the name under a key drawn at
   random once per `AccountBook`, so it is the same every time the name is tried; 4096 iterations;
   random keys. The exchange runs to the end and is refused `e=invalid-proof`, exactly as a wrong
   password is. *Why:* RFC 5802 section 9 warns that a random salt per attempt reveals that the
   user does not exist; a consistent salt and the same error leave nothing to tell the two apart,
   as the other mechanisms' dummies do (`AccountBook`).
5. **Server errors are carried as `RefusalNote`.** `SaslLoginStep` has no failure-data field, so a
   refused step carries the `server-final-message` that refuses it, `e=<error>`, as its
   `RefusalNote`, and each server writes it in its own words (ADR-0049 Amendment 3, ADR-0038). The
   errors: `invalid-encoding` for a malformed message, `channel-binding-not-supported`,
   `extensions-not-supported`, `channel-bindings-dont-match`, `other-error` for a final nonce that is
   not the one issued or an `a=` authorization identity naming another user, and `invalid-proof`.
   *Why:* no contract change was needed (ADR-0084 decision 4 says a server's own ADR adds one only
   when the contracts cannot express a step), and XMPP's `<failure>` and Kafka's error message can
   both carry the note.
6. **Success data as a challenge or as `AdditionalSuccessData`.** `v=<ServerSignature>` is sent as
   one more challenge, answered empty by the client, on servers whose protocol has no
   success-with-data (the mail servers, as `DIGEST-MD5`'s `rspauth` is), or as
   `AdditionalSuccessData` where the server can carry it (`CanCarrySecurityLayer`; XMPP's
   `<success>` and Kafka's `SaslAuthenticate` response). *Why:* both are RFC 4422 section 3.6's
   forms; the server picks the one its protocol has, so the client always gets the signature that
   proves the server knew the password.
7. **`--allow-anonymous` runs every step and accepts a well-formed final message unchecked**, as
   the other SASL mechanisms do under it (ADR-0032). *Why:* a test stand-in can then take any
   SCRAM client without configuring its password, while the message syntax is still checked.
8. **SASLprep is hand-built, without the prohibition and bidi checks.** `SaslPrep` applies
   RFC 4013's mapping (C.1.2 non-ASCII spaces to U+0020, B.1 characters removed) and then NFKC
   through `string.Normalize`, base class library only. The prohibited-output (section 2.3) and
   bidirectional (section 2.4) checks are left out. Account names match exactly, unnormalized.
   *Why:* those checks only refuse; a conforming client never sends a password they would refuse,
   so leaving them out changes no successful login and saves the stringprep tables. Exact name
   matching is how every other mechanism in `AccountBook` matches.
9. **Not in the default `--auth` set; offered after `GSSAPI`.** `AuthenticationMethod` gains
   `ScramSha256` and `ScramSha1`, and ADR-0049 section 2's order becomes `GSSAPI`,
   `SCRAM-SHA-256`, `SCRAM-SHA-1`, `DIGEST-MD5`, `CRAM-MD5`, `NTLM`, `OAUTHBEARER`, `XOAUTH2`,
   `PLAIN`, `LOGIN`, `EXTERNAL`. Neither is in `AuthenticationMethods.DefaultAccepted`; BL-575
   added the `--auth` words `scram-sha-256` and `scram-sha-1`, which turn them on for the mail
   servers' offer. *Why:* strongest first,
   as ADR-0049 section 3 orders methods computed from a secret, and SCRAM is stronger than
   `DIGEST-MD5` and `CRAM-MD5`; keeping them out of the default set means no original server's
   offer, and so no byte upstream curl sees, changes unless `--auth` asks for them (secure by
   default, and today's behaviour kept).

## Alternatives considered

- **Offer `SCRAM-SHA-256-PLUS` now.** Rejected for decision 1's reason: it needs a contract change
  carrying the TLS binding into the policy, and no client of the planned extended servers requires
  it.
- **Per-attempt random salt for an unknown user.** Rejected: RFC 5802 section 9's user
  enumeration.
- **A full stringprep (RFC 3454) implementation.** Rejected for decision 8's reason; it can be
  added without changing any accepted login.
- **A failure-data field on `SaslLoginStep`.** Rejected: `RefusalNote` already carries the text,
  and changing the contract would touch every SASL server.

## Consequences

- The Xmpp and Kafka servers (ADR-0084) log in with SCRAM through `ISaslAuthenticationPolicy` and
  need nothing of their own for it.
- **BL-575** (done) added the `--auth` words `scram-sha-256` and `scram-sha-1`, listed after
  `digest` and before `digest-md5` in `AuthenticationMethod`'s declaration order (ADR-0032
  section 3); `surl --auth scram-sha-256,scram-sha-1` makes SMTP, IMAP and POP3 offer them.
- `Foundation/Surl.Authentication.UnitLibrary/CLAUDE.md` ("SASL `SCRAM-SHA-1` and
  `SCRAM-SHA-256`") is the library's own summary of this ADR.

## Amendment 1 — `SCRAM-SHA-512` and its `--auth` word (BL-915, 2026-11-11)

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions").

- **BL-700** (done) added `SCRAM-SHA-512` - RFC 5802 over SHA-512 (`ScramHash.Sha512`), the IANA
  SASL registry's name, without channel binding - to `Surl.Authentication` as
  `AuthenticationMethod.ScramSha512`, for the Kafka broker (ADR-0105 decision 5). Everything
  decisions 2 to 8 say of the two SCRAM mechanisms holds for it unchanged; only the hash differs.
- **Offer order.** `SCRAM-SHA-512` is offered after `SCRAM-SHA-256` and before `SCRAM-SHA-1`
  (`AuthenticationMethod`'s declaration order), so decision 9's SASL offer reads `GSSAPI`,
  `SCRAM-SHA-256`, `SCRAM-SHA-512`, `SCRAM-SHA-1`, `DIGEST-MD5`, ... Upstream curl's own SASL
  client picks among the mechanisms it supports and does not speak `SCRAM-SHA-512`, so the
  position changes nothing curl chooses; keeping `SCRAM-SHA-256` first keeps the mechanism every
  SCRAM client implements (RFC 7677) at the head, and the stronger hash before the weaker one.
- **The word.** BL-915 adds the `--auth` word `scram-sha-512`, the mechanism's name in lower case
  as for every other SASL word (ADR-0049 section 3), listed between `scram-sha-256` and
  `scram-sha-1` in `--help`, `--manual`, `--aihelp` and `OptionArgumentReader`'s allowed words.
  It is not in the default set: like the other SCRAM words, a server offers it only when
  `--auth` names it, so no byte an original server sends changes without it.
