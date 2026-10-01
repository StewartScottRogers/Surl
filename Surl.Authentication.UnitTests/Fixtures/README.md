# Basic and Bearer credential fixtures (BL-111)

Request heads recorded from upstream curl 8.21.0, the win-x64 build pinned in
`UpstreamCurlBuilds.json` (`C:\Program Files\Git\mingw64\bin\curl.exe`, SHA-256
`0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`), with
`Record-CurlExchange.ps1`. Never from the Curl port (ADR-0003).

Each folder holds the recorder's four files: `request.bin` (the bytes curl sent),
`stdout.bin`, `stderr.txt` and `exitcode.txt`. Every case exited 0 with stdout and stderr
empty: curl sent its credentials unasked, got the `401` and stopped there without opening
the second connection. The files are embedded resources of `Surl.Authentication.UnitTests`,
so the tests read them without touching the file system. `.gitattributes` here keeps git
from rewriting their CRLF line endings.

Recorded on 2026-09-29 from the repository root in Windows PowerShell, `-Port 18111
-Connections 2`, with two `-Response` values: connection 1 got `<401>` followed by one
challenge and `\r\n`, connection 2 the `200` of `Surl.Protocol.Http.UnitTests/Fixtures`'
`get-file`. `<401>` is
`HTTP/1.1 401 Unauthorized\r\nDate: Mon, 28 Sep 2026 12:00:00 GMT\r\nServer: surl\r\nContent-Length: 0\r\n`,
`<basic>` `WWW-Authenticate: Basic realm=\"surl\", charset=\"UTF-8\"\r\n` and `<bearer>`
`WWW-Authenticate: Bearer realm=\"surl\"\r\n` (ADR-0032, section 4).

| Folder | Challenge | `-CurlArgs` | `Authorization` sent |
| --- | --- | --- | --- |
| `basic` | `<basic>` | `'-sS','--basic','-u','tester:secret','http://127.0.0.1:18111/x'` | `Basic dGVzdGVyOnNlY3JldA==` |
| `user-default` | `<basic>` | `'-sS','-u','tester:secret','http://127.0.0.1:18111/x'` | `Basic dGVzdGVyOnNlY3JldA==`: `-u` alone is Basic, sent unasked |
| `bearer` | `<bearer>` | `'-sS','--oauth2-bearer','tok','http://127.0.0.1:18111/x'` | `Bearer tok` |
| `basic-non-ascii` | `<basic>` | `'-sS','--basic','-u',"t$([char]0xEB)ster:s$([char]0xE9):cr$([char]0x20AC)t",'http://127.0.0.1:18111/x'` | `Basic dOtzdGVyOnPpOmNygHQ=`: the Windows-1252 bytes `74 EB 73 74 65 72 3A 73 E9 3A 63 72 80 74` |
| `basic-utf8-config` | `<basic>` | `'-sS','--basic','-K',<cfg>,'http://127.0.0.1:18111/x'` | `Basic dMOrc3Rlcjpzw6k6Y3Ligqx0`: the UTF-8 bytes of `tëster:sé:cr€t` |

`<cfg>` is a file in the temporary directory holding the one line
`user = "tëster:sé:cr€t"` in UTF-8 without a byte-order mark.

What the two non-ASCII cases show: the reference build sends the user name and password as
the bytes it was given. A command-line argument on Windows reaches it in the ANSI code page
(Windows-1252 here), whatever `charset="UTF-8"` the challenge names; a config file's bytes,
like a command line on Linux and macOS, reach it as UTF-8. Surl reads Basic credentials as
UTF-8, the charset it announces (RFC 7617 section 2.1), so the first is refused and the
second accepted for the account `tëster:sé:cr€t` (ADR-0035).

## Digest answers (BL-113)

Recorded on 2026-09-29 with the same build and `-Connections 2`, `-Port 18113`: connection 1
got `<401>` followed by
`WWW-Authenticate: Digest realm=\"surl\", qop=\"auth\", algorithm=<A>, nonce=\"fixturenonce\"\r\n`
and `\r\n`, connection 2
`HTTP/1.1 200 OK\r\nDate: Mon, 28 Sep 2026 12:00:00 GMT\r\nServer: surl\r\nContent-Length: 2\r\n\r\nok`.
`request.bin` holds both requests; the second carries the answer. The tests replay it with
`FixedDigestNonceBook`, which knows `fixturenonce`.

| Folder | `<A>` | `-CurlArgs` | Exit | What the answer shows |
| --- | --- | --- | --- | --- |
| `digest-md5` | `MD5` | `'-sS','--digest','-u','tester:secret','http://127.0.0.1:18113/x'` | 0 | `algorithm=MD5`, `qop="auth"`, and `nc=00000002` on this run |
| `digest-md5-sess` | `MD5-sess` | the same | 0 | `algorithm=MD5-sess` |
| `digest-query` | `MD5` | `'-sS','--digest','-u','tester:secret','http://127.0.0.1:18113/dir/x?y=1&z=%41'` | 0 | `uri="/dir/x?y=1&z=%41"`, the request target as sent |
| `digest-post` | `MD5` | `'-sS','--digest','-u','tester:secret','-X','POST','-d','x','http://127.0.0.1:18113/x'` | 0 | the first `POST` carried `Content-Length: 0`; the answer hashes `POST` |
| `digest-non-ascii-argument` | `MD5` | `'-sS','--digest','-u',"t$([char]0xEB)ster:secret",'http://127.0.0.1:18113/x'` | 0 | `username="t\xEBster"`: the Windows-1252 byte, and the response is over the ISO-8859-1 bytes |
| `digest-non-ascii` | `MD5` | `'-sS','--digest','-K',<cfg>,'http://127.0.0.1:18113/x'` | 94 | no answer: SSPI refuses the UTF-8 user `tëster:sé:cr€t` from `<cfg>` (as above) |

What Surl makes of them is ADR-0036: the ISO-8859-1 user name is accepted for an account whose
name and password are all ISO-8859-1.

## NTLM handshakes (BL-120)

Recorded on 2026-09-29 with the same build (`C:\Program Files\Git\mingw64\bin\curl.exe`,
SHA-256 `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`) and
`-Port 18120`, keeping curl's one connection open with `-ResponsesPerConnection`, so
`request-1.bin` holds the request carrying the `NEGOTIATE_MESSAGE` and `request-2.bin` the one
carrying the `AUTHENTICATE_MESSAGE` (`request.bin` holds both). The first request was answered
with `<ntlm401>`,
`HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: NTLM TlRMTVNTUAACAAAACAAIADAAAAAFgoqgASNFZ4mrze8AAAAAAAAAABwAHAA4AAAAUwBVAFIATAACAAgAUwBVAFIATAABAAgAUwBVAFIATAAAAAAA\r\nContent-Length: 0\r\n\r\n`:
the `CHALLENGE_MESSAGE` `NtlmChallengeMessage` builds for that `NEGOTIATE_MESSAGE` and the
server challenge `0123456789abcdef` (ADR-0039), which `FixedNtlmServerChallengeSource` gives
the tests.

| Folder | `-ResponsesPerConnection` and `-Response` values | `-CurlArgs` | Exit | What it shows |
| --- | --- | --- | --- | --- |
| `ntlm` | `2`: `<ntlm401>`, then `HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok` | `'-sS','--ntlm','-u','tester:secret','http://127.0.0.1:18120/x'` | 0 | the NTLMv2 answer for `tester:secret`, empty domain; stdout `ok` |
| `ntlm-two-urls` | `3`: `<ntlm401>`, then `HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok` twice; `-Port 18133` (BL-133) | `'-sS','--ntlm','-u','tester:secret','http://127.0.0.1:18133/x','http://127.0.0.1:18133/y'` | 0 | the same handshake for `/x`, then `request-3.bin`: `GET /y` on the same connection with no `Authorization`, since the connection is logged in; stdout `okok` (ADR-0041) |
| `ntlm-wrong-password` | `3`: `<ntlm401>`, then `HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: NTLM\r\nContent-Length: 0\r\n\r\n` | `'-sS','-f','--ntlm','-u','tester:wrong','http://127.0.0.1:18120/x'` | 22 | the answer for `tester:wrong`; after the second `401` curl gives up (`curl: (22) The requested URL returned error: 401`) and sends no third request. Without `-f` the same run exits 0 with an empty body. |

## Non-ASCII NTLM password (BL-321)

Recorded on 2026-09-30 with `-ResponsesPerConnection 2 -Response <ntlm401>,'HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok'`,
on a Windows 11 machine with ANSI code page 1252 and OEM code page 437. `<cfg>` is a UTF-8 file
holding `user = "tester:pässword"`. Every case exited 0 with stdout `ok`; each `request-2.bin`
holds an NTLMv2 answer for `tester`, empty domain. The builds hash `pässword` differently, so
`NtlmPasswordHashes` keeps one hash for each:

| Folder | Build | `-Port` | `-CurlArgs` | The NT hash the answer proves |
| --- | --- | --- | --- | --- |
| `ntlm-non-ascii-password` | reference | 18321 | `'-sS','--ntlm','-u',"tester:p$([char]0xE4)ssword",'http://127.0.0.1:18321/x'` | `MD4(UTF-16LE("pΣssword"))`: SSPI reads the argument's Windows-1252 byte `E4` in the OEM code page 437 |
| `ntlm-non-ascii-password-utf8-config` | reference | 18323 | `'-sS','--ntlm','-K',<cfg>,'http://127.0.0.1:18323/x'` | `MD4(UTF-16LE("p├ñssword"))`: the UTF-8 bytes `C3 A4` read in code page 437 |
| `ntlm-non-ascii-password-static` | static-curl (`C:\UpstreamCurl\static-curl-8.21.0-windows-x86_64\curl.exe`, SHA-256 `589C8E4D297B4831C82ADF0261FC1CA57CE59D663B91B4106D2EE7DFF3972648`) | 18322 | as `ntlm-non-ascii-password` | `MD4(UTF-16LE("pässword"))`, the specification's |
| `ntlm-non-ascii-password-static-utf8-config` | static-curl | 18324 | as `ntlm-non-ascii-password-utf8-config` | `MD4(UTF-16LE("pässword"))` |

The reference build has no `Unicode` feature and hands SSPI an ANSI identity; static-curl's has
it and hands SSPI the password as UTF-16. Upstream curl's own NTLM code, which the Linux and macOS
builds use, widens each UTF-8 byte instead (`Curl_ntlm_core_mk_nt_hash` in `lib/curl_ntlm_core.c`).

| Folder | Build | `-Port` | `-CurlArgs` | The NT hash the answer proves |
| --- | --- | --- | --- | --- |
| `ntlm-non-ascii-password-linux` | linux-x64 reference (`/opt/upstream-curl/8.21.0/curl`, SHA-256 `153CA463957609117D21A848BE29B70691B85F9E5CC9370C7DAA037B839A4E45`) | 18325 | `'-sS','--ntlm','-u',"tester:p$([char]0xE4)ssword",'http://127.0.0.1:18325/x'` | `MD4` of the UTF-8 bytes `70 C3 A4 73 73 77 6F 72 64`, each widened to 16 bits |
| `ntlm-non-ascii-password-macos` | osx-arm64 reference (`/opt/upstream-curl/8.21.0/curl`, SHA-256 `04E0E69BCD3BD814EC093551A0447AC14EDEEA9D395B468A2025DCB3F766EEBF`) | 18325 | as `ntlm-non-ascii-password-linux` | as `ntlm-non-ascii-password-linux` |

The Linux folder was recorded on 2026-10-01 (BL-324) by `pwsh` 7.6 in WSL Ubuntu 26.04 on the
same Windows machine, with the same `-ResponsesPerConnection` and `-Response` values and
`LANG=C.UTF-8`, so the argument reached curl as UTF-8. The macOS folder is the artifact
`ntlm-non-ascii-password-macos` that the macOS leg of `.github/workflows/ci.yml` recorded on
2026-10-01 in CI run https://github.com/StewartScottRogers/Surl/actions/runs/36848637228
(commit 32f41ad0), committed as downloaded (BL-352). Both exited 0 with stdout `ok`.
`NtlmAuthenticationMethodTests.RecordedAuthenticate_LinuxAndMacOsBuilds_ProveTheNtHashOfTheWidenedUtf8Password`
checks each answer's `NTProofStr` against that hash.

## Negotiate (BL-121)

Recorded on 2026-09-29 with the same build and `-Port 18121 -ResponsesPerConnection 2`, every
request answered with `HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Negotiate\r\nContent-Length: 0\r\n\r\n`,
on a Windows 11 10.0.26200 machine that is not in a domain.

| Folder | `-CurlArgs` | Exit | What it shows |
| --- | --- | --- | --- |
| `negotiate-no-token` | `'-sS','-v','--negotiate','-u','tester:secret','http://127.0.0.1:18121/x'` | 0 | SSPI's `InitializeSecurityContext` failed with `SEC_E_NO_CREDENTIALS` before the request and again after the `401`; curl sent one request with no `Authorization` and gave up with an empty body. With `-f` instead of `-v` it exits 22, `curl: (22) InitializeSecurityContext failed: SEC_E_NO_CREDENTIALS (0x8009030e) - ...`. The same happened for `localhost`, a `--resolve`d name, `SURL\tester`, `tester@surl`, `.\tester`, `-u :` and `--delegation always`. |

The reference build sends no token because it passes SSPI the `PackageList` `!ntlm`, upstream's
change after the tag 8.21.0 (ADR-0040, "Measured"; found in BL-134).
`NegotiateAuthenticationMethodTests` replays the NTLM messages of `ntlm` above, bare and wrapped
in SPNEGO as RFC 4178 lays it out (`SpnegoTestTokens`), and the Negotiate handshakes below.

## Negotiate from the unpatched 8.21.0 build (BL-134)

Recorded on 2026-09-29, on the same machine, with stunnel/static-curl's build of the tag 8.21.0
(`C:\UpstreamCurl\static-curl-8.21.0-windows-x86_64\curl.exe`, SHA-256
`589C8E4D297B4831C82ADF0261FC1CA57CE59D663B91B4106D2EE7DFF3972648`; ADR-0042) and `-Port 18134`.
The first request was answered with `<negotiate401>`,
`HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Negotiate TlRMTVNTUAACAAAACAAIADAAAAAFgoqgASNFZ4mrze8AAAAAAAAAABwAHAA4AAAAUwBVAFIATAACAAgAUwBVAFIATAABAAgAUwBVAFIATAAAAAAA\r\nContent-Length: 0\r\n\r\n`:
the `CHALLENGE_MESSAGE` of `<ntlm401>`, which is also what `NtlmChallengeMessage` builds for this
build's `NEGOTIATE_MESSAGE` and the fixed server challenge. SSPI sent bare NTLM after
`Negotiate`, on the first request already, and no SPNEGO, so no `mechListMIC`.

| Folder | `-ResponsesPerConnection` and `-Response` values | `-CurlArgs` | Exit | What it shows |
| --- | --- | --- | --- | --- |
| `negotiate-ntlm` | `2`: `<negotiate401>`, then `HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok` | `'-sS','--negotiate','-u','tester:secret','http://127.0.0.1:18134/x'` | 0 | `request-1.bin`: `Authorization: Negotiate` with a 40-byte `NEGOTIATE_MESSAGE`; `request-2.bin`: the NTLMv2 `AUTHENTICATE_MESSAGE` for `tester:secret`, empty domain; stdout `ok` |
| `negotiate-ntlm-wrong-password` | `3`: `<negotiate401>`, then `HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Negotiate\r\nContent-Length: 0\r\n\r\n` | `'-sS','-f','--negotiate','-u','tester:wrong','http://127.0.0.1:18134/x'` | 22 | the answer for `tester:wrong`; after the second `401` curl gives up (`curl: (22) The requested URL returned error: 401`) and sends no third request |
| `negotiate-ntlm-two-urls` | `3`: `<negotiate401>`, then `HTTP/1.1 200 OK
Content-Length: 2

ok` twice; `-Port 18135` (BL-135) | `'-sS','--negotiate','-u','tester:secret','http://127.0.0.1:18135/x','http://127.0.0.1:18135/y'` | 0 | the same handshake for `/x`, then `request-3.bin`: `GET /y` on the same connection with no `Authorization`, since the connection is logged in; stdout `okok` (ADR-0044) |

## AWS Signature Version 4 (BL-122)

Recorded on 2026-09-29 with the reference build and `-Port 18122`, one connection answered with
`HTTP/1.1 200 OK\r\nDate: Mon, 28 Sep 2026 12:00:00 GMT\r\nServer: surl\r\nContent-Length: 2\r\n\r\nok`
(`aws-sigv4-refused`: with `<401>` and
`WWW-Authenticate: Digest realm=\"surl\", qop=\"auth\", algorithm=MD5, nonce=\"fixturenonce\"\r\n\r\n`).
Every case ran `'-sS','--aws-sigv4',<provider>,'-u','AKIDEXAMPLE:secret'` (`AKIDEXAMPLE:wrong`
for `aws-sigv4-refused`) and exited 0. The signing time is the recorded `X-<Provider>-Date`
field, which the tests set their clock to. What the requests show is ADR-0043's "Measured".

| Folder | `<provider>` | Other `-CurlArgs` | What it shows |
| --- | --- | --- | --- |
| `aws-sigv4-get` | `aws:amz:us-east-1:s3` | `'http://127.0.0.1:18122/x'` | `SignedHeaders=host;x-amz-content-sha256;x-amz-date`, the empty body's hash |
| `aws-sigv4-put` | `aws:amz:us-east-1:s3` | `'-X','PUT','-d','body','http://127.0.0.1:18122/x'` | `x-amz-content-sha256` is the body's SHA-256; `Content-Type` not signed |
| `aws-sigv4-query` | `aws:amz:us-east-1:s3` | `'http://127.0.0.1:18122/dir/x?z=%41&b=2&a=1'` | the query sorted and `%41` read as `A` |
| `aws-sigv4-query-encoding` | `aws:amz:us-east-1:s3` | `'http://127.0.0.1:18122/x?b=%7e&a=x%2fy&c&a-b=1&A=Z+z&e=%zz&f=a=b'` | canonical query `A=Z%20z&a=x%2Fy&a-b=1&b=~&c=&e=%25zz&f=a%3Db` |
| `aws-sigv4-path-encoding` | `aws:amz:us-east-1:ec2` | `'http://127.0.0.1:18122/a%41b/c~d!e'` | no content hash field; the path encoded again, `/a%2541b/c~d%21e` |
| `aws-sigv4-ec2-put` | `aws:amz:us-east-1:ec2` | `'-X','PUT','-d','body','http://127.0.0.1:18122/x'` | no content hash field, so the body's hash is signed but not sent |
| `aws-sigv4-provider-only` | `aws` | `'--resolve','s3.us-east-1.example:18122:127.0.0.1','http://s3.us-east-1.example:18122/x'` | service and region from the host name; `X-Aws-Date`, `x-aws-content-sha256` |
| `aws-sigv4-other-provider` | `osc:osc:eu-west-2:api` | `'http://127.0.0.1:18122/x'` | `OSC4-HMAC-SHA256`, `osc4_request`, `X-Osc-Date` |
| `aws-sigv4-extra-headers` | `aws:amz:us-east-1:s3` | `'-H','X-Test:  a   b  ','-H','Content-Type: text/plain','http://127.0.0.1:18122/x'` | both fields signed; `x-test` signed as `a b` |
| `aws-sigv4-refused` | `aws:amz:us-east-1:s3` | `'http://127.0.0.1:18122/x'` | signed with the wrong secret; after the `401` curl exits 0 with an empty body and does not sign again |

### Bodies (BL-136)

Recorded on 2026-09-29 as above, with `-Port 18136` and the same `200`; each exited 0. `<file>`
is a file holding the four bytes `body`. What they show is ADR-0045's "Measured".

| Folder | `<provider>` | Other `-CurlArgs` | What it shows |
| --- | --- | --- | --- |
| `aws-sigv4-unsigned-payload` | `aws:amz:us-east-1:s3` | `'-H','x-amz-content-sha256: UNSIGNED-PAYLOAD','-X','PUT','-d','body','http://127.0.0.1:18136/x'` | the field sent once, as given, and signed with `UNSIGNED-PAYLOAD` as the payload hash |
| `aws-sigv4-upload` | `aws:amz:us-east-1:s3` | `'-T',<file>,'http://127.0.0.1:18136/upload'` | `-T` sends `x-amz-content-sha256: UNSIGNED-PAYLOAD` itself |
| `aws-sigv4-ec2-upload` | `aws:amz:us-east-1:ec2` | `'-T',<file>,'http://127.0.0.1:18136/upload'` | no content hash field, and the signature is over the empty body's hash though `body` is sent |

## LDAP NTLM sealing (BL-329)

Recorded on 2026-09-30 with the win-x64 reference build and `Record-CurlExchange.ps1 -Ldap`,
`-Port 18329 -CurlTimeoutMilliseconds 20000`, two `-LdapEntry` values - the root DSE
`dn: \nsupportedSASLMechanisms: GSS-SPNEGO\nsupportedSASLMechanisms: NTLM` and
`dn: cn=alice,dc=example,dc=com\nobjectClass: person\ncn: alice\nsn: Smith\nmail: alice@example.com` -
and `<challenge>`, the `CHALLENGE_MESSAGE` Surl's LDAP handshake answers `WinLDAP`'s
`NEGOTIATE_MESSAGE` (flags `E20882B7`) with over the server challenge `0123456789abcdef`
(ADR-0072 decision 4: flags `E08A8235`, sign, seal and key exchange granted):
`4E544C4D53535000020000000800080030000000` `35828AE0` `0123456789ABCDEF` `0000000000000000`
`1C001C0038000000` `5300550052004C00` `020008005300550052004C00` `010008005300550052004C00`
`00000000`. Each folder holds the recorder's files and `transcript.txt`, which the tests read.

| Folder | `-LdapReply` | `-CurlArgs` | What it shows |
| --- | --- | --- | --- |
| `ldap-ntlm-sealed` | `"BIND=0\|\|\|<challenge>"`, `'BIND=0'` | `'-sS','--ntlm','-u','alice:secret','ldap://127.0.0.1:18329/dc=example,dc=com'` | Sicily: `[10]` with the `NEGOTIATE_MESSAGE`, `[11]` with the `AUTHENTICATE_MESSAGE` (flags `E2888235`), then an 84-byte sealed buffer: the base search, message ID 4 |
| `ldap-negotiate-sealed` | `"BIND=14\|\|<challenge>"`, `'BIND=0'` | the same with `--negotiate` | `GSS-SPNEGO` with bare NTLM both ways, then an 84-byte sealed buffer: the base search, message ID 5 |

Both exited 39 (`LDAP remote: Server Down`): the recorder cannot unseal and closed, and
`WinLDAP`'s reconnect found no server it could use. `LdapNtlmSaslMechanismTests` replays both
binds with the fixed challenge and unseals each buffer to the base search of ADR-0072's
simple-bind transcript.

## LDAP DIGEST-MD5 (BL-326)

Recorded on 2026-09-30 with the win-x64 reference build and `Record-CurlExchange.ps1 -Ldap`,
`-Port 18326 -CurlTimeoutMilliseconds 20000`, two `-LdapEntry` values - the root DSE
`dn: \nsupportedSASLMechanisms: DIGEST-MD5` and
`dn: cn=alice,dc=example,dc=com\nobjectClass: person\ncn: alice` - and `-LdapReply`
`"BIND=14||<challenge hex>"`, `'BIND=0'`, where `<challenge>` is Surl's LDAP challenge over the
fixed nonce (ADR-0072 decision 4):
`realm="surl",nonce="MDEyMzQ1Njc4OWFiY2RlZg==",qop="auth,auth-int,auth-conf",cipher="3des,rc4",maxbuf=65536,charset=utf-8,algorithm=md5-sess`.
`-CurlArgs` were `'-sS','--digest','-u','alice:secret','ldap://127.0.0.1:18326/dc=example,dc=com'`.

| Folder | What it shows |
| --- | --- |
| `ldap-digest-md5` | `WinLDAP` opens the bind with empty credentials, then answers the challenge with `qop=auth-conf,cipher=3des`, `realm=""`, `digest-uri="ldap/127.0.0.1"` and `response=a5161e5d54e3950512ceb4bd61f5a564` |

curl exited 38 (`bind via ldap_win_bind Protocol Error`): the recorder's success carried no
`rspauth`, and it cannot compute one, since it does not know the password. The response and the
`rspauth` it calls for (`317c080c54526d1d62d9f392f87cf69a`) and `H(A1)`
(`68d9795fbde795dac0fdfdbd4461a106`) were checked with PowerShell's MD5 alone when recorded.
`LdapDigestMd5SaslMechanismTests` replays the bind, and checks the 3DES, RC4 and integrity
layers against RFC 2831 sections 2.3 and 2.4's layout computed in the test.

## LDAP Kerberos inside GSS-SPNEGO (BL-327)

Recorded on 2026-09-30 at 23:24 (-07:00) with the win-x64 reference build on the lane machine,
which has BL-265's `SURL.TEST` realm mapping, through `Record-CurlExchange.ps1 -Ldap
-LdapKerberosAcceptor -KerberosTestKdc -KerberosPassword 'surl-test-password'
-KerberosServicePrincipal 'ldap/Stewart-Rogers-AI-PC:18389'`, `-Port 18389
-LdapIdleMilliseconds 3000 -CurlTimeoutMilliseconds 30000`, two `-LdapEntry` values - the root
DSE `dn: \nsupportedSASLMechanisms: GSS-SPNEGO\nsupportedSASLMechanisms: GSSAPI` and
`dn: dc=example,dc=com\ncn: example` - and `-CurlArgs`
`'-sS','--negotiate','-u','tester@SURL.TEST:surl-test-password','ldap://localhost:18389/dc=example,dc=com?cn?base'`.
The recorder answered the bind and wrapped its replies through `Run-KerberosAcceptor.cs` (ADR-0072
Amendment 1).

| Folder | What it shows |
| --- | --- |
| `ldap-kerberos-sealed` | `WinLDAP` asks the KDC for `ldap/Stewart-Rogers-AI-PC:18389` (`kdc.log`), binds `GSS-SPNEGO` with a `NegTokenInit` (MS-KRB5, Kerberos, NEGOEX, NTLMSSP) whose optimistic AP-REQ asks for mutual authentication and confidentiality, takes the `accept-completed` `negTokenResp` with the AP-REP, then sends the base search and the unbind as sealed RFC 4121 wrap tokens (`EC` 0, `RRC` 28) |

curl exited 0 and printed `DN: dc=example,dc=com` with `cn: example`. The folder also holds
`service.keytab`, the keys the test KDC drew for that run, and `kdc.log`.
`LdapKerberosSaslMechanismTests` replays the bind with that keytab on a clock set to the
recording's time and unwraps both buffers.
