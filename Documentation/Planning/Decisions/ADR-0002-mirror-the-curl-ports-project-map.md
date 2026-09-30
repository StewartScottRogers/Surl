# ADR-0002 — Mirror the Curl port's project map, with `Surl.Content` in place of a file protocol library

- **Status:** Accepted
- **Date:** 2026-09-28
- **Decided by:** Stewart, approving the proposed shell on 2026-09-28
- **Amended:** decision 3's table by [ADR-0048](ADR-0048-the-hand-built-ssh-primitive-libraries.md) (2026-09-29), which adds the four hand-built SSH primitive libraries: `Surl.Cryptography.ChaCha20.UnitLibrary`, `Surl.Cryptography.Curve25519.UnitLibrary` and `Surl.Cryptography.Poly1305.UnitLibrary`, each referencing nothing, and `Surl.Cryptography.Ed25519.UnitLibrary`, referencing `Surl.Cryptography.Curve25519.UnitLibrary`.
- **Amended:** decision 3's table by [ADR-0050](ADR-0050-the-mail-store-and-the-line-machinery-the-mail-servers-share.md) (2026-09-29), which adds the two libraries the mail servers share: `Surl.LineProtocol.UnitLibrary`, referencing `Surl.Protocol.Abstractions.UnitLibrary`, and `Surl.MailStore.UnitLibrary`, referencing `Surl.Protocol.Abstractions.UnitLibrary` and `Surl.Content.UnitLibrary`.
- **Amended:** decision 3's table by [ADR-0057](ADR-0057-surls-kerberos-keytab-and-ap-req-check-for-negotiate-and-sasl-gssapi.md) (2026-09-30), which adds the hand-built Kerberos library, `Surl.Kerberos.UnitLibrary`, referencing nothing. `Surl.Authentication.UnitLibrary` will reference it (BL-240); no protocol server needs it, since every login goes through the policy contracts.

## Context

Stewart asked for a shell for Surl that matches the Curl port "using the same mindset as
a sibling", to be completed later. The Curl port organises curl into 26 production
projects, each followed by its `.UnitTests` twin, created together on its first day: one
library per protocol family, horizontal libraries for authentication, cookies,
networking, output and cryptography, a command-line library, a serving core, the
contracts, a library for upstream curl's own test cases, and the executable.

Three questions followed:

1. **Which projects?** Mirroring the map makes the pairing legible - each Surl server
   answers what the matching client library asks - and lets the port's conventions,
   agents and task board carry over unchanged.
2. **What of `file://`?** curl reads `file://` URLs from its own disk. There is no wire,
   so there is no server to write, and a library named `Surl.Protocol.File` would claim
   a protocol server that cannot exist. Yet serving files is the heart of half of Surl's
   protocols - HTTP, FTP, SFTP, SCP, TFTP, Gopher and SMB all publish a directory.
3. **All at once, or phase by phase?** Empty projects before anything works are a cost,
   but the dark factory plans and schedules by project name (a task's `touches`), and
   the reference-graph test can only guard projects that exist.

## Decision

1. Every Curl port project has a Surl twin of the same shape: `Surl.<Area>.UnitLibrary`,
   `Surl.Protocol.<Name>.UnitLibrary` for the fifteen protocol servers, and `Surl.Console`
   (assembly `surl`), each followed by its `.UnitTests` twin - 26 production projects
   and 26 test projects, all created in the Phase 0 shell.
2. `Surl.Content.UnitLibrary` takes the place of a file protocol library. It is the
   content store: it maps a request path onto the served directory without escaping it,
   lists directories, names media types, reads byte ranges, reports sizes and
   modification times, and decides where uploads land - through an injected file-system
   seam, so its tests need no disk.
3. A protocol server may reference `Surl.Protocol.Abstractions.UnitLibrary` and the
   horizontal libraries in this table, and nothing else:

   | Horizontal library | May itself reference |
   | --- | --- |
   | `Surl.Content.UnitLibrary` | `Surl.Protocol.Abstractions.UnitLibrary` |
   | `Surl.Cryptography.ChaCha20.UnitLibrary` (ADR-0048) | nothing |
   | `Surl.Cryptography.Curve25519.UnitLibrary` (ADR-0048) | nothing |
   | `Surl.Cryptography.Ed25519.UnitLibrary` (ADR-0048) | `Surl.Cryptography.Curve25519.UnitLibrary` |
   | `Surl.Cryptography.Poly1305.UnitLibrary` (ADR-0048) | nothing |
   | `Surl.Cryptography.UnitLibrary` | nothing |
   | `Surl.Kerberos.UnitLibrary` (ADR-0057) | nothing |
   | `Surl.LineProtocol.UnitLibrary` (ADR-0050) | `Surl.Protocol.Abstractions.UnitLibrary` |
   | `Surl.MailStore.UnitLibrary` (ADR-0050) | `Surl.Protocol.Abstractions.UnitLibrary`, `Surl.Content.UnitLibrary` |

   `Surl.Protocol.Abstractions.UnitTests` enforces the table and that Abstractions
   references nothing. The rows marked ADR-0048 name projects BL-149 created, and the rows
   marked ADR-0050 projects BL-189 creates; the test learns each row once its project exists. A further hand-built library (an NTLM, Kerberos, TLS, HTTP/2, QUIC
   or HTTP/3 library, say) joins the table through a new ADR when Surl needs one.

## Consequences

Good:

- The pairing reads at a glance: `Surl.Protocol.Ftp` answers what curl's FTP support
  asks.
- The Curl port's agents, commands, rules and task board carry over with only the names
  changed, and a task can name any project in `touches` from day one.
- The architecture test is live from the first commit.

Costs and caveats:

- Twenty-four of the twenty-six production projects hold no code yet, and
  `Measure-CodeQuality.ps1` reports only the assemblies that have members. Accepted:
  that is the Phase 0 shell by design.
- One protocol server per family means the secure variants (`https`, `ftps`, `imaps` and
  the rest) are the same server over a secured connection, never a second library.

## Alternatives considered

- **Create projects phase by phase.** Rejected: it breaks the match Stewart asked for,
  and the factory's lanes plan by project name.
- **Keep `Surl.Protocol.File`.** Rejected: it would name a protocol server that cannot
  exist - the opposite of "say what it does".
- **Put the content store in `Surl.Core`.** Rejected: protocol servers may not reference
  `Surl.Core`, and every file-serving server needs the store.
