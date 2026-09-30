# ADR-0065 — Kerberos logins are proved against pinned upstream curl through a hand-built loopback KDC

- **Status:** Accepted
- **Date:** 2026-09-30
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-30,
  in BL-242 (FR-046).
- **Follows:** [ADR-0057](ADR-0057-surls-kerberos-keytab-and-ap-req-check-for-negotiate-and-sasl-gssapi.md)
  decision 11, which left open how `--negotiate` and SASL `GSSAPI` from a pinned upstream curl
  build are proved end to end against `surl --keytab`. It amends nothing: ADR-0057 decisions 8
  and 9 stand until decision 6 below measures them.

## Context

ADR-0057 built Kerberos by hand and tests it in CI with RFC vectors and hand-made tickets, with no
KDC. What CI cannot show is a real client's token: measured on the lane machine, the Windows
reference build (`C:\Program Files\Git\mingw64\bin\curl.exe`, SHA-256
`0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`, curl 8.21.0, features
`Kerberos`, `SPNEGO`, `SSPI`) sends no Kerberos token without a KDC (SASL `GSSAPI` exits 94,
`--negotiate` fails `SEC_E_NO_CREDENTIALS`), and the Linux and macOS reference builds have no
Kerberos, SPNEGO or GSS-API feature at all (`UpstreamCurlBuilds.json`). ADR-0057 decisions 8 and 9
(SPNEGO `mechTypes` leading with `1.2.840.48018.1.2.2`, whether `mutual-required` is set, whether
a `mechListMIC` is sent; SASL `GSSAPI`'s bare `InitialContextToken` without mutual
authentication, and its security-layer answer) were therefore decided from the RFCs, MS-SPNG and
curl 8.21.0's source, not from measured bytes.

A pinned client's token needs three things this repository does not have: a KDC that issues it a
ticket, a machine whose Kerberos client knows where that KDC is, and a service key surl can read
as a keytab.

### The lane machine, checked during this task

On 2026-09-30 the lane machine (Windows 11 Pro 10.0.26200) was checked for a KDC:
`Win32_ComputerSystem.PartOfDomain` is `False`; `ksetup` answers "Machine is not configured to
log on to an external KDC. Probably a workgroup member"; there is no key under
`HKLM:\SYSTEM\CurrentControlSet\Control\Lsa\Kerberos\Domains`; and the lane does not run as an
administrator. **No KDC was available, so nothing was measured in this task**; BL-268 measures
(decision 6).

## Decision

### 1. The KDC is hand-built: `Surl.Kerberos.TestKdc.UnitLibrary`, a loopback test fixture

The KDC is built by hand with the BCL and `Surl.Kerberos` (ADR-0057 decision 6), in its own
library `Surl.Kerberos.TestKdc.UnitLibrary` with its own `Surl.Kerberos.TestKdc.UnitTests`, held
to the same quality gates as every `*.UnitLibrary`. No MIT or Heimdal install, no Samba, no
package, no existing KDC of Stewart's: each would be a download or a machine outside the
repository, and the root `CLAUDE.md` has Kerberos built by hand.

- **It is a test fixture, not a surl server.** curl never speaks to a KDC itself; its SSPI or
  GSS-API library does. So the KDC is no protocol server, is not registered in `Surl.Console`'s
  `ComposeProtocolServers`, has no command-line option and no `--aihelp` topic, and only test
  projects reference it. `Surl.Console` does not reference it, so it is not in the native-AOT
  binary (it is still AOT-compatible, as `Directory.Build.props` makes every production project).
- **What it serves**: one realm, `SURL.TEST`; one user principal with a password the test gives
  (`tester@SURL.TEST`); the service principals the test names (`HTTP/web.surl.test`,
  `smtp/mail.surl.test`, `imap/mail.surl.test`, `pop/mail.surl.test`); the AS exchange with
  `PA-ENC-TIMESTAMP` pre-authentication (an AS-REQ without it is answered
  `KDC_ERR_PREAUTH_REQUIRED` with `PA-ETYPE-INFO2` naming the salt, as a Windows client expects)
  and the TGS exchange (RFC 4120 sections 3.1 and 3.3). User keys come from the password by
  RFC 3962 string-to-key with the default salt; service and ticket-granting keys are random from
  an injected source.
- **Enctypes**: `aes256-cts-hmac-sha1-96` (18) and `aes128-cts-hmac-sha1-96` (17), chosen in the
  client's order; the RFC 8009 enctypes (19, 20) too, which ADR-0057 accepts, when the client
  lists them. No `rc4-hmac`: ADR-0057 decision 3 refuses it, so a ticket in it proves nothing.
- **Transport**: `127.0.0.1` port 88 over both UDP and TCP (RFC 4120 section 7.2), since a
  Windows client tries UDP first and retries over TCP on `KRB_ERR_RESPONSE_TOO_BIG`. Binding
  port 88 needs no administrator on Windows. It is transport-injected like every Surl piece, so
  its tests stay off the network; the loopback sockets are made by `Surl.Networking`'s factories
  the Conformance tests already use, not by the fixture itself.
- **The keytab**: it writes the service principals' keys as an MIT keytab (the format ADR-0057
  decision 1 reads), which the test hands to `surl --keytab`.
- **Its bounds**: a request larger than 64 KiB, an unknown principal, an unsupported enctype or
  a failed pre-authentication is answered with the RFC 4120 error for it and never crashes it;
  it is a fixture, but it is still exposed on loopback while a test runs.

Built by **BL-266**.

### 2. The pinned Windows client is pointed at it by a one-time machine change Stewart makes

On a machine outside a domain, Windows' Kerberos package finds a realm's KDC only through the
realm mapping `ksetup` writes under `HKLM:\SYSTEM\CurrentControlSet\Control\Lsa\Kerberos`, which
needs administrator rights and changes the machine. A lane must not do that and a CI runner
cannot, so it is **Stewart's one-time change, filed as BL-265** (assignee Stewart), run from an
elevated prompt on the lane machine:

```
ksetup /addkdc SURL.TEST 127.0.0.1
ksetup /addhosttorealmmap .surl.test SURL.TEST
```

and undone with `ksetup /delhosttorealmmap .surl.test SURL.TEST` and `ksetup /delkdc SURL.TEST
127.0.0.1`. The change is harmless while no test runs: it names a realm nobody else uses, mapped
to hosts under `.surl.test` (RFC 6761 reserves `.test`), with its KDC on loopback.

- **No hosts-file change.** Every test names its service host in the URL and pins its address
  with curl's `--resolve web.surl.test:<port>:127.0.0.1`, so curl builds the SPN
  `HTTP/web.surl.test` (and `smtp/mail.surl.test` and the rest) from the URL while connecting to
  loopback.
- **Credentials are explicit**: `--negotiate -u tester@SURL.TEST:<password>` and, for SASL,
  `-u tester@SURL.TEST:<password>`, so SSPI makes its AS exchange with the test's password and
  no logged-on user's ticket is involved.

### 3. The proof is `TestCategory=Integration` only, Windows only, and Inconclusive until BL-265

The end-to-end tests live in `Surl.Conformance.UnitTests` beside the other
`UpstreamCurlLogsInToSurl*` tests, marked `[TestCategory("Integration")]` and
`[OSCondition(OperatingSystems.Windows)]`: never in the fast set, never on Linux or macOS, whose
pinned builds have no Kerberos. Each test is `Inconclusive`, not failed, when the machine has no
`SURL.TEST` realm mapping (the `Domains\SURL.TEST` registry key, readable without administrator
rights), when port 88 is taken, or when the pinned build is not installed. So CI stays green, and
the proof runs wherever BL-265 has been done.

### 4. `Record-CurlExchange.ps1` gains `-KerberosTestKdc`

Measuring the pinned build's tokens needs the KDC running while curl runs, and the service key to
read what it sent. `Record-CurlExchange.ps1` gains one switch, `-KerberosTestKdc`, built by
**BL-267**:

- It starts the test KDC through a C# file-based app at the repository root
  (`dotnet run Run-KerberosTestKdc.cs`, referencing `Surl.Kerberos.TestKdc.UnitLibrary`), with the
  user's password from `-KerberosPassword` and the service principals from
  `-KerberosServicePrincipal` (repeatable), waits for it to say it is listening, runs curl, then
  stops it.
- It writes two more files to `OutDirectory`: `service.keytab`, the service principals' keys, and
  `kdc.log`, one line per AS and TGS exchange (principal, enctype, error code). With the keytab,
  every recorded AP-REQ can be decrypted to its authenticator and read.
- It combines with the HTTP mode (a canned `401` with `WWW-Authenticate: Negotiate`), with
  `-Smtp`, `-Imap` and `-Pop3` with `-SaslChallenge` (the initial token), and with `-NoServer`
  against a started `surl --keytab` (the whole exchange, security layer included).
- Its refusal to run a binary not pinned in `UpstreamCurlBuilds.json` is unchanged, and it
  refuses `-KerberosTestKdc` outside Windows, where no pinned build has Kerberos.

### 5. Who builds and runs what

| Work | Task |
| --- | --- |
| Map `SURL.TEST` to `127.0.0.1` on the lane machine with `ksetup` (decision 2) | BL-265 (Stewart) |
| `Surl.Kerberos.TestKdc` and its tests: AS and TGS exchanges, pre-authentication, UDP and TCP, the keytab writer (decision 1) | BL-266 |
| `Run-KerberosTestKdc.cs` and `Record-CurlExchange.ps1 -KerberosTestKdc` (decision 4) | BL-267 |
| Measure the pinned Windows build's SPNEGO and SASL `GSSAPI` tokens and check ADR-0057 decisions 8 and 9 against them (decision 6) | BL-268 |
| The Integration tests: pinned curl logs in to `surl --keytab` with `--negotiate` over HTTP and with `GSSAPI` over SMTP, IMAP and POP3 (decision 3) | BL-269 |

### 6. What BL-268 measures, and what happens if it differs

Once BL-265, BL-266 and BL-267 are done, BL-268 records, with the build's path, SHA-256 and
version (8.21.0), for the Windows reference build (and for the unpatched static-curl 8.21.0
Windows build for HTTP Negotiate only, as ADR-0042 allows):

- **HTTP Negotiate**: the SPNEGO `NegTokenInit`'s `mechTypes` in order, whether the optimistic
  token is a Kerberos AP-REQ, whether its authenticator's checksum sets `mutual-required`
  (`GSS_C_MUTUAL_FLAG`), and whether a `mechListMIC` is sent; then what curl does with surl's
  `negTokenResp`.
- **SASL `GSSAPI`**, over SMTP, IMAP and POP3, with and without `--sasl-ir`: the initial token's
  form (a bare `InitialContextToken` or SPNEGO), its `mutual-required` flag, and the unwrapped
  security-layer answer (layer byte, maximum size, authorization identity), with and without
  `--sasl-authzid`.

It states for each whether ADR-0057 decision 8 or 9 holds. Any difference is recorded as an
amendment of ADR-0057 (or a new ADR amending it) and filed as a task that changes surl, before
BL-269 pins the behaviour in a test.

## Alternatives considered

- **An MIT Kerberos, Heimdal or Samba KDC.** Rejected: a download (Stewart's to approve), a
  platform dependency, and against the root `CLAUDE.md`'s rule that Kerberos is built by hand.
- **A KDC Stewart already runs, or joining a domain.** Rejected: there is none on the lane
  machine, joining a domain changes far more than `ksetup` does, and a proof that needs a
  particular network cannot be rerun by a lane.
- **Pointing SSPI at the KDC without `ksetup`**, through DNS `SRV` records or a hosts file.
  Rejected: a workgroup machine's Kerberos package does not find an MIT-style realm's KDC from
  DNS alone, and a hosts-file edit needs administrator rights too; `ksetup` is the documented
  way and is undone as simply.
- **A `surl kdc` mode.** Rejected: upstream curl never makes a KDC request itself, so a KDC is
  not part of being curl's server-side mate; it would add an option and an `--aihelp` topic for
  a test's sake.
- **Proving on Linux or macOS with a hand-written `krb5.conf`.** Not possible: the pinned Linux
  and macOS builds have no GSS-API; pinning one that has needs a download (Stewart's).
- **Running the proof in CI.** Rejected: a runner cannot change its Kerberos configuration, and
  a hosted runner's port 88 and realm mapping are not ours.

## Consequences

- Nothing in surl changes; ADR-0057 decisions 8 and 9 stay as decided from the RFCs and curl's
  source until BL-268 measures them.
- Until Stewart does BL-265, BL-268 and BL-269 wait, and the Kerberos Integration tests, once
  written, are `Inconclusive` everywhere.
- `Surl.Kerberos.TestKdc.UnitLibrary` and its tests join the solution as a test fixture library,
  referenced only by test projects.
- The lane machine keeps a `SURL.TEST` realm mapping after BL-265; `ksetup /delkdc` and
  `/delhosttorealmmap` remove it.
