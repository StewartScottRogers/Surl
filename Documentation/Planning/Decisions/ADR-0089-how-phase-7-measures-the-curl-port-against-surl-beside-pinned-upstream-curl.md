# ADR-0089 — How Phase 7 measures the Curl port against Surl beside pinned upstream curl

- **Status:** Accepted
- **Date:** 2026-11-04
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-04,
  in BL-576. Built by BL-577 (the port pin), BL-578 (one case, compared and judged), BL-579 (client
  bytes), BL-580 to BL-590 (one family each), BL-591 (upstream test cases), BL-597 (the libcurl
  rows, filed by BL-576), BL-592 (summary and handoff) and BL-593 (the product documents).
- **Refines:** [ADR-0003](ADR-0003-upstream-curl-is-surls-only-oracle.md) decision 6, which this
  ADR puts into practice without changing it, and
  [ADR-0083](ADR-0083-how-surl-serves-the-curl-ports-extended-protocols-and-how-they-are-tested.md)
  decision 10's sentence about Phase 7 (decision 9 below). ADR-0003's oracle rule and ADR-0083's
  rules for the extended servers stand as written.

## Context

Phase 7 is the product overview's last phasing row and success criterion 5: "Then, and only then,
it measures the Curl port". ADR-0003 decision 6 says what it means: the port runs the same
conversations against Surl beside pinned upstream curl; wherever the two disagree, upstream is
right, the disagreement is the port's defect and is filed on the port's own board, and Surl never
changes to make the port pass.

What exists to build on, on 2026-11-04:

- The corpus: the `UpstreamCurl…SurlOver…Tests` classes in `Conformance/Surl.Conformance.UnitTests`
  run pinned upstream curl against `SurlOnLoopback`, the build chosen per protocol by
  `PinnedUpstreamCurl` (the Windows reference build; the supplementary static 8.21.0 build for HTTP/2
  and HTTP/3; the 8.22.0 build for SMB; the OpenLDAP build on Linux). The pinned-libcurl classes
  `PinnedLibcurlTalksToSurlOverWebSocketTests` and `PinnedLibcurlTalksToSurlOverRtspTests` drive the
  upstream `libcurl-4.dll` through `PinnedLibcurlDriver`. Phase 6 adds HTTP/2 and HTTP/3 classes
  (BL-371, BL-385) and the upstream test-case runner (BL-392 to BL-397, ADR-0080).
- The port's client: `CurlPortBuilds.json` pins its `curl` by path, SHA-256 and commit (ADR-0083
  decision 2); `CurlPortClientLocator` and `CurlPortClientRunner` in `Surl.Conformance.UnitLibrary`
  run it. Only `win-x64` is pinned. The port's own `CURL_EXTENSIONS=0` turns its extended schemes off
  (port `CLAUDE.md`).
- `surl` dumps nothing it receives; its `-v` notes land in `SurlOnLoopback.Log`.
  `ClientKexInitRecorder` and `DigestChallengeRelay` show the shape of a loopback relay.
- The port's board is `Tasks/` in `Z:\repos\Curl`, worked by its own dark factory with its own ID
  allocation. A commit to another repository is not one of root `CLAUDE.md`'s delegated git actions.

## Decision

### 1. The corpus

Phase 7 runs three sets of conversations, and leaves none out:

- **(a) Surl's own conformance conversations.** For each original protocol family, a new class
  `CurlPortBesideUpstreamCurlOver<Family>Tests` in `Conformance/Surl.Conformance.UnitTests` runs the
  same command lines, `surl` options and content as that family's `UpstreamCurl…SurlOver…Tests`
  class, one test per mirrored test, each naming the test it mirrors (`[Description("Mirrors
  UpstreamCurlTalksToSurlOverHttpTests.Get_ServesTheFile")]`). No existing class is edited. Families
  and tasks: HTTP/1.1 and HTTPS (BL-580), FTP and FTPS (BL-581), SCP and SFTP (BL-582), SMTP, IMAP and
  POP3 (BL-583), DICT, Gopher, TELNET, TFTP and MQTT (BL-584), WebSocket (BL-585), LDAP (BL-586), SMB
  (BL-587), RTSP (BL-588), HTTP/2 (BL-589), HTTP/3 (BL-590). A mirrored test that runs with
  `[OSCondition]` keeps its condition.
- **(b) Upstream curl's own test cases.** Every case ADR-0080's runner can run on the platform, with
  the port as the client in place of the reference build (BL-591).
- **(c) The libcurl rows.** The port's `Libcurl` is a managed API, not a native `libcurl-4.dll`, so
  `PinnedLibcurlDriver` cannot load it. Phase 7 gets port-side twins of the two driver scripts that
  take the same arguments and print the same one-line-per-call report, calling the port's `Libcurl`
  assembly pinned in `CurlPortBuilds.json` beside its `curl` (BL-597). BL-585 and BL-588 leave those
  rows to BL-597.

### 2. The pairing

Each case is compared with the upstream build its mirrored test uses on the same platform:
`PinnedUpstreamCurl`'s choice for (a), the case's reference build for (b), the pinned upstream
`libcurl-4.dll` for (c). Each client gets its own fresh `surl`, started with identical options over an
identical copy of the case's content, so neither run sees the other's state.

When the paired build is not 8.21.0 (the SMB build is 8.22.0), the finding names its version line. A
difference that upstream's release notes between 8.21.0 and that version describe as an upstream
change is judged **not comparable: release difference**, not a port defect. When no pinned upstream
build on the platform has a feature the case needs, the mirrored test is already Inconclusive, and so is
the Phase 7 test.

### 3. The port's environment

The port runs **as shipped**: `CURL_EXTENSIONS` is removed from its environment, and from upstream's for
symmetry. `surl` runs as shipped too: `SURL_EXTENSIONS` is removed from its environment. Neither switch
changes how an original scheme is served or requested (ADR-0083 decision 9, and the port's own rule), so
running both ways would double the runs and measure nothing new. Every other environment variable the
mirrored test sets is set identically for both clients; `HOME`, `CURL_HOME` and `USERPROFILE` point at
one empty folder per run, so neither client reads a `.curlrc` or `.netrc` the test did not write.

### 4. What is compared

| Item | Compared | Normalisation, and the case it serves |
| --- | --- | --- |
| Exit code | Exactly. | None. |
| Standard output | Byte for byte after normalisation. | The `surl` port number replaced by `<port>` (every case; each run has its own ephemeral port). A `Date:` or `Last-Modified:` field value under `-i` or `-D -` replaced by `<date>` (HTTP, RTSP). `-w` variables that measure time (`%{time_*}`, `%{speed_*}`) replaced by `<time>`. |
| Standard error | Only where the mirrored test asserts on it, and then only the lines it asserts on, normalised as standard output. | The progress meter is never compared: both clients run with `-s -S` or the mirrored test's own `-s`. The port is not held to upstream's wording where the mirrored test does not pin it. |
| Bytes each client sent to `surl` | Per connection, in order, on every plaintext TCP connection, recorded by a loopback relay (BL-579). | The `surl` and client ports replaced (FTP `PORT`/`EPRT`, `Host:`). A multipart boundary replaced by `<boundary>` everywhere it occurs (HTTP `-F`, SMTP MIME). Digest `cnonce` and the `response` computed from it replaced (HTTP, RTSP, SASL `DIGEST-MD5`). The NTLM client challenge, timestamp and the responses derived from them replaced (HTTP, SMB, SASL `NTLM`). `User-Agent:` replaced by `<user-agent>` (the port reports upstream's version, but its user agent is not a protocol requirement). |
| `surl`'s `-v` notes | Line by line after normalisation, from `SurlOnLoopback.Log`. | Ports as above; time stamps replaced by `<time>`. |
| Files `surl` stored | Byte for byte, where the mirrored test asserts on stored content (uploads). | None. |

Not compared, by design: request bytes inside TLS or SSH (the ClientHello, key exchange and every
encrypted byte depend on the TLS or SSH library, not on curl, and SSH and TLS randoms differ every run),
and request bytes over UDP (TFTP, HTTP/3), which the TCP relay does not see. For those, the exit code,
output, stored files and `surl`'s `-v` notes carry the comparison. An FTP data connection goes straight
to the port `surl` announced, so the relay records the control connection only; uploads are compared
through the stored file. Timing is never compared.

### 5. The verdict

Each case ends in exactly one of:

- **agreement** - every compared item equal after normalisation;
- **port defect** - upstream's run is what the mirrored test asserts, and the port's run differs in a
  compared item;
- **Surl defect** - upstream's own run is not what the mirrored test asserts. Filed on Surl's board,
  never on the port's; the port's run is not judged;
- **not comparable** - with a named reason: `release difference` (decision 2) or a reason a later ADR
  adds. A case missing a pinned file is Inconclusive, not a verdict.

A Phase 7 test **fails** when upstream's run is not what the mirrored test asserts (a Surl defect);
when a port defect appears that the family's findings file does not list; and when a listed finding
stops appearing. It **passes** on agreement and on a port defect the findings file lists. The findings
file therefore stays true of the code, and the suite is green while the port still has defects. Each
findings file has one table whose first column is the Phase 7 test's method name, which the test reads
(from the repository root, as `PinnedUpstreamCurl.RepositoryRoot` finds it).

### 6. The finding's form

One **Phase 7 finding** per disagreement, in
`Documentation/Conformance/CurlPort/<Family>.md` (`Http.md`, `Ftp.md`, `Ssh.md`, `Mail.md`,
`Dict-Gopher-Telnet-Tftp-Mqtt.md`, `WebSocket.md`, `Ldap.md`, `Smb.md`, `Rtsp.md`, `Http2.md`,
`Http3.md`, `Upstream-Test-Cases.md`, `Libcurl.md`). Each file names, once at its head, the port
commit and SHA-256, the upstream builds' SHA-256 and version lines, and Surl's commit it was measured
at. Each finding gives: the Phase 7 test and the test it mirrors; the command line; upstream's result
and the port's result for every item that differs (exit codes, the first differing output line, the
first differing request byte with its connection index and offset); and the specification section or
upstream documentation (with the curl version checked, 8.21.0) that says what the right behaviour is.
Upstream is right by ADR-0003 decision 6; the citation is there so the port's maintainer can fix the
defect without re-deriving it.

### 7. The handoff to the port's board

Surl sessions **never write into `Z:\repos\Curl`**. BL-592 publishes
`Documentation/Conformance/CurlPort/Port-Board-Handoff.md`: one section per port defect in the port's
task shape (title, goal, context, acceptance criteria), naming the finding it comes from and leaving
`id` and `touches` blank. The port's own `/task-plan`, run in a session of the port's repository,
files them: the port's lanes allocate IDs concurrently, and a commit to another repository is not
delegated to a Surl session. Stewart files it himself: he runs the port's `/task-plan` on the handoff,
so he sees each finding before it becomes the port's work (Stewart, 2026-11-04). Surl's part ends when
the handoff is committed and Stewart is told it is ready and how many findings it holds.

### 8. Platforms

Phase 7 runs on **Windows**. `CurlPortBuilds.json` pins only `win-x64`, lanes run on Windows, and
ADR-0003 named Git for Windows' 8.21.0 build "the right baseline for comparing the two in the last
phase". On Linux and macOS each Phase 7 test is Inconclusive with `CurlPortClientUnavailability`'s
message, as `UpstreamCurlUnavailability` reports a missing upstream build. CI does not run the port
(ADR-0083 decision 2). When a `linux-x64` or `osx-arm64` port pin is added, the same tests run there
with no change to this ADR, each platform's findings in its own section of the family's file.

### 9. The extended clients

ADR-0083 decision 10 says "Phase 7 then measures the port's extended clients against these servers
beside its original ones". Read with this ADR: upstream curl has no counterpart to an extended client,
so there is nothing to run *beside*. The port's extended clients meet Surl's extended servers in the
extended track's own `Integration` tasks (BL-422, BL-428, BL-434, BL-440, BL-446, BL-451, BL-456,
BL-462, BL-469, BL-477, BL-486, BL-493, BL-502, BL-508, BL-515, BL-523, BL-529, BL-534, BL-541),
judged by each protocol's specification, and Phase 7 does not run them again. A port defect they find
is recorded in that task's Notes; BL-592 writes each as a Phase 7 finding in decision 6's form, marked
"judged by the specification", in `Documentation/Conformance/CurlPort/Extended.md`, and puts it in the
same handoff (decision 7).
ADR-0083's oracle rule is unchanged: such a finding is about the port, never evidence about Surl.

### 10. The port commit measured

Phase 7 measures one port commit at a time: the one `CurlPortBuilds.json` pins, which every finding
file names. The pin moves only through a task that re-runs every family and updates every findings
file in the same change. Decision 5 makes that self-enforcing: a new pin that fixes a listed defect, or
adds one, turns its test red until the findings file says so. The extended servers' `Integration`
tests use the same pin.

## Consequences

Good:

- Every disagreement lands in a file that a test keeps true, with everything the port's maintainer
  needs to reproduce it, and Surl's suite stays green while the port still has defects.
- The port remains the thing measured: no Phase 7 output becomes an expected value, and a run where
  upstream itself fails is a Surl defect, not a port finding.
- No cross-repository write: the handoff is a file the port's own planner reads.

Costs and caveats:

- Request bytes inside TLS, SSH and UDP are not compared; a port defect visible only there is caught
  only if it changes the exit code, output, stored files or `surl`'s notes.
- Moving the port pin is a large task by design.
- Phase 7 is Windows-only until a non-Windows port pin exists.

## Alternatives considered

- **Compare the port with the expected values of the mirrored tests only, not with a live upstream
  run.** Rejected: success criterion 5 says "beside upstream curl", and a live upstream run in the same
  test separates a Surl regression from a port defect.
- **Run the port both as shipped and with `CURL_EXTENSIONS=0`.** Rejected: twice the runs for no
  original-scheme difference (decision 3).
- **File findings on the port's board directly.** Rejected: ID races with the port's lanes, and an
  undelegated commit to another repository.
- **Keep a permanently red test per port defect.** Rejected: a red suite hides new failures and blocks
  the dark factory's merge.
- **Compare TLS-wrapped request bytes by terminating TLS in the relay.** Rejected: the bytes inside
  would still differ by TLS library only where the library chooses (record sizes), and the plaintext
  twin of each conversation (`http` beside `https`, `ftp` beside `ftps`) already compares curl's own
  bytes.

## Amendment 1 (2026-11-05, BL-580): what the first family run showed decision 4 must leave out

Decided by Claude under Stewart's delegation, in BL-580, from the HTTP/1.1 and HTTPS family's first
runs. Each item below differed between two runs for a reason that is neither client's curl, so
comparing it would have filed a false port finding. None widens what a port finding may excuse: the
exit code, output, asserted stderr lines and stored files are still compared in every case.

- **Digest `nonce`** is replaced by `<nonce>` beside `cnonce` and `response`: it is the value each
  run's own `surl` chose at random, which the client only echoes (`RequestBytesNormalisation`).
- **Request bytes are not recorded** for AWS Signature Version 4 cases (`x-amz-date` and the signature
  over it follow the clock; decision 4 never compares timing) and for Kerberos and `Negotiate` cases
  (each token carries a fresh session key and time stamp, so its bytes are the GSS-API library's, as
  TLS bytes are the TLS library's).
- **`surl`'s packet dumps** (`#<n> > ` and `#<n> < ` lines under `-v`) are not compared on SSH: they carry
  the server's random cookie and padding. Its `*` notes are.
- **How a refused TLS connection ended** (`TLS handshake failed: …`, or an `IOException` from a reset
  connection) is not compared on `https`: the same client produced either note from run to run, and
  which one depends on the TLS library and timing.
- A case whose mirrored test runs a supplementary build, a relay of its own, several listeners or
  per-run arguments passes a `CurlPortRunSetup` that both runs share; set-up steps that are not the
  conversation measured (an SMTP delivery for an IMAP or POP3 read) run once through upstream curl, and
  each run's `surl` starts over its own copy of the result, so both clients meet the same bytes.
