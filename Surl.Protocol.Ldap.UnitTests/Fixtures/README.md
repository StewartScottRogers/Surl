# LDAP fixtures

Exchanges recorded from upstream curl 8.21.0, the win-x64 build pinned in
`UpstreamCurlBuilds.json` (`C:\Program Files\Git\mingw64\bin\curl.exe`, SHA-256
`0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`, `WinLDAP`), with
`Record-CurlExchange.ps1 -Ldap` (ADR-0072's cases, recorded again by BL-308). Never from the
Curl port (ADR-0003).

Each folder holds the recorder's files: `request.bin` (every byte curl sent, in order),
`transcript.txt` (each message decoded, then its hex, and the recorder's answers),
`stdout.bin`, `stderr.txt` and `exitcode.txt`. Every run opens two connections - curl's own
connect, which sends nothing, then `WinLDAP`'s - so `request.bin` holds `WinLDAP`'s messages
alone, each in BER's four-byte long-form length (`30 84 00 00 00 1B ...`). The recorder answered
with its own entry `E` (`dn: cn=alice,dc=example,dc=com`, `objectClass: person`, `cn: alice`,
`sn: Smith`, `mail: alice@example.com`) and the result codes below; the tests replay curl's
bytes against `LdapDirectoryFixture.PeopleEntries` and check what Surl answers. The files are
embedded resources of `Surl.Protocol.Ldap.UnitTests`; `.gitattributes` here keeps git from
rewriting their bytes. `RecordedFixture.ReadRequestMessages` cuts `request.bin` into messages.

Recorded on 2026-09-30 from the repository root, in PowerShell 7, with
`$u = 'ldap://127.0.0.1:18389/dc=example,dc=com'`, `$e` the entry `E` written as
`-LdapEntry` text, and each case run as
`.\Record-CurlExchange.ps1 -Port 18389 -Ldap -CurlTimeoutMilliseconds 20000 -LdapEntry $e [-LdapReply <reply>] -CurlArgs <args> -OutDirectory Surl.Protocol.Ldap.UnitTests\Fixtures\<folder>`:

| Folder | `-CurlArgs` | `-LdapReply` | curl sent | Exit |
| --- | --- | --- | --- | --- |
| `simple-bind-base-search` | `'-sS','-u','alice:secret',$u` | | bind v3 `alice`, base search, unbind | 0 |
| `one-level-search` | `'-sS','-u','alice:secret',"$u?cn,mail?one"` | | bind, one-level search for `[cn, mail]`, unbind | 0 |
| `subtree-filtered-search` | `'-sS','-u','alice:secret',"$u?cn?sub?(&(objectClass=person)(\|(cn=al*)(sn>=K))(!(mail=*@other.example)))"` | | bind, subtree search with the filter as written, unbind | 0 |
| `empty-result-search` | `'-sS','-u','alice:secret',"$u??sub?(cn=nobody)"` | | bind, subtree search `(cn=nobody)`, unbind | 0 |
| `no-such-object-search` | `'-sS','-u','alice:secret','ldap://127.0.0.1:18389/dc=nowhere'` | `SEARCH=32` | bind, base search of `dc=nowhere`, unbind | 39, `LDAP remote: No Such Object` |
| `wrong-password-version-2-retry` | `'-sS','-u','alice:wrong',$u` | `BIND=49` | bind v3, **the same bind with version 2**, unbind | 38, `Invalid Credentials` |
| `plaintext-bind-refused` | `'-sS','-u','alice:secret',$u` | `BIND=13` | bind v3, bind v2, unbind | 38, `Confidentiality Required` |

**Abandon is never sent.** With `-LdapReply 'SEARCH=SILENT'` and `-LdapIdleMilliseconds 30000`,
`WinLDAP` sent no `abandonRequest` in the 40 seconds before the recorder killed curl, so no
fixture holds one; `LdapProtocolServerOperationTests` builds abandon, unknown operations,
malformed messages, compare and writes by hand, and `LdapProtocolServerSaslBindTests` and
`LdapProtocolServerStartTlsTests` the SASL, Sicily and `StartTLS` cases no recording holds.

## SASL and Sicily binds (BL-309)

`ldap-ntlm-sealed`, `ldap-negotiate-sealed` and `ldap-digest-md5` are byte-for-byte copies of the
folders of the same names in `Surl.Authentication.UnitTests/Fixtures`, recorded there by BL-329
and BL-326 with the same pinned build; that folder's README says how, with which `-LdapReply`
answers and which `CHALLENGE_MESSAGE` and `DIGEST-MD5` challenge. Their `request.bin` also holds
`WinLDAP`'s security-layer buffers - a 4-byte big-endian length then that many sealed bytes -
which `RecordedFixture.ReadRequestMessages` keeps whole, and the reconnect after the recorder
closed.

| Folder | curl sent | What `LdapProtocolServerSaslBindTests` replays |
| --- | --- | --- |
| `ldap-ntlm-sealed` | root DSE read, Sicily `[10]` `NEGOTIATE_MESSAGE`, `[11]` `AUTHENTICATE_MESSAGE`, an 84-byte sealed buffer | the first four, through a fake SASL policy answering the recorded challenge: `[10]` answered `success` with the challenge as the matched DN, the sealed buffer handed to the security layer |
| `ldap-negotiate-sealed` | two root DSE reads, `GSS-SPNEGO` with bare NTLM both ways, an 84-byte sealed buffer | the first five: `saslBindInProgress` with the challenge as `serverSaslCreds`, then `success` |
| `ldap-digest-md5` | two root DSE reads, `DIGEST-MD5` with **empty** credentials, then its response | the first four: the empty initial response passed as empty, not absent, and `success` carrying `rspauth=...` as `serverSaslCreds` |
