# Phase 7 findings: SMB and SMBS

What `CurlPortBesideUpstreamCurlOverSmbTests` in `Conformance/Surl.Conformance.UnitTests` found when it
ran every SMB and SMBS conversation of `UpstreamCurlTransfersFilesWithSurlOverSmbTests` through pinned
upstream curl and the pinned Curl port, each against its own fresh `surl` (ADR-0089, BL-587).

## Measured

| What | Pin |
| --- | --- |
| Curl port | commit `b42c319ba940f66452b2b6f3e9910d46825b08fe`, `curl.exe` SHA-256 `F28D1417807AA862BA2A6D5B0BF0CF1200673BE45B959B1A78E1E4B476EF8FA7` (`CurlPortBuilds.json`, `win-x64`) |
| Upstream curl | SHA-256 `589C8E4D297B4831C82ADF0261FC1CA57CE59D663B91B4106D2EE7DFF3972648`, `curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 OpenSSL/4.0.1 zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 c-ares/1.34.6 libidn2/2.3.8 libpsl/0.22.0 libssh2/1.11.1 nghttp2/1.69.0 ngtcp2/1.23.0 nghttp3/1.16.0` (ADR-0030's static-curl build, which `PinnedUpstreamCurl` picks for `smb` because the reference build has no SMB; ADR-0089 decision 2) |
| Surl | commit `4e626fa87f39c6c5d141a3bac7b5f9353ab7338c` |
| Platform | Windows (`win-x64`); off Windows every case is Inconclusive until a port pin exists there (ADR-0089 decision 8) |
| Date | 2026-11-05 |

## Result

24 cases on Windows: 24 port defects, no agreement, no Surl defect, none not comparable. Upstream's run
is what the mirrored test asserts in every case.

Compared, per ADR-0089 decision 4: exit code, standard output, `surl`'s `-v` notes, the bytes each
client sent on every `smb://` connection (the two `smbs://` cases compare no request bytes, which are
TLS's), and the stored file for each upload case. The mirrored test asserts on no standard error line,
so none is compared.

All 24 findings have one cause. The port answers every `smb://` and `smbs://` URL with exit code 1 and
`curl: (1) Protocol "smb" is disabled` without connecting, so `surl` notes no exchange, the relay
records no connection from the port, and no upload is stored. At port commit `b42c319`,
`Curl.Engine.UnitLibrary/EngineComposition.cs` registers `smb` and `smbs` with a
`DisabledProtocolHandler`, and `curl -V` lists neither in `Protocols:`. Upstream curl 8.21.0 built with
SMB (`docs/libcurl/opts/CURLOPT_PROTOCOLS_STR.md` lists `SMB` and `SMBS`; `docs/URL-SYNTAX.md`,
"SMB") logs in with NTLMv1, downloads and uploads in each case below.

This is the reference build's own answer: the pinned Windows reference build (SHA-256
`0E773709…8778`, Schannel), which has no SMB, prints the same `curl: (1) Protocol "smb" is disabled`
and exits 1. The port follows the reference build's protocol set. ADR-0089 decision 2 pairs each case
with the build its mirrored test uses, and that build answers SMB, so by decision 5 each case is a port
defect: the port, which has its own `Curl.Protocol.Smb.UnitLibrary`, cannot be measured over SMB until
it turns that handler on. When it does, every row below must agree or be replaced by the defect it
then shows.

## Findings

The test reads the first column of this table (ADR-0089 decision 5): a listed case must still show its
port defect, and an unlisted case must agree. A data-driven case is named `Method[data row]`, the data
row's values joined by `, `. Every command line starts `curl -sS -m 20`. In every row the port exits 1
with `curl: (1) Protocol "smb" is disabled` (`"smbs"` for the two `smbs://` rows), writes nothing, and
opens no connection, so `surl` notes no exchange for it; on every `smb://` row but
`Download_NoFileAfterTheShare_CurlItselfExits3`, where upstream does not connect either, the relay's
connection 0 holds upstream's bytes and is absent for the port.

| Phase 7 test | Mirrors | Finding |
| --- | --- | --- |
| `Download_ServedFile_WritesItsBytes[files/hello.txt]` | `Download_ServedFile_WritesItsBytes` | `-u alice:secret {surl}/files/hello.txt`: upstream exits 0 writing `hello smb`; port exits 1. |
| `Download_ServedFile_WritesItsBytes[files/big.bin]` | `Download_ServedFile_WritesItsBytes` | `-u alice:secret {surl}/files/big.bin`: upstream exits 0 writing the 40000 bytes; port exits 1. |
| `Download_UserWithDomain_LogsInAsTheUser[WORKGROUP/alice:secret]` | `Download_UserWithDomain_LogsInAsTheUser` | `-u WORKGROUP/alice:secret {surl}/files/hello.txt`: upstream exits 0 writing `hello smb`; port exits 1. |
| `Download_UserWithDomain_LogsInAsTheUser[WORKGROUP\alice:secret]` | `Download_UserWithDomain_LogsInAsTheUser` | `-u WORKGROUP\alice:secret {surl}/files/hello.txt`: upstream exits 0 writing `hello smb`; port exits 1. |
| `Download_WrongPasswordOrUserNameCase_Exits67[alice:wrong]` | `Download_WrongPasswordOrUserNameCase_Exits67` | `-u alice:wrong {surl}/files/hello.txt`: upstream exits 67 after `surl` refuses the login; port exits 1. |
| `Download_WrongPasswordOrUserNameCase_Exits67[ALICE:secret]` | `Download_WrongPasswordOrUserNameCase_Exits67` | `-u ALICE:secret {surl}/files/hello.txt`: upstream exits 67 after `surl` refuses the login; port exits 1. |
| `Download_NoUser_CurlItselfExits67` | `Download_NoUser_CurlItselfExits67` | `{surl}/files/hello.txt`: upstream connects, sends nothing and exits 67 for want of a user; port exits 1. |
| `Download_NoFileAfterTheShare_CurlItselfExits3` | `Download_NoFileAfterTheShare_CurlItselfExits3` | `-u alice:secret {surl}/files`: upstream exits 3 for want of a file, without connecting; port exits 1. |
| `Download_MissingShareOrFileHiddenShareOrDirectory_Exits78[nosuch/hello.txt]` | `Download_MissingShareOrFileHiddenShareOrDirectory_Exits78` | `-u alice:secret {surl}/nosuch/hello.txt`: upstream exits 78; port exits 1. |
| `Download_MissingShareOrFileHiddenShareOrDirectory_Exits78[.hidden/x.txt]` | `Download_MissingShareOrFileHiddenShareOrDirectory_Exits78` | `-u alice:secret {surl}/.hidden/x.txt`: upstream exits 78; port exits 1. |
| `Download_MissingShareOrFileHiddenShareOrDirectory_Exits78[files/missing.txt]` | `Download_MissingShareOrFileHiddenShareOrDirectory_Exits78` | `-u alice:secret {surl}/files/missing.txt`: upstream exits 78; port exits 1. |
| `Download_MissingShareOrFileHiddenShareOrDirectory_Exits78[files/sub]` | `Download_MissingShareOrFileHiddenShareOrDirectory_Exits78` | `-u alice:secret {surl}/files/sub`: upstream exits 78; port exits 1. |
| `Download_MissingShareOrFileHiddenShareOrDirectory_Exits78[files/]` | `Download_MissingShareOrFileHiddenShareOrDirectory_Exits78` | `-u alice:secret {surl}/files/`: upstream exits 78; port exits 1. |
| `Download_DotDotPathAsIs_Exits78` | `Download_DotDotPathAsIs_Exits78` | `-u alice:secret --path-as-is {surl}/files/../files/hello.txt`: upstream exits 78; port exits 1. |
| `Upload_UploadsOff_Exits9AndStoresNothing` | `Upload_UploadsOff_Exits9AndStoresNothing` | `-u alice:secret -T up.txt {surl}/files/up.txt`: upstream exits 9 and nothing is stored; port exits 1. |
| `Upload_UploadsOn_StoresTheBytes[up.txt, files/up.txt]` | `Upload_UploadsOn_StoresTheBytes` | `-u alice:secret -T up.txt {surl}/files/up.txt` with `--allow-uploads`: upstream exits 0 and `files/up.txt` holds its 9 bytes; port exits 1 and stores nothing. |
| `Upload_UploadsOn_StoresTheBytes[big.bin, files/big2.bin]` | `Upload_UploadsOn_StoresTheBytes` | `-u alice:secret -T big.bin {surl}/files/big2.bin` with `--allow-uploads`: upstream exits 0 and `files/big2.bin` holds its 40000 bytes; port exits 1 and stores nothing. |
| `Upload_MissingShare_Exits78` | `Upload_MissingShare_Exits78` | `-u alice:secret -T up.txt {surl}/nodir/up.txt` with `--allow-uploads`: upstream exits 78; port exits 1. |
| `Upload_PastMaxFilesize_Exits25AndLeavesNoFile` | `Upload_PastMaxFilesize_Exits25AndLeavesNoFile` | `-u alice:secret -T big.bin {surl}/files/big3.bin` with `--allow-uploads --max-filesize 1000`: upstream exits 25 and nothing is stored; port exits 1. |
| `Download_NoAccountsOrNtlmV1NotAccepted_Exits67[--auth, ntlmv1]` | `Download_NoAccountsOrNtlmV1NotAccepted_Exits67` | `-u alice:secret {surl}/files/hello.txt` against `surl --auth ntlmv1` with no account: upstream exits 67; port exits 1. |
| `Download_NoAccountsOrNtlmV1NotAccepted_Exits67[-u, alice:secret]` | `Download_NoAccountsOrNtlmV1NotAccepted_Exits67` | `-u alice:secret {surl}/files/hello.txt` against `surl -u alice:secret` without NTLMv1: upstream exits 67; port exits 1. |
| `Download_AllowAnonymous_LetsAnyLoginIn` | `Download_AllowAnonymous_LetsAnyLoginIn` | `-u anyone:anything {surl}/files/hello.txt` against `surl --allow-anonymous`: upstream exits 0 writing `hello smb`; port exits 1. |
| `Smbs_Insecure_WritesTheFile` | `Smbs_Insecure_WritesTheFile` | `-k -u alice:secret {surl}/files/hello.txt` over `smbs://`: upstream exits 0 writing `hello smb`; port exits 1 (`Protocol "smbs" is disabled`). |
| `Smbs_SelfSignedCertificateNotTrusted_Exits60` | `Smbs_SelfSignedCertificateNotTrusted_Exits60` | `-u alice:secret {surl}/files/hello.txt` over `smbs://` with a self-signed certificate: upstream exits 60; port exits 1 (`Protocol "smbs" is disabled`). |
