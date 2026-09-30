# ADR-0066 — `--ssh-ciphers` and `--ssh-macs` narrow the SSH server's offer

- **Status:** Accepted
- **Date:** 2026-09-30
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-30,
  in BL-262 (FR-039).
- **Amends:** [ADR-0051](ADR-0051-the-ssh-transport-host-keys-and-user-authentication.md) decisions 2 and 12
  (the offer and the `ssh` help category) and [ADR-0007](ADR-0007-the-phase-1-command-line-surface.md)
  section 2's option table.

## Context

Upstream curl has no option to choose its SSH cipher or MAC: libssh2 takes the first name in its
own list that the server also offers (RFC 4253 section 7.1). With ADR-0051 decision 2's offer, even
under `--allow-weak-ssh-algorithms`, libssh2 always agrees a strong cipher and MAC, so a test that
must prove a weak one - BL-261's `blowfish-cbc`, `cast128-cbc`, `hmac-ripemd160` and
`hmac-ripemd160@openssh.com` against pinned upstream curl - needs the server to offer that one
alone. Rewriting surl's `SSH_MSG_KEXINIT` on the wire is not an option: both `KEXINIT` payloads are
hashed into the exchange hash. OpenSSH's sshd has the same knobs as `Ciphers` and `MACs`.

No upstream curl byte changes with this decision: it narrows only what surl offers, and the
negotiation rule it relies on is RFC 4253's and libssh2's, already pinned by ADR-0051.

## Decision

1. **Options.** `--ssh-ciphers <names>` and `--ssh-macs <names>`, after sshd's `Ciphers` and
   `MACs`. Both are in the `ssh` help category only, not negatable, not in the short list, and not
   loosening options: they can only take algorithms away from the offer, never add one.
2. **Syntax.** A comma-separated list of algorithm names, exact case as SSH names are
   (RFC 4251 section 6), no empty item (`option --ssh-ciphers: is badly used here`), an empty
   argument blank (`blank argument where content is expected`), each refused by `Surl.Cli` with
   exit 2 as every badly used option is. The last `--ssh-ciphers` (and `--ssh-macs`) given wins.
   sshd's `+`, `-` and `^` prefixes are not taken: a test names exactly what it wants.
3. **Order.** The names are offered in the order given, the list's first name surl's first
   preference, as sshd does. A name given twice is offered once, at its first place. Both
   directions (client to server and server to client) get the same list, as ADR-0051 decision 2's
   offer does.
4. **Refusals.** `Surl.Console` checks each name before anything else is read (after
   `--auth gssapi` without `--keytab` and the options not yet available), ciphers first, then MACs,
   and refuses the first it cannot offer with exit 2 (`SurlExitCode.FailedInit`, as every
   inconsistent start):
   - a name not in ADR-0051 decision 2's offer, weak algorithms included, on this machine
     (`aes256-gcm@openssh.com` and `aes128-gcm@openssh.com` count only where AES-GCM is
     supported): `surl: (2) --ssh-ciphers: surl does not offer the SSH cipher <name>`
     (`--ssh-macs: surl does not offer the SSH MAC <name>`);
   - a weak name (ADR-0051 decision 2, ADR-0061) without `--allow-weak-ssh-algorithms`:
     `surl: (2) --ssh-ciphers: <name> needs --allow-weak-ssh-algorithms` (and `--ssh-macs`
     likewise), worded after the `--hostkey` refusal of a short RSA key.

   Refusing rather than silently leaving a name out keeps a test from believing it is proving an
   algorithm surl never offered. `--allow-weak-ssh-algorithms` still writes its warning whenever
   it is given, narrowed or not.
5. **Where.** `SshAlgorithmOffer.Narrowed(ciphers, macs)` in `Surl.Protocol.Ssh` narrows an offer;
   `Surl.Console`'s `SshAlgorithmComposition` checks the names (`FindRefusal`) and composes the
   narrowed offer (`Compose`). `Surl.Cli` reads the lists into `SurlCommandLine.SshCiphers` and
   `SshMacs` without knowing the names, so the names live in one place, `SshAlgorithmOffer`.
6. **Help.** `surl --aihelp ssh` lists every name each option takes, and a `Surl.Console` test
   fails when an offered name is missing from it; the manual's SSH section describes both options.

## Consequences

- BL-261 can start surl with, for example, `--allow-weak-ssh-algorithms --ssh-ciphers blowfish-cbc
  --ssh-macs hmac-ripemd160` so that pinned upstream curl can agree only those.
- A narrowed list that the client shares no name with ends the key exchange as ADR-0051 already
  answers a failed negotiation; surl does not check overlap with any client at start.
