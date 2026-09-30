# ADR-0068 — Upstream curl does not trust surl's host certificate through `@cert-authority`; the refusal is pinned

- **Status:** Accepted
- **Date:** 2026-09-30
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-30,
  in BL-279 (FR-039).
- **Relates to:** [ADR-0051](ADR-0051-the-ssh-transport-host-keys-and-user-authentication.md)
  Amendment 2 (how surl serves `--hostcert`), whose decisions stand unchanged.

## Context

BL-222 built `--hostcert` (ADR-0051 decision 4 and Amendment 2) and proved it only against the
test-side client in `Surl.Protocol.Ssh.UnitTests`. BL-279 asked whether each pinned upstream curl
build completes an `sftp://` download from `surl --hostkey <key> --hostcert <key>-cert.pub`,
trusting the certificate's CA through a `@cert-authority` line in its `--knownhosts` file and given
neither `-k` nor `--hostpubsha256`, and, if it does not, to pin what it does.

## Measurements

Measured on 2026-09-30 with `UpstreamCurlTrustsSurlsHostCertificateTests` (which runs a live,
in-process surl and the pinned build, as every SSH conformance test does), with `-sS -v`, the
`host-certificates` fixtures of `Surl.Protocol.Ssh.UnitTests` (CA `ca_ed25519`, principal
`localhost`, valid 2026-01-01 to 2036-01-01), and one `known_hosts` line
`@cert-authority <pattern> ssh-ed25519 <CA key>`:

| Build | Key | URL host | `<pattern>` | Agreed host key | Exit | curl's `-v` lines |
| --- | --- | --- | --- | --- | --- | --- |
| win-x64 reference (WinCNG) | RSA | `127.0.0.1` | `[127.0.0.1]:<port>`, `*` | `rsa-sha2-512` | 60 | `SSH: did not find host '127.0.0.1' in '<file>'`, `SSH: host check 2, key: <none>`, `SSH: knownhost check failed` |
| win-x64 reference (WinCNG) | RSA | `localhost` | `[localhost]:<port>` | `rsa-sha2-512` | 60 | the same, for `localhost` |
| win-x64 reference (WinCNG) | Ed25519 | `127.0.0.1` | `[127.0.0.1]:<port>`, `*` | none | 2 | `Failure establishing ssh session: -5, Unable to exchange encryption keys` (WinCNG lists no Ed25519 name, ADR-0051 Context) |
| win-x64 supplementary static-curl (OpenSSL 4.0.1) | RSA | `127.0.0.1`, `localhost` | as above | `rsa-sha2-512` | 60 | as the first row |
| win-x64 supplementary static-curl (OpenSSL 4.0.1) | Ed25519 | `127.0.0.1` | `[127.0.0.1]:<port>`, `*` | `ssh-ed25519` | 60 | as the first row |

Every exit 60 printed `curl: (60) SSL peer certificate or SSH remote key was not OK` as the first
line of stderr and nothing on stdout. curl never logged `Failed to read known hosts`, so libssh2
read the file without error; it simply holds no entry curl can match.

Two causes, each enough on its own:

1. **libssh2 agrees the plain key.** RFC 4253 section 7.1 has the client's first name the server
   also offers win. libssh2 1.11.1 lists each plain name before its certificate name (ADR-0051,
   "The decoded name-lists": `rsa-sha2-512, rsa-sha2-256, rsa-sha2-512-cert-v01@openssh.com, ...`
   and `ssh-ed25519, ssh-ed25519-cert-v01@openssh.com`), and surl offers both (Amendment 2
   decision 1), so the plain key is agreed and the certificate is never sent.
2. **libssh2 does not read `@cert-authority`.** curl looks up the host in `known_hosts` and finds
   nothing (`did not find host`, then `host check 2` - `LIBSSH2_KNOWNHOST_CHECK_NOTFOUND`),
   whatever the host pattern, so it refuses the key.

The Linux and macOS reference pins are the same curl 8.21.0, libssh2 1.11.1 and OpenSSL 4.0.1 as
the supplementary static-curl Windows pin (ADR-0063 measured their SSH `KEXINIT`s identical); they
were not run on this machine, so their test pins the answer the Windows OpenSSL pin gave, and CI's
Linux and macOS jobs measure it.

## Decision

1. **Pin the refusal.** `UpstreamCurlTrustsSurlsHostCertificateTests` pins exit 60, empty stdout,
   stderr's first line `curl: (60) SSL peer certificate or SSH remote key was not OK`, and surl's
   `SSH negotiated` note naming the plain host key: `rsa-sha2-512` for the RSA key on the Windows
   reference pin, `ssh-ed25519` for the Ed25519 key on the supplementary OpenSSL Windows pin and on
   the Linux and macOS reference pins. The Ed25519 run on the Windows reference pin is not repeated
   here; `UpstreamCurlLogsInToSurlOverSshTests` already pins its exit 2.
2. **surl does not change.** It keeps offering each certificate name just before its plain name.
   Why: that is the order OpenSSH's sshd uses and what a certificate-aware client (OpenSSH's `ssh`
   and `sftp`) needs; offering only the certificate names would not help upstream curl either,
   because libssh2 would still find no `known_hosts` entry for the certificate, and it would make
   every curl without a CA line fail where it now succeeds with `--hostpubsha256`.
3. **What an operator does.** A host certificate serves certificate-aware SSH clients; upstream
   curl 8.21.0 checks surl's plain host key whether or not `--hostcert` is given. The same test
   class pins that: with `--hostcert` given and the RSA key's `--hostpubsha256`, the pinned build
   downloads the file (exit 0), on every platform.

## Consequences

- FR-039's `--hostcert` is proved to leave upstream curl's plain-key checks working and to be
  unusable as upstream curl's only trust anchor; a later libssh2 in a newly pinned build that
  reads `@cert-authority` would turn these tests red, which is the signal to revisit this ADR.
- The test holds copies of the fixtures as `SshTestHostCertificates`, since conformance tests do
  not reference `Surl.Protocol.Ssh.UnitTests`.
